using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/*
  From 
  http://codeincomplete.com/posts/2012/6/23/javascript_racer_v1_straight/
  and
  http://www.extentofthejam.com/pseudo/

  I'm using my own coordinate system, which will make things harder to
  follow. You're welcome.

  World
  x - right perpendicular to the road positive
  y - forward along the road positive
  z - up positive

  Screen
  x - right positive
  y - up positive
  z - into the screen positive

  
  Now, we define some terms
  worldCameraHeight - height of camera above ground
  screenDistance - distance from chair to screen
  worldDistance - distance from virtual camera to world point (car)
  screenY - point where car renders onscreen

  screenY / screenDistance = worldCameraHeight / worldDistance
  screenY = screenDistance * worldCameraHeight / worldDistance

  Similarly

  screenX / screenDistance = worldPointLateralDisplacement / worldDistance
  screenX = screenDistance * worldPointLateralDisplacement / worldDistance

  xWorld, yWorld, zWorld
  translate
  xCamera, yCamera, zCamera
  project
  xProj, yProj, (zProj?)
  scale
  xScreen, yScreen
 */

/// <summary>
///   Segments are the small chunks of track ~1m in length that get rendered.
/// </summary>
enum SegmentColor
{
    Dark,
    Light
};

class Particle
{
    public Vector2 position;
    public Vector2 velocity;
    public int colorIndex;
    public float secondsRemaining;
}

class Bullet
{
    public float xOffset;
    public float trackPosition;
    public int particleIndex;
    public float distanceRemaining;
    public float speed;
    public float segmentFrac;
}

class EnemyCar
{
    public float xOffset;
    public float trackPosition; // distance along track, y
    public int carSpriteIndex;
    public float speed;
    public float segmentFrac; // fraction along segment, y [0, 1]
    public bool hit; // have I been hit?
}

class SpriteContainer
{
    public Texture2D sprite;
    public float xOffset;
    public int treeIndex;
}

class Segment
{
    public int index;
    public Vector3 worldPosition1;
    public Vector3 worldPosition2;

    public Vector3 cameraPosition1;
    public Vector3 cameraPosition2;

    public Vector3 screenPosition1;
    public Vector3 screenPosition2;

    public SegmentColor color;
    public float curve;

    public List<SpriteContainer> sprites;
    public int clipHeight;

    public List<EnemyCar> cars;
    public List<Bullet> bullets;
    public List<Particle> particles;
}

class StraightSegment
{
}

class CurveSegment
{
}

enum LegIdentifier
{
    Manchester = 0, // MAN -> BOS
    Boston = 1,
    Albany = 2,
    NewYork = 3,
    Philadelphia = 4,
    Harrisburg = 5,
    Baltimore = 6,
    WashingtonDC = 7
}

public class HighwayMode 
{
    public Texture2D background;
    
    int RUMBLE_LENGTH = 4;

    int SKY_COLOR = 6;
    int DARK_GRASS_COLOR = 4;
    int LIGHT_GRASS_COLOR = 12;
    int DARK_ROAD_COLOR = 5;
    int LIGHT_ROAD_COLOR = 10;
    int DARK_RUMBLE_COLOR = 1;
    int LIGHT_RUMBLE_COLOR = 15;
    
    float tripDistance;
    float segmentDistance;

    float NEAR_PLANE = 1.0f;
    float FAR_PLANE = 10000.0f;

    const float CAMERA_FOV_DEGREES = 45.0f;
    const float CAMERA_FOV_RADIANS = HighwayMode.CAMERA_FOV_DEGREES * Mathf.Deg2Rad;
    float SCREEN_DISTANCE = 1.0f / Mathf.Tan(HighwayMode.CAMERA_FOV_RADIANS / 2.0f);

    int ROADLENGTH_SHORT = 25;
    int ROADLENGTH_MEDIUM = 50;
    int ROADLENGTH_LONG = 100;

    float CURVE_NONE = 0.0f;
    float CURVE_EASY = 0.02f;
    float CURVE_MEDIUM = 0.04f;
    float CURVE_HARD = 0.06f;

    float HILL_NONE = 0.0f;
    float HILL_LOW = 5.0f;
    float HILL_MEDIUM = 10.0f;
    float HILL_HIGH = 15.0f;
    
    int frameCounter;

    // car constants
    float maxSpeed = 60.0f;
    float bulletSpeed = 30.0f;
    float accel = 10.0f;
    float brake = -15.0f;
    float decel = -1.0f;
    float offRoadDecel = -3.0f;
    float offRoadLimit = 20.0f;

    float carVisualOffset = 7.0f;

    float centrifugal = 30.0f; // push we feel going around a curve

    float worldCameraHeight = 2.0f;
    
    float speed; // m/s
    float trackPosition; // world (y) position of the camera
    float worldPlayerPosition; // world (x) position of the player's
                               // car
    int playerHitPoints = 3;
    float playerDeadTime = 0.0f;
    bool isShowingAttract = false;
    bool winnerScreen = false;
    float timeInAttract = 0.0f;
    int backgroundIndex = 0;
    int lapsNeeded = 0;
    LegIdentifier currentLeg;

    float trackLength; // world extent (y) of road that we have data
                       // for (see also tripDistance?) TODO

    int legCarCount = 12;

    float segmentLength = 2.0f;

    List<Segment> segments;

    List<EnemyCar> enemyCars;
    List<Bullet> bullets;

    float skySpeed = 0.3f;
    float skyOffset = -30.0f;

    float EaseIn(float a, float b, float frac)
    {
        return a + (b - a) * Mathf.Pow(frac, 2.0f);
    }

    float EaseOut(float a, float b, float frac)
    {
        return a + (b - a) * (1 - Mathf.Pow(1 - frac, 2.0f));
    }

    float EaseInOut(float a, float b, float frac)
    {
        return a + (b - a) * ((-Mathf.Cos(frac * Mathf.PI) / 2.0f) + 0.5f);
    }

    float Accelerate(float curSpeed, float acceleration, float dt)
    {
        return curSpeed += acceleration * dt;
    }

    float Clamp(float val, float min, float max)
    {
        return Mathf.Min(Mathf.Max(val,min), max);
    }

    float LastElev()
    {
        if (segments.Count == 0)
        {
            return 0.0f;
        }
        return segments[segments.Count - 1].worldPosition2.z;
    }
    
    void AddSegment(float curve, float elev)
    {
        int n = segments.Count;

        Segment newSegment = new Segment();

        newSegment.index = n;
        newSegment.worldPosition1.y = n * segmentLength;
        newSegment.worldPosition1.z = LastElev();
        newSegment.worldPosition2.y = (n+1) * segmentLength;
        newSegment.worldPosition2.z = elev;

        newSegment.color = (((n / RUMBLE_LENGTH) % 2) == 1) ? SegmentColor.Dark : SegmentColor.Light;
        newSegment.curve = curve;
        segments.Add(newSegment);
    }

    void AddFlatTurn(int enter, int hold, int leave, float curve, float elev)
    {
        for (int i = 0; i < enter; ++i)
        {
            AddSegment(EaseIn(0.0f, curve, i / (float)enter), elev);
        }

        for (int i = 0; i < hold; ++i)
        {
            AddSegment(curve, elev);
        }

        for (int i = 0; i < leave; ++i)
        {
            AddSegment(EaseOut(curve, 0.0f, i / (float)leave), elev);
        }
    }

