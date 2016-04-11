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
}

class StraightSegment
{
}

class CurveSegment
{
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
    float maxSpeed = 100.0f;
    float accel = 10.0f;
    float brake = -15.0f;
    float decel = -1.0f;
    float offRoadDecel = -3.0f;
    float offRoadLimit = 20.0f;

    float centrifugal = 30.0f; // push we feel going around a curve

    float worldCameraHeight = 2.0f;
    
    bool keyLeft = false;
    bool keyRight = false;
    bool keyFaster = false;
    bool keyBraking = false;

    float speed; // m/s
    float trackPosition; // world (y) position of the camera
    float worldPlayerPosition; // world (x) position of the player's
                               // car

    float trackLength; // world extent (y) of road that we have data
                       // for (see also tripDistance?) TODO

    float segmentLength = 2.0f;

    List<Segment> segments;

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

    void AddLeftCurve()
    {
        AddFlatTurn(ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    ROADLENGTH_MEDIUM,
                    -CURVE_EASY,
                    HILL_NONE);  // easy left
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

    void ResetRoad()
    {
        segments = new List<Segment>();

        AddStraight(ROADLENGTH_SHORT, HILL_NONE);
        AddStraightHill(ROADLENGTH_LONG, ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, HILL_MEDIUM);
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        
        AddStraightHill(ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, ROADLENGTH_MEDIUM, HILL_HIGH);
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);

        AddLeftCurve();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        AddSCurves();
        AddStraight(ROADLENGTH_MEDIUM, HILL_NONE);
        
        trackLength = segments.Count * segmentLength;
    }

    Segment FindSegment(float trackDist)
    {
        return segments[Mathf.FloorToInt(trackDist / segmentLength) % segments.Count];
    }

    void DrawBackground(RoadRenderer r)
    {
        r.drawSprite(r.background, 0, 0); // TODO parallax
    }

    void DrawPlayerCar(RoadRenderer r)
    {
        int carPosition = 32 // center of screen
            - 10 // sprite width
            + Mathf.RoundToInt(worldPlayerPosition * 20); // TODO this
                                                          // needs to
                                                          // be
                                                          // cleaned up
        r.drawSprite(r.cars[0], carPosition, 5);
    }

    void DrawSegment(RoadRenderer r, Vector3 screenPos1, Vector3 screenPos2, SegmentColor color, int horizon)
    {
        int start = Mathf.FloorToInt(screenPos1.y);
        int end = Mathf.CeilToInt(screenPos2.y);

        int roadColor = ((color == SegmentColor.Dark) ? DARK_ROAD_COLOR : LIGHT_ROAD_COLOR);
        int rumbleColor = ((color == SegmentColor.Dark) ? DARK_RUMBLE_COLOR : LIGHT_RUMBLE_COLOR);
        int grassColor = ((color == SegmentColor.Dark) ? DARK_GRASS_COLOR : LIGHT_GRASS_COLOR);
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
            r.Hlin(roadLeft,roadRight,y,roadColor);
            r.Hlin(rumbleLeft, roadLeft-1, y, rumbleColor);
            r.Hlin(roadRight+1, rumbleRight, y, rumbleColor);
            r.Hlin(0, rumbleLeft-1, y, grassColor);
            r.Hlin(rumbleRight+1, 63, y, grassColor);
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

        float drawDistance = 40;
        int horizon = 0; // for rolling hills, this can obscure
                            // later segments

        Vector3 cameraPos = new Vector3 (0,
                                         trackPosition,
                                         worldCameraHeight+trackElev);

        for (int n = 0; n < drawDistance; ++n)
        {
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

            int lastY = Mathf.CeilToInt(segment.screenPosition2.y);

            if (lastY <= horizon)
            {
                continue;
            }

            DrawSegment(r, segment.screenPosition1, segment.screenPosition2, segment.color, horizon);
            horizon = lastY;
        }
    }
    
    
    public void Start () 
    {
        Debug.Log("Starting Highway Mode");
        frameCounter = 0;

        speed = 15.0f;
        trackPosition = 0.0f;
        worldPlayerPosition = 0.0f;

        ResetRoad();
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
        }

        Segment playerSegment = FindSegment(trackPosition);

        float speedFrac = speed / maxSpeed;

        float dx = dt * 2.0f * speedFrac; // allow us to cross the
                                          // track in 1.0f seconds
                                          // at max speed

        float throttle = Input.GetAxis("Vertical");
        float steering = Input.GetAxis("Horizontal");

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

        DrawBackground(r);
        DrawRoad(r, trackPosition);
        DrawPlayerCar(r);

        /*
        int horizon = 50;
        for (int y = 0; y < 64; ++y)
        {
            int c = 6;
            if (y < horizon)
            {
                c = 4;
            }
            r.Hlin(0, 63, y, c);
            
            if (y < horizon)
            {
                float dist = y / (float)horizon;
                int left = (int)(32*dist);
                r.Hlin(left, 63-left, y, 5);
            }
        }
        */     
    }
}