    void AddSlopedTurn(int enter, int hold, int leave, float curve, float elev)
    {
        float firstElev = LastElev();
        float totalSegments = enter+hold+leave;
        
        float secondElev = (enter/(float)totalSegments) * (elev-firstElev) + firstElev;
        float thirdElev = ((enter+hold)/(float)totalSegments) * (elev-firstElev) + firstElev;
            
        for (int i = 0; i < enter; ++i)
        {
            AddSegment(EaseIn(0.0f, curve, i / (float)enter),
                       EaseInOut(firstElev,secondElev, i/(float)enter));
        }

        for (int i = 0; i < hold; ++i)
        {
            AddSegment(curve,
                       EaseInOut(secondElev, thirdElev, i/(float)hold));
        }

        for (int i = 0; i < leave; ++i)
        {
            AddSegment(EaseOut(curve, 0.0f, i / (float)leave),
                       EaseInOut(thirdElev, elev, i/(float)leave));
        }
    }

    void AddStraightHill(int enter, int hold, int leave, float elev)
    {
        float lastElev = LastElev();
        
        for (int i = 0; i < enter; ++i)
        {
            AddSegment(0, EaseInOut(lastElev, elev, i / (float)enter));
        }
        for (int i = 0; i < hold; ++i)
        {
            AddSegment(0, elev);
        }
        for (int i = 0; i < leave; ++i)
        {
            AddSegment(0, EaseInOut(elev, lastElev, i/ (float)leave));
        }
    }

    void AddSlope(int length, float exitElev)
    {
        float startElev = LastElev();
        for (int i = 0; i < length; ++i)
        {
            AddSegment(0, EaseInOut(startElev, exitElev, i/(float)length));
        }
    }

    void AddLeftCurve()
    {
        AddFlatTurn(ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    CURVE_EASY,
                    HILL_NONE);  // easy left
    }
    
    void AddLeftHairpin()
    {
        AddFlatTurn(ROADLENGTH_MEDIUM,
                    ROADLENGTH_SHORT,
                    ROADLENGTH_MEDIUM,
                    CURVE_HARD,
                    HILL_NONE);  // easy right        
    }
    void AddRightCurve()
    {
        AddFlatTurn(ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    -CURVE_EASY,
                    HILL_NONE);  // easy right
    }

    void AddRightHairpin()
    {
        AddFlatTurn(ROADLENGTH_MEDIUM,
                    ROADLENGTH_SHORT,
                    ROADLENGTH_MEDIUM,
                    -CURVE_HARD,
                    HILL_NONE);  // easy right        
    }

    void AddSCurves()
    {
        AddFlatTurn(ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    -CURVE_EASY,
                    HILL_NONE);  // easy left
        
        AddFlatTurn(ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    CURVE_MEDIUM,
                    HILL_NONE); // medium right
        
        AddFlatTurn(ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    CURVE_EASY,
                    HILL_NONE); // easy right
        
        AddFlatTurn(ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    -CURVE_EASY,
                    HILL_NONE); // easy left
        
        AddFlatTurn(ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    -CURVE_MEDIUM,
                    HILL_NONE); // medium left
    }

    void AddStraight(int numSegments, float elev)
    {
        for (int i = 0; i < numSegments; ++i)
        {
            AddSegment(0, elev);
        }
    }

    void AddSprite(Texture2D sprite, float xOffset, int segmentIndex, int treeIndex)
    {
        Segment segment = segments[segmentIndex];
        if (segment.sprites == null)
        {
            segment.sprites = new List<SpriteContainer>();
        }
        SpriteContainer sc = new SpriteContainer();
        sc.sprite = sprite;
        sc.xOffset = xOffset;
        sc.treeIndex = treeIndex;
        segment.sprites.Add(sc);
    }

    void AddCar(float xOffset, float trackPosition, int carSpriteIndex, float speed)
    {
        EnemyCar car = new EnemyCar();
        car.xOffset = xOffset;
        car.trackPosition = trackPosition;
        car.carSpriteIndex = carSpriteIndex;
        car.speed = speed;
        car.hit = false;

        enemyCars.Add(car);
        AddCarToCurrentSegment(car);
    }

    void AddBullet(float xOffset, float trackPosition, int particleIndex, float speed, float distanceRemaining)
    {
        Bullet bullet = new Bullet();
        bullet.xOffset = xOffset;
        bullet.trackPosition = trackPosition;
        bullet.particleIndex = particleIndex;
        bullet.distanceRemaining = distanceRemaining;
        bullet.speed = speed;

        bullets.Add(bullet);
        Segment segment = FindSegment(trackPosition);
        AddBulletToSegment(bullet, segment);
    }

    void AddParticle(float x, float y, float vx, float vy, int colorIndex, float timeToLive, Segment segment)
    {
        Particle particle = new Particle();
        particle.position = new Vector2(x,y);
        particle.velocity = new Vector2(vx, vy);
        particle.colorIndex = colorIndex;
        particle.secondsRemaining = timeToLive;

        if (segment.particles == null)
        {
            segment.particles = new List<Particle>();
        }
        segment.particles.Add(particle);
    }

    void AddExplosion(float x, float y, Segment segment)
    {
        int numParticles = 400;
        float baseSpeed = 2.0f;

        int[] explosionColors = {0,1,3,5,7,9,10,11,13,15};

        for (int i = 0; i < numParticles; ++i)
        {
            //int color = Mathf.FloorToInt(Random.value * 16);
            int color = explosionColors[Mathf.FloorToInt(Random.value *explosionColors.Length)];
            float speed = Random.value * baseSpeed;
            float angle = Random.value*Mathf.PI; // only the top 180 degrees
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            AddParticle(x, y, cos*speed, sin*speed, color, 1.5f, segment);
            //Debug.Log(string.Format("added particle at {0} {1}", x, y));
        }
    }

    void RemoveCarFromSegment(EnemyCar car, Segment segment)
    {
        if ((segment != null) && (segment.cars != null))
        {
            segment.cars.Remove(car);
        }
    }

    void AddCarToCurrentSegment(EnemyCar car)
    {
        Segment segment = FindSegment(car.trackPosition);
        if (segment != null)
        {
            if (segment.cars == null)
            {
                segment.cars = new List<EnemyCar>();
            }
            segment.cars.Add(car);
        }
    }

    void RemoveBulletFromSegment(Bullet bullet, Segment segment)
    {
        if ((segment != null) && (segment.bullets != null))
        {
            segment.bullets.Remove(bullet);
        }
    }

    void AddBulletToSegment(Bullet bullet, Segment segment)
    {
        if (segment.bullets == null)
        {
            segment.bullets = new List<Bullet>();
        }
        segment.bullets.Add(bullet);
    }

    void UpdateCars()
    {
        List<EnemyCar> deadCars = new List<EnemyCar>();
        
        foreach (EnemyCar car in enemyCars)
        {
            Segment oldSegment = FindSegment(car.trackPosition);
            // could update x

            if (car.hit)
            {
                deadCars.Add(car);
                oldSegment.cars.Remove(car);
                continue;
            }

            // predict if we'll overtake the player
            if (car.speed > speed)
            {
                // track position of player relative to us
                float playerPos = trackPosition - car.trackPosition;
                while (playerPos < - trackLength / 2.0f)
                {
                    playerPos += trackLength;
                }
                while (playerPos > trackLength / 2.0f)
                {
                    playerPos -= trackLength;
                }

                if ((playerPos > 0) && (playerPos < trackLength * 0.1f))
                {
                    // correct position to avoid unpleasant surprises
                    car.xOffset = worldPlayerPosition + 1.0f;
                    if (car.xOffset > 1.0f)
                    {
                        car.xOffset -= 2.0f;
                    }
                }
            }
            
            car.trackPosition += car.speed * Time.deltaTime;
            car.trackPosition %= trackLength;

            // TODO detect collision with player car.
            
            float distanceFromStartOfSegment = car.trackPosition % segmentLength;
            car.segmentFrac = distanceFromStartOfSegment / segmentLength;

            Segment newSegment = FindSegment(car.trackPosition);

            if (newSegment != oldSegment)
            {
                RemoveCarFromSegment(car, oldSegment);
                AddCarToCurrentSegment(car);
            }
        }

        foreach (EnemyCar car in deadCars)
        {
            enemyCars.Remove(car);
        }
    }

    void UpdateBullets()
    {
        List<Bullet> deadBullets = new List<Bullet>();
        
        foreach (Bullet bullet in bullets)
        {
            Segment oldSegment = FindSegment(bullet.trackPosition);

            float distanceTravelledThisTick = bullet.speed * Time.deltaTime;
            
            bullet.trackPosition += distanceTravelledThisTick;
            bullet.trackPosition %= trackLength;

            bullet.distanceRemaining -= distanceTravelledThisTick;
            if (bullet.distanceRemaining <= 0)
            {
                //Debug.Log("expiring bullet");
                RemoveBulletFromSegment(bullet, oldSegment);
                deadBullets.Add(bullet);
                continue;
            }

            float distanceFromStartOfSegment = bullet.trackPosition % segmentLength;
            bullet.segmentFrac = distanceFromStartOfSegment / segmentLength;

            Segment newSegment = FindSegment(bullet.trackPosition);

            float HitRadius = 0.25f;
            bool hitCar=false;
            if (newSegment.cars != null)
            {
                foreach (EnemyCar car in newSegment.cars)
                {
                    //Debug.Log("Testing bullet/car");

                    float separation = Mathf.Abs(car.xOffset - bullet.xOffset);

                    //Debug.Log("separation: " + separation);

                    if (separation < HitRadius)
                    {
                        car.hit = true;
                        deadBullets.Add(bullet);
                        hitCar = true;
                        AddExplosion(car.xOffset, 0.0f, newSegment);
                        break;
                    }
                }
            }

            if (!hitCar)
            {
                if (newSegment != oldSegment)
                {
                    RemoveBulletFromSegment(bullet, oldSegment);
                    AddBulletToSegment(bullet, newSegment);
                }
            }
            else
            {
                RemoveBulletFromSegment(bullet, oldSegment);
            }
        }

        foreach (Bullet bullet in deadBullets)
        {
            bullets.Remove(bullet);
        }
    }

    void UpdateParticles()
    {
        float accelerationY = -1.0f;
        
        List<Particle> deadParticles = new List<Particle>();
        foreach (Segment segment in segments)
        {
            deadParticles.Clear();
            if (segment.particles == null)
            {
                continue;
            }
            foreach (Particle particle in segment.particles)
            {
                //Debug.Log("updating particle " + particle);
                particle.secondsRemaining -= Time.deltaTime;

                particle.velocity.y += accelerationY * Time.deltaTime;
                particle.position += particle.velocity * Time.deltaTime;

                if ((particle.secondsRemaining < 0) ||
                    (particle.position.y < 0))
                {
                    deadParticles.Add(particle);
                }
            }
        }
    }

    void DetectPlayerCarCollision()
    {
        float hitRadius = 0.3f;
        float playerCarTrackPosition = trackPosition + carVisualOffset;
        Segment segment = FindSegment(playerCarTrackPosition);
        if (segment.cars != null)
        {
            foreach (EnemyCar enemyCar in segment.cars)
            {
                float separation = Mathf.Abs(enemyCar.xOffset - worldPlayerPosition);
                if (separation < hitRadius)
                {
                    enemyCar.hit = true;
                    AddExplosion(enemyCar.xOffset, 0.0f, segment);
                    ApplyDamage();
                }
            }
        }
    }

    void ApplyDamage()
    {
        playerHitPoints -= 1;
        Debug.Log("New Hit Points: "+ playerHitPoints);
        if (playerHitPoints <= 0)
        {
            BlowUpPlayer();
        }
        speed = 0.0f;
    }

    void BlowUpPlayer()
    {
        float playerCarTrackPosition = trackPosition + carVisualOffset;
        Segment segment = FindSegment(playerCarTrackPosition);
        AddExplosion(worldPlayerPosition - 0.3f, 0.0f, segment);
        AddExplosion(worldPlayerPosition + 0.3f, 0.0f, segment);
        AddExplosion(worldPlayerPosition, 0.2f, segment);
    }

    bool PlayerIsAlive()
    {
        return playerHitPoints > 0;
    }

    int randomTreeIndex()
    {
        return Mathf.FloorToInt(Random.value * 5);
    }

    void MakeRoadManchester()
    {
        backgroundIndex = 0;
        legCarCount = 6;
        lapsNeeded = 3;
        
        AddRightCurve(); // 0-150
        AddLeftCurve(); // 150-300
        AddStraight(ROADLENGTH_SHORT, HILL_NONE); // 300-325
        AddStraightHill(ROADLENGTH_LONG, ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, HILL_MEDIUM); // 325-525
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE); // 525-575
        
        AddStraightHill(ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, HILL_HIGH); // 575-725
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE); // 725-775

        AddLeftCurve(); // 775 - 925
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE); // 925-975
        AddSCurves(); // 975 - 1725
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE); // 1725 - 1775

        for (int s = 30; s < 150; s += 4)
        {
            float r = Random.value * .4f + 1.6f;
            AddSprite(null, r, s, randomTreeIndex());
        }

        for (int s = 160; s < 300; s += 4)
        {
            float r = - (Random.value * .6f + 1.8f);
            AddSprite(null, r, s, randomTreeIndex());
        }

        for (int s = 525; s < 575; s += 4)
        {
            float r = Random.value * .4f + 1.3f;
            AddSprite(null, r, s, randomTreeIndex());

            r = - (Random.value * .4f + 1.8f);
            AddSprite(null, r, s, randomTreeIndex());
        }

        for (int s = 975; s < 1725; s += 4)
        {
            float coin = Random.value;
            if (coin > 0.5f)
            {
                float r = Random.value * .6f + 1.3f;
                AddSprite(null, r, s, randomTreeIndex());
            }
            else
            {
                float r = - (Random.value * .6f + 1.8f);
                AddSprite(null, r, s, randomTreeIndex());
            }
        }

        for (int s = 775; s < 925; s += 4)
        {
            float r = Random.value * .6f + 1.3f;
            AddSprite(null, r, s, randomTreeIndex());

            r = - (Random.value * .6f + 1.8f);
            AddSprite(null, r, s, randomTreeIndex());
        }
    }

    void MakeRoadBoston()
    {
        Debug.Log("Making Boston");
        backgroundIndex = 1;
        legCarCount = 8;
        lapsNeeded = 5;

        // Fuji Raceway:
        // https://upload.wikimedia.org/wikipedia/commons/2/2c/Fuji.svg
        // and
        // https://www.youtube.com/watch?v=sojfa9vMOyw

        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddSprite(null, 2.0f, 1, randomTreeIndex());
        AddSprite(null, -2.0f, 1, randomTreeIndex());

        for (int i = 25; i < segments.Count; i +=3)
        {
            float r = -(Random.value + 1.5f);
            AddSprite(null, r, i, randomTreeIndex());
        }
        AddRightHairpin(); // 1st hairpin

        int segCheckPoint = segments.Count;
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);

        for (int i = segCheckPoint; i < segments.Count; i += 4)
        {
            float r = Random.value + 1.5f;
            AddSprite(null, r, i, randomTreeIndex());            
        }
        AddLeftCurve(); // "coca cola"
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddRightCurve(); // 100R
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddLeftHairpin(); // Hairpin
        AddStraightHill(ROADLENGTH_SHORT, ROADLENGTH_SHORT, ROADLENGTH_SHORT, HILL_LOW);

        AddFlatTurn(ROADLENGTH_LONG, // 300R - Dunlop
                    ROADLENGTH_LONG,
                    ROADLENGTH_LONG,
                    CURVE_EASY,
                    HILL_NONE);  // easy right        

        segCheckPoint = segments.Count;
        AddStraight(ROADLENGTH_SHORT, HILL_NONE);
        AddRightCurve(); // Panasonic
        for (int i = segCheckPoint; i < segments.Count; i += 4)
        {
            float r = Random.value + 1.5f;
            AddSprite(null, r, i, randomTreeIndex());
            i += 3;
            r = -(Random.value + 1.5f);
            AddSprite(null, r, i, randomTreeIndex());            
        }
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddStraightHill(ROADLENGTH_LONG, ROADLENGTH_LONG, ROADLENGTH_LONG, HILL_MEDIUM);
    }

    void MakeRoadAlbany()
    {
        backgroundIndex = 2;
        legCarCount = 8;
        lapsNeeded = 5;
        
        AddStraight(ROADLENGTH_LONG, HILL_NONE);
        AddRightCurve();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddRightHairpin();

        AddRightCurve();
        AddStraight(ROADLENGTH_SHORT, HILL_NONE);
        AddLeftCurve();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddSCurves();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddLeftCurve();
        AddStraightHill(ROADLENGTH_LONG, ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, HILL_MEDIUM);
        AddLeftCurve();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddLeftCurve();        
        AddStraightHill(ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, HILL_HIGH);
        AddLeftCurve();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);

        for (int s = 30; s < 150; s += 4)
        {
            float r = Random.value * .4f + 1.6f;
            AddSprite(null, r, s, randomTreeIndex());
        }

        for (int s = 160; s < 300; s += 4)
        {
            float r = - (Random.value * .6f + 1.8f);
            AddSprite(null, r, s, randomTreeIndex());
        }

        for (int s = 525; s < 575; s += 4)
        {
            float r = Random.value * .4f + 1.3f;
            AddSprite(null, r, s, randomTreeIndex());

            r = - (Random.value * .4f + 1.8f);
            AddSprite(null, r, s, randomTreeIndex());
        }

        for (int s = 975; s < 1725; s += 4)
        {
            float coin = Random.value;
            if (coin > 0.5f)
            {
                float r = Random.value * .6f + 1.3f;
                AddSprite(null, r, s, randomTreeIndex());
            }
            else
            {
                float r = - (Random.value * .6f + 1.8f);
                AddSprite(null, r, s, randomTreeIndex());
            }
        }

        for (int s = 775; s < 925; s += 4)
        {
            float r = Random.value * .6f + 1.3f;
            AddSprite(null, r, s, randomTreeIndex());

            r = - (Random.value * .6f + 1.8f);
            AddSprite(null, r, s, randomTreeIndex());
        }
        
    }
    
    void MakeRoadNewYork()
    {
        Debug.Log("Starting NYC");
        backgroundIndex = 3;
        legCarCount = 8;
        lapsNeeded = 5;

        //https://en.wikipedia.org/wiki/Top_Gear_test_track#/media/File:TG_Test_Track.PNG        
        AddStraight(ROADLENGTH_LONG, HILL_NONE);
        int segCheckPoint = segments.Count;
        AddFlatTurn(ROADLENGTH_LONG,
                    ROADLENGTH_SHORT,
                    ROADLENGTH_LONG,
                    -CURVE_EASY,
                    HILL_NONE); // Crooner
        AddStraight(ROADLENGTH_SHORT, HILL_NONE);
        for (int i= segCheckPoint; i < segments.Count; i+=4)
        {
            float r = -(Random.value + 1.5f);
            AddSprite(null, r, i, randomTreeIndex());            
        }
        AddFlatTurn(ROADLENGTH_SHORT,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_LONG,
                    CURVE_MEDIUM,
                    HILL_NONE); // Wilson
        AddStraight(ROADLENGTH_SHORT, HILL_NONE);
        segCheckPoint = segments.Count;
        AddFlatTurn(ROADLENGTH_SHORT,
                    ROADLENGTH_SHORT,
                    ROADLENGTH_SHORT,
                    CURVE_MEDIUM,
                    HILL_NONE); // Chicago 1
        AddFlatTurn(ROADLENGTH_SHORT,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_SHORT,
                    -CURVE_MEDIUM,
                    HILL_NONE); // Chicago 2
        AddFlatTurn(ROADLENGTH_SHORT,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_LONG,
                    -CURVE_EASY,
                    HILL_NONE); // Chicago 3
        for (int i= segCheckPoint; i < segments.Count; i+=4)
        {
            float r = (Random.value + 1.5f);
            AddSprite(null, r, i, randomTreeIndex());            
        }
        AddStraight(ROADLENGTH_SHORT, HILL_NONE);
        AddStraightHill(ROADLENGTH_MEDIUM, ROADLENGTH_SHORT, ROADLENGTH_MEDIUM, HILL_MEDIUM);
        
        AddFlatTurn(ROADLENGTH_MEDIUM,
                    ROADLENGTH_SHORT,
                    ROADLENGTH_SHORT,
                    CURVE_HARD,
                    HILL_NONE); // Hammerhead 1
        AddFlatTurn(ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    -CURVE_MEDIUM,
                    HILL_NONE); // Hammerhead 2
        AddFlatTurn(ROADLENGTH_MEDIUM,
                    ROADLENGTH_LONG,
                    ROADLENGTH_LONG,
                    -CURVE_EASY,
                    HILL_NONE); // Hammerhead 3

        AddStraight(ROADLENGTH_SHORT, HILL_NONE);
        AddStraightHill(ROADLENGTH_LONG, ROADLENGTH_MEDIUM, ROADLENGTH_SHORT, HILL_LOW);

        segCheckPoint = segments.Count;
        AddFlatTurn(ROADLENGTH_MEDIUM,
                    ROADLENGTH_LONG,
                    ROADLENGTH_LONG,
                    -CURVE_EASY,
                    HILL_NONE); // Follow-through
        for (int i= segCheckPoint; i < segments.Count; i+=7)
        {
            float r = Random.value + 1.5f;
            AddSprite(null, r, i, randomTreeIndex());            
        }
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        
        AddFlatTurn(ROADLENGTH_SHORT,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_LONG,
                    CURVE_EASY,
                    HILL_NONE); // Bentley
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);

        AddFlatTurn(ROADLENGTH_LONG,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    CURVE_MEDIUM,
                    HILL_NONE); // Second to last turn (Bacharach)
        segCheckPoint = segments.Count;
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        for (int i= segCheckPoint; i < segments.Count; i+=4)
        {
            float r = -(Random.value + 1.5f);
            AddSprite(null, r, i, randomTreeIndex());
            AddSprite(null, -r, i, randomTreeIndex());
        }

        AddFlatTurn(ROADLENGTH_SHORT,
                    ROADLENGTH_SHORT,
                    ROADLENGTH_SHORT,
                    CURVE_HARD,
                    HILL_NONE); // Gambon

        AddSprite(null, 2.0f, 1, randomTreeIndex());
        AddSprite(null, -2.0f, 1, randomTreeIndex());        
    }
    
    void MakeRoadPhiladelphia()
    {
        backgroundIndex = 4;
        legCarCount = 10;
        lapsNeeded = 6;
        
        AddStraight(ROADLENGTH_LONG, HILL_NONE);
        AddRightCurve();
        AddStraight(ROADLENGTH_LONG, HILL_NONE);
        AddRightCurve();
        AddStraight(ROADLENGTH_SHORT, HILL_NONE);
        AddRightHairpin();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddLeftCurve();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddLeftCurve();        
        AddStraightHill(ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, HILL_HIGH);
        AddSCurves();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddRightHairpin();
        AddStraightHill(ROADLENGTH_LONG, ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, HILL_MEDIUM);
        AddLeftCurve();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddRightCurve();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);

        for (int s = 30; s < 150; s += 4)
        {
            float r = Random.value * .4f + 1.6f;
            AddSprite(null, r, s, randomTreeIndex());
        }

        for (int s = 160; s < 300; s += 4)
        {
            float r = - (Random.value * .6f + 1.8f);
            AddSprite(null, r, s, randomTreeIndex());
        }

        for (int s = 525; s < 575; s += 4)
        {
            float r = Random.value * .4f + 1.3f;
            AddSprite(null, r, s, randomTreeIndex());

            r = - (Random.value * .4f + 1.8f);
            AddSprite(null, r, s, randomTreeIndex());
        }

        for (int s = 975; s < 1725; s += 4)
        {
            float coin = Random.value;
            if (coin > 0.5f)
            {
                float r = Random.value * .6f + 1.3f;
                AddSprite(null, r, s, randomTreeIndex());
            }
            else
            {
                float r = - (Random.value * .6f + 1.8f);
                AddSprite(null, r, s, randomTreeIndex());
            }
        }

        for (int s = 775; s < 925; s += 4)
        {
            float r = Random.value * .6f + 1.3f;
            AddSprite(null, r, s, randomTreeIndex());

            r = - (Random.value * .6f + 1.8f);
            AddSprite(null, r, s, randomTreeIndex());
        }
    }
    
    void MakeRoadHarrisburg()
    {
        backgroundIndex = 5;
        legCarCount = 10;
        lapsNeeded = 6;
        AddStraight(ROADLENGTH_LONG, HILL_NONE);
        AddLeftCurve();        
        AddStraightHill(ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, HILL_HIGH);
        AddSCurves();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddRightHairpin();
        AddStraightHill(ROADLENGTH_LONG, ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, HILL_MEDIUM);
        AddRightCurve();
        AddStraight(ROADLENGTH_LONG, HILL_NONE);
        AddRightCurve();
        AddStraight(ROADLENGTH_LONG, HILL_NONE);
        AddRightCurve();
        AddStraight(ROADLENGTH_SHORT, HILL_NONE);
        AddRightHairpin();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddLeftCurve();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddLeftCurve();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddRightCurve();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);

        for (int s = 30; s < 150; s += 4)
        {
            float r = Random.value * .4f + 1.6f;
            AddSprite(null, r, s, randomTreeIndex());
        }

        for (int s = 160; s < 300; s += 4)
        {
            float r = - (Random.value * .6f + 1.8f);
            AddSprite(null, r, s, randomTreeIndex());
        }

        for (int s = 525; s < 575; s += 4)
        {
            float r = Random.value * .4f + 1.3f;
            AddSprite(null, r, s, randomTreeIndex());

            r = - (Random.value * .4f + 1.8f);
            AddSprite(null, r, s, randomTreeIndex());
        }

        for (int s = 975; s < 1725; s += 4)
        {
            float coin = Random.value;
            if (coin > 0.5f)
            {
                float r = Random.value * .6f + 1.3f;
                AddSprite(null, r, s, randomTreeIndex());
            }
            else
            {
                float r = - (Random.value * .6f + 1.8f);
                AddSprite(null, r, s, randomTreeIndex());
            }
        }

        for (int s = 775; s < 925; s += 4)
        {
            float r = Random.value * .6f + 1.3f;
            AddSprite(null, r, s, randomTreeIndex());

            r = - (Random.value * .6f + 1.8f);
            AddSprite(null, r, s, randomTreeIndex());
        }
    }
    
    void MakeRoadBaltimore()
    {
        backgroundIndex = 6;
        legCarCount = 12;
        lapsNeeded = 7;
        AddStraight(ROADLENGTH_LONG, HILL_NONE);
        AddStraight(ROADLENGTH_LONG, HILL_NONE);
        AddRightCurve();
        AddStraight(ROADLENGTH_LONG, HILL_NONE);
        AddRightHairpin();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddLeftCurve();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddLeftCurve();        
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddRightHairpin();
        AddStraightHill(ROADLENGTH_LONG, ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, HILL_MEDIUM);
        AddLeftCurve();
        AddStraightHill(ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, HILL_HIGH);
        AddSCurves();
        AddRightCurve();
        AddStraight(ROADLENGTH_SHORT, HILL_NONE);
        AddLeftHairpin();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddRightCurve();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);

        for (int s = 30; s < 150; s += 4)
        {
            float r = Random.value * .4f + 1.6f;
            AddSprite(null, r, s, randomTreeIndex());
        }

        for (int s = 160; s < 300; s += 4)
        {
            float r = - (Random.value * .6f + 1.8f);
            AddSprite(null, r, s, randomTreeIndex());
        }

        for (int s = 525; s < 575; s += 4)
        {
            float r = Random.value * .4f + 1.3f;
            AddSprite(null, r, s, randomTreeIndex());

            r = - (Random.value * .4f + 1.8f);
            AddSprite(null, r, s, randomTreeIndex());
        }

        for (int s = 975; s < 1725; s += 4)
        {
            float coin = Random.value;
            if (coin > 0.5f)
            {
                float r = Random.value * .6f + 1.3f;
                AddSprite(null, r, s, randomTreeIndex());
            }
            else
            {
                float r = - (Random.value * .6f + 1.8f);
                AddSprite(null, r, s, randomTreeIndex());
            }
        }

        for (int s = 775; s < 925; s += 4)
        {
            float r = Random.value * .6f + 1.3f;
            AddSprite(null, r, s, randomTreeIndex());

            r = - (Random.value * .6f + 1.8f);
            AddSprite(null, r, s, randomTreeIndex());
        }
    }
    
    void MakeRoadWashingtonDC()
    {
        backgroundIndex = 7;
        legCarCount = 16;
        lapsNeeded = 10;

        // Laguna Seca
        //https://upload.wikimedia.org/wikipedia/commons/thumb/5/57/Laguna_Seca.svg/2000px-Laguna_Seca.svg.png        
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);

        AddFlatTurn(ROADLENGTH_SHORT,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_SHORT,
                    CURVE_EASY,
                    HILL_NONE); // Turn 1

        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        
        AddFlatTurn(ROADLENGTH_SHORT,
                    ROADLENGTH_SHORT,
                    ROADLENGTH_SHORT,
                    CURVE_MEDIUM,
                    HILL_NONE); // Andretti Hairpin 1
        AddFlatTurn(ROADLENGTH_SHORT,
                    ROADLENGTH_SHORT,
                    ROADLENGTH_SHORT,
                    CURVE_MEDIUM,
                    HILL_NONE); // Andretti Hairpin 2

        int segCheckPoint = segments.Count;
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        for (int i = segCheckPoint; i < segments.Count; i+= 7)
        {
            float r = Random.value + 1.5f;
            AddSprite(null, -r, i, randomTreeIndex());                        
        }

        AddRightCurve();  // Turn 3
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);

        AddRightCurve();  // Turn 4
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddFlatTurn(ROADLENGTH_SHORT,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_SHORT,
                    -CURVE_EASY,
                    HILL_NONE); // Gentle turn
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddFlatTurn(ROADLENGTH_SHORT,
                    ROADLENGTH_LONG,
                    ROADLENGTH_SHORT,
                    CURVE_MEDIUM,
                    HILL_NONE); // Turn 5

        segCheckPoint = segments.Count;
        AddSlope(ROADLENGTH_MEDIUM, HILL_LOW);
        for (int i = segCheckPoint; i < segments.Count; i+= 7)
        {
            float r = Random.value + 1.5f;
            AddSprite(null, r, i, randomTreeIndex());                        
        }
        AddFlatTurn(ROADLENGTH_SHORT,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_LONG,
                    CURVE_MEDIUM,
                    HILL_LOW); // Turn 6
        AddSlope(ROADLENGTH_LONG, HILL_HIGH);

        // and now the corkscrew

        segCheckPoint = segments.Count;
        AddSlopedTurn(ROADLENGTH_MEDIUM,
                      ROADLENGTH_MEDIUM,
                      ROADLENGTH_MEDIUM,
                      -CURVE_EASY,
                      HILL_MEDIUM); // Turn 7
        AddSlopedTurn(ROADLENGTH_MEDIUM,
                      ROADLENGTH_MEDIUM,
                      ROADLENGTH_MEDIUM,
                      CURVE_HARD,
                      HILL_LOW); // Turn 8
        AddSlopedTurn(ROADLENGTH_MEDIUM,
                      ROADLENGTH_MEDIUM,
                      ROADLENGTH_MEDIUM,
                      -CURVE_HARD,
                      HILL_NONE); // Turn 8a
        for (int i = segCheckPoint; i < segments.Count; i+= 7)
        {
            float r = Random.value + 1.5f;
            AddSprite(null, -r, i, randomTreeIndex());                        
            AddSprite(null, r, i, randomTreeIndex());                        
        }

        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddLeftCurve(); // Rainey Curve
        segCheckPoint = segments.Count;
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddRightCurve(); // Curve 10
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        for (int i = segCheckPoint; i < segments.Count; i+= 7)
        {
            float r = Random.value + 1.5f;
            AddSprite(null, r, i, randomTreeIndex());                        
        }
        AddFlatTurn(ROADLENGTH_SHORT,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    CURVE_MEDIUM,
                    HILL_NONE); // Curve 11
        AddStraight(ROADLENGTH_LONG, HILL_NONE);
        
        AddSprite(null, 2.0f, 1, randomTreeIndex());
        AddSprite(null, -2.0f, 1, randomTreeIndex());        
    }

    void ResetRoad(LegIdentifier legID)
    {
        Debug.Log("ResetRoad: " + legID);
        segments = new List<Segment>();
        enemyCars = new List<EnemyCar>();
        bullets = new List<Bullet>();

        switch(legID)
        {
            case LegIdentifier.Manchester:
                MakeRoadManchester();
                break;
            case LegIdentifier.Boston:
                MakeRoadBoston();
                break;
            case LegIdentifier.Albany:
                MakeRoadAlbany();
                break;
            case LegIdentifier.NewYork:
                MakeRoadNewYork();
                break;
            case LegIdentifier.Philadelphia:
                MakeRoadPhiladelphia();
                break;
            case LegIdentifier.Harrisburg:
                MakeRoadHarrisburg();
                break;
            case LegIdentifier.Baltimore:
                MakeRoadBaltimore();
                break;
            case LegIdentifier.WashingtonDC:
                MakeRoadWashingtonDC();
                break;
        }                
        
        trackLength = segments.Count * segmentLength;
        Debug.Log("Track Length: " + trackLength);

        for (int i = 0; i < legCarCount; ++i)
        {
            float stepSize = trackLength / legCarCount;
            int spriteIndex = i % 12;
            float pos = (i*stepSize)+75.0f;
            float maxSpeed = 30.0f;
            float minSpeed = 5.0f;
            float speed = (maxSpeed - minSpeed) * Random.value + minSpeed;

            float x = Random.value * 2.0f - 1.0f;
            AddCar(x, pos, spriteIndex, speed);
        }
        Debug.Log("Done setting up leg");
    }

    void IncrementLeg()
    {
        Debug.Log("Incrementing from " + currentLeg);
        if (currentLeg != LegIdentifier.WashingtonDC)
        {
            currentLeg += 1;
            Debug.Log("Now " + currentLeg);
            StartLeg(currentLeg);
        }
        else
        {
            Debug.Log("Winner!");
            timeInAttract = 0.0f;
            winnerScreen = true;
        }
    }

    Segment FindSegment(float trackDist)
    {
        return segments[Mathf.FloorToInt(trackDist / segmentLength) % segments.Count];
    }

    void DrawBackground(RoadRenderer r, float offset)
    {
        int pxOffset = (int)(64*offset);

        Texture2D bkgd = r.backgrounds[backgroundIndex];
        
        while (pxOffset > 0) {
            pxOffset -= bkgd.width;
        }

        while (true)
        {
            r.drawSprite(bkgd, pxOffset, 0, false, false);
            if (pxOffset + bkgd.width >= 64)
            {
                return;
            }
            pxOffset += bkgd.width;
        }
    }

    void DrawPlayerCar(RoadRenderer r)
    {
        float fakeWidth = 50.0f;
        
        int carPosition = 32 // center of screen
            + Mathf.RoundToInt(worldPlayerPosition * fakeWidth);
        r.drawSprite(r.cars[0], carPosition, 5, true, false);
    }

    Color32 fade(Color32 c1, Color32 c2, float fadeFrac)
    {
        Color32 o = new Color32();
        o.r = (byte)(c1.r + (c2.r-c1.r) * fadeFrac);
        o.g = (byte)(c1.g + (c2.g-c1.g) * fadeFrac);
        o.b = (byte)(c1.b + (c2.b-c1.b) * fadeFrac);
        o.a = (byte)(c1.a + (c2.a-c1.a) * fadeFrac);
        return o;
    }
    
    void DrawSegment(RoadRenderer r, Vector3 screenPos1, Vector3 screenPos2, SegmentColor color, int horizon, float distanceFade)
    {
        int start = Mathf.FloorToInt(screenPos1.y);
        int end = Mathf.CeilToInt(screenPos2.y);

        int roadColor = ((color == SegmentColor.Dark) ? DARK_ROAD_COLOR : LIGHT_ROAD_COLOR);
        int rumbleColor = ((color == SegmentColor.Dark) ? DARK_RUMBLE_COLOR : LIGHT_RUMBLE_COLOR);
        int grassColor = ((color == SegmentColor.Dark) ? DARK_GRASS_COLOR : LIGHT_GRASS_COLOR);

        Color32 roadColorRGB = r.palette[roadColor];
        Color32 roadFadeColorRGB = new Color32(128, 128, 128, 255);

        Color32 rumbleColorRGB = r.palette[rumbleColor];
        Color32 rumbleFadeColorRGB = new Color32(255, 128, 128, 255);

        Color32 grassColorRGB = r.palette[grassColor];
        Color32 grassFadeColorRGB = new Color32(128, 255, 128, 255);

        Color32 roadRGB = fade(roadColorRGB, roadFadeColorRGB, distanceFade);
        Color32 rumbleRGB = fade(rumbleColorRGB, rumbleFadeColorRGB, distanceFade);
        Color32 grassRGB = fade(grassColorRGB, grassFadeColorRGB, distanceFade);
        
        for (int y = start; y <= end; ++y)
        {
            if (y < 0 || y >=64)
            {
                continue;
            }
            float roadWidth = 2.4f;
            float frac = (y - screenPos1.y) / (screenPos2.y - screenPos1.y);
            float width = Interpolate(screenPos1.z, screenPos2.z, frac) * roadWidth;
            float x = Interpolate(screenPos1.x, screenPos2.x, frac);

            int roadLeft = Mathf.FloorToInt(x-width);
            int roadRight = Mathf.FloorToInt(x+width);

            float rumbleFrac = 0.3f;
            int rumbleLeft = Mathf.FloorToInt(x - width * (1.0f + rumbleFrac));
            int rumbleRight = Mathf.FloorToInt(x + width * (1.0f + rumbleFrac));

            r.Hlin32(roadLeft, roadRight, y, roadRGB);
            r.Hlin32(rumbleLeft, roadLeft-1, y, rumbleRGB);
            r.Hlin32(roadRight+1, rumbleRight, y, rumbleRGB);
            r.Hlin32(0, rumbleLeft-1, y, grassRGB);
            r.Hlin32(rumbleRight+1, 63, y, grassRGB);
        }
    }

    float FracRemaining(float val, float cycleLength)
    {
        return val % cycleLength;
    }

    float Interpolate(float a, float b, float frac)
    {
        return a+ (b-a) * frac;
    }

    void DrawRoad(RoadRenderer r, float trackPosition)
    {
        Segment baseSegment = FindSegment(trackPosition);
        float baseFrac = FracRemaining(trackPosition, segmentLength);
        float dx = - (baseSegment.curve * baseFrac);
        float x = 0;

        float trackElev = Interpolate(baseSegment.worldPosition1.z,
                                      baseSegment.worldPosition2.z,
                                      baseFrac);

        int drawDistance = 40;
        int horizon = 0; // for rolling hills, this can obscure
                            // later segments

        Vector3 cameraPos = new Vector3 (0,
                                         trackPosition,
                                         worldCameraHeight+trackElev);

        for (int n = 0; n < drawDistance; ++n)
        {
            float distanceFade = (float)n/drawDistance;
            
            Segment segment = segments[(baseSegment.index + n) % segments.Count];

            int numLooped = (baseSegment.index + n) / segments.Count;
        
            Vector3 worldPos1 = new Vector3(-x,
                                            segment.worldPosition1.y + numLooped * trackLength,
                                            segment.worldPosition1.z);
            segment.screenPosition1 = ProjectWorldToScreen(worldPos1, cameraPos);
            Vector3 worldPos2 = new Vector3(-x - dx,
                                            segment.worldPosition2.y + numLooped * trackLength,
                                            segment.worldPosition2.z);            
            segment.screenPosition2 = ProjectWorldToScreen(worldPos2, cameraPos);

            x += dx;
            dx += segment.curve;

            int firstY = Mathf.FloorToInt(segment.screenPosition1.y);
            int lastY = Mathf.CeilToInt(segment.screenPosition2.y);
            segment.clipHeight = Mathf.Max(firstY, horizon);

            if (lastY <= horizon)
            {
                continue;
            }

            DrawSegment(r, segment.screenPosition1, segment.screenPosition2, segment.color, horizon, distanceFade);
            horizon = lastY;
        }

        for (int n = drawDistance-1; n >= 0; --n)
        {
            Segment segment = segments[(baseSegment.index + n) % segments.Count];

            //Debug.Log("drawing segment "+ segment.index);
            
            float scale = segment.screenPosition1.z;
            float roadWidth = 2.4f; // duplicated hardcoded number, yay!
            float segmentX = segment.screenPosition1.x;
            float width = roadWidth * scale;
            int segmentY = Mathf.FloorToInt(segment.screenPosition1.y);
            float treeScale = 1.0f/9.0f;

            if (segment.sprites != null)
            {
                foreach (SpriteContainer sc in segment.sprites)
                {
                    r.drawSpriteScaledAndClipped(r.trees[sc.treeIndex], // hack!
                                                 Mathf.FloorToInt(segmentX+width*sc.xOffset),
                                                 segmentY,
                                                 scale * treeScale,
                                                 segment.clipHeight,
                                                 true,
                                                 false);

                //r.Vlin(segmentY, Mathf.FloorToInt(segmentY+3.0f*scale), Mathf.FloorToInt(segmentX + width*sc.xOffset), 0);
                }
            }

            float carScale = 1.0f / 22.3f;
            if (segment.cars != null)
            {
                foreach (EnemyCar car in segment.cars)
                {
                    //Debug.Log("segment index:" + segment.index);
                    //Debug.Log("car count:" + segment.cars.Count);

                    float sy1 = segment.screenPosition1.y;
                    float sy2 = segment.screenPosition2.y;
                    float sx1 = segment.screenPosition1.x;
                    float sx2 = segment.screenPosition2.x;

                    float sc1 = segment.screenPosition1.z;
                    float sc2 = segment.screenPosition2.z;

                    int sy = Mathf.FloorToInt((sy2-sy1) * car.segmentFrac + sy1);
                    float sx = (sx2-sx1) * car.segmentFrac + sx1;

                    float sc = (sc2-sc1) * car.segmentFrac + sc1;

                    r.drawSpriteScaledAndClipped(r.cars[car.carSpriteIndex],
                                                 Mathf.RoundToInt(sx + width * car.xOffset),
                                                 sy, 
                                                 sc * carScale,
                                                 segment.clipHeight,
                                                 true,
                                                 false);

                }
            }

            float bulletScale = 1.0f / 10.0f;

            if (segment.bullets != null)
            {
                foreach (Bullet bullet in segment.bullets)
                {
                    float sy1 = segment.screenPosition1.y;
                    float sy2 = segment.screenPosition2.y;
                    float sx1 = segment.screenPosition1.x;
                    float sx2 = segment.screenPosition2.x;

                    float sc1 = segment.screenPosition1.z;
                    float sc2 = segment.screenPosition2.z;

                    float sc = (sc2-sc1) * bullet.segmentFrac + sc1;
                    int sy = Mathf.FloorToInt((sy2-sy1) * bullet.segmentFrac + sy1 + sc * bulletScale);
                    float roadX = (sx2-sx1) * bullet.segmentFrac + sx1;
                    int sx = Mathf.FloorToInt(roadX + width * bullet.xOffset);

                    if (sy >= segment.clipHeight)
                    {
                        r.Vlin(sy, sy, sx, 0);                        
                    }
                }
            }

            float particleScale = 1.0f / 4.0f;
            float yFactor = 12.0f;
            if (segment.particles != null)
            {
                foreach (Particle particle in segment.particles)
                {
                    //Debug.Log("drawing particle");

                    float sc = segment.screenPosition1.z * particleScale;
                    int sy = Mathf.FloorToInt(segment.screenPosition1.y + particle.position.y * sc * yFactor);
                    int sx = Mathf.FloorToInt(segment.screenPosition1.x + particle.position.x * width * sc);
                    //Debug.Log("sx " + sx + " sy " + sy);
                    if (sy >= segment.clipHeight)
                    {
                        r.Vlin(sy, sy, sx, particle.colorIndex);
                    }
                }
            }
        }
    }
    
    
    public void Start () 
    {
        Debug.Log("Starting Highway Mode");
        frameCounter = 0;

        ResetToAttract();
        ResetRoad(LegIdentifier.Manchester);
    }

    void ResetToAttract()
    {
        isShowingAttract = true;
        winnerScreen = false;
        playerHitPoints = 3;
        playerDeadTime = 0.0f;
        timeInAttract = 0.0f;
        
        speed = 15.0f;
        trackPosition = 0.0f;
        worldPlayerPosition = 0.0f;
        playerDeadTime = 0.0f;
        currentLeg = LegIdentifier.Manchester;
    }

    void StartLeg(LegIdentifier legId)
    {
        Debug.Log("StartLeg " + legId);
        isShowingAttract = false;
        winnerScreen = false;
        playerHitPoints = 3;
        playerDeadTime = 0.0f;
        
        speed = 15.0f;
        trackPosition = 0.0f;
        worldPlayerPosition = 0.0f;
        playerDeadTime = 0.0f;
        skyOffset = -30.0f;
        backgroundIndex = 0;

        ResetRoad(legId);
    }

    bool FireButtonHit()
    {
        string[] shootButtons = {"Fire1", "Fire2", "Fire3", "Jump"};

        foreach (string buttonLabel in shootButtons)
        {
            if (Input.GetButtonDown(buttonLabel))
            {
                return true;
            }
        }
        return false;
    }

    void DrawHitPoints(RoadRenderer r)
    {
        for (int i = 0; i < playerHitPoints; ++i)
        {
            int boxSize = 2;
            int boxSpacing = 2;
            int right = 61 - (boxSpacing + boxSize) * i;
            int left = right - boxSize;
            int top = 63 - boxSpacing;
            int bottom = top - boxSize;
            
            r.DrawBox(left, top, right, bottom, 9);
        }        
    }

    void DrawEnemies(RoadRenderer r, int maxEnemies, int remainingEnemies)
    {
        for (int i = 0; i < maxEnemies; ++i)
        {
            int boxHeight = 2;
            int boxWidth = 0;
            int boxXSpacing = 2;
            int left = boxXSpacing + (boxWidth + boxXSpacing) * i;
            int right = left + boxWidth;

            int top = 61;
            int bottom = top - boxHeight;

            int color = 0;
            if (i < remainingEnemies)
            {
                color = 1;
            }
            
            r.DrawBox(left, top, right, bottom, color);
        }                
    }

    private Vector3 ProjectWorldToScreen(Vector3 worldPoint, Vector3 cameraPos)
    {
        Vector3 cameraPoint = worldPoint - cameraPos;
        Vector3 projPoint = new Vector3 (
            cameraPoint.x * SCREEN_DISTANCE / cameraPoint.y,
            cameraPoint.z * SCREEN_DISTANCE / cameraPoint.y,
            (cameraPoint.y - NEAR_PLANE) * (FAR_PLANE-NEAR_PLANE));

        
        Vector3 screenPoint = new Vector3 (
            32 + 50 * projPoint.x,
            50 + 50 * projPoint.y,
            50 * SCREEN_DISTANCE / cameraPoint.y
            );
        return screenPoint;
    }

    public void Tick(RoadRenderer r) 
    {
        frameCounter++;

        float dt = Time.deltaTime;
        
        trackPosition += speed * dt;
        if (trackPosition >= trackLength)
        {
            trackPosition -= trackLength;
            lapsNeeded--;
        }

        if ((!winnerScreen) && (!isShowingAttract))
        {
            if ((lapsNeeded<=0) || (enemyCars.Count ==0))
            {
                IncrementLeg();
                return;
            }
        }

        Segment playerSegment = FindSegment(trackPosition);

        float speedFrac = speed / maxSpeed;

        float dx = dt * 2.0f * speedFrac; // allow us to cross the
                                          // track in 1.0f seconds
                                          // at max speed

        if (PlayerIsAlive())
        {

            float throttle = Input.GetAxis("Vertical");
            float steering = Input.GetAxis("Horizontal");
            if (isShowingAttract || winnerScreen)
            {
                throttle = 0.0f;
                steering = 0.0f;
            }
    
            worldPlayerPosition += dx * steering;
    
            worldPlayerPosition += dx * speedFrac * playerSegment.curve * centrifugal;
    
            if (throttle > 0)
            {
                speed = Accelerate(speed, accel * throttle, dt);
            }
            else if (throttle < 0)
            {
                speed = Accelerate(speed, -brake * throttle, dt);
            }
            else
            {
                speed = Accelerate(speed, decel, dt);
            }
    
            if (((worldPlayerPosition < -1.0f) || (worldPlayerPosition > 1.0f)) &&
                (speed > offRoadLimit))
            {
                speed = Accelerate(speed, offRoadDecel, dt);
            }
            
            worldPlayerPosition = Clamp(worldPlayerPosition, -2.0f, 2.0f);
            speed = Clamp(speed, 0, maxSpeed);
            if ((!(isShowingAttract || winnerScreen)) &&
                (FireButtonHit()))
            {
                AddBullet(worldPlayerPosition, trackPosition + carVisualOffset, 0, speed + bulletSpeed, 100.0f);
            }
        }
        else
        { // player not alive
            speed /= 2.0f;
            if (speed < 0.0001f)
            {
                speed = 0.0f;
            }
            playerDeadTime += Time.deltaTime;
        }
        
        if (Input.GetKeyDown("x"))
        {
            Debug.Log("X");
            float explosionPos = trackPosition + 40.0f;
            Segment explosionSegment = FindSegment(explosionPos);
            AddExplosion(0.0f, 0.0f, explosionSegment);
        }

        if (Input.GetKeyDown("1"))
        {
            currentLeg = LegIdentifier.Manchester;
            StartLeg(currentLeg);
        }
        if (Input.GetKeyDown("2"))
        {
            Debug.Log("2");
            currentLeg = LegIdentifier.Boston;
            StartLeg(currentLeg);
        }

        if (Input.GetKeyDown("3"))
        {
            currentLeg = LegIdentifier.Albany;
            StartLeg(currentLeg);
        }
        
        if (Input.GetKeyDown("4"))
        {
            currentLeg = LegIdentifier.NewYork;
            StartLeg(currentLeg);
        }
        
        if (Input.GetKeyDown("5"))
        {
            currentLeg = LegIdentifier.Philadelphia;
            StartLeg(currentLeg);
        }

        if (Input.GetKeyDown("6"))
        {
            currentLeg = LegIdentifier.Harrisburg;
            StartLeg(currentLeg);
        }
        
        if (Input.GetKeyDown("7"))
        {
            currentLeg = LegIdentifier.Baltimore;
            StartLeg(currentLeg);
        }
        
        if (Input.GetKeyDown("8"))
        {
            currentLeg = LegIdentifier.WashingtonDC;
            StartLeg(currentLeg);
        }

        DetectPlayerCarCollision();
        UpdateBullets();
        UpdateCars();
        UpdateParticles();

        skyOffset += skySpeed * playerSegment.curve * speedFrac;

        DrawBackground(r, skyOffset);
        DrawRoad(r, trackPosition);
        if (!(isShowingAttract || winnerScreen))
        {
            DrawPlayerCar(r);
            DrawHitPoints(r);
            DrawEnemies(r, legCarCount, enemyCars.Count);
        }

        if (playerDeadTime > 4.0f)
        {
            ResetToAttract();
        }

        if (isShowingAttract)
        {
            r.drawSprite(r.title, 0, 8, false, false);
            timeInAttract += Time.deltaTime;

            if (timeInAttract > 2.0f)
            {
                r.drawString("PRESS FIRE", 2, 2, 0);
                if (FireButtonHit())
                {
                    Debug.Log("Starting leg");
                    currentLeg = LegIdentifier.Manchester;
                    StartLeg(currentLeg);
                }
            }
        }
        if (winnerScreen)
        {
            r.drawSprite(r.winner, 0, 0, false, false);
            timeInAttract += Time.deltaTime;
            if (timeInAttract > 5.0f)
            {
                timeInAttract = 0.0f;
                winnerScreen = false;
                currentLeg = LegIdentifier.Manchester;
                ResetToAttract();
            }
        }
    }
}
