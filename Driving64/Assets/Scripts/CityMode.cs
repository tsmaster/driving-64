using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class CityMode
{
    Vector2 carPos;
    float carHeading;

    BDG_City city;
    
    public void Start(City cityEnum)
    {
        Debug.Log("Starting City Mode");
        city = new Boston();

        Debug.Log("City:" + city);
        Debug.Log("City root:" + city.BSP_Root);

        carPos = new Vector2(400.0f, 200.0f);
        carHeading = Mathf.PI;
    }

    float normalizeRadians(float rad)
    {
        while (rad < Mathf.PI)
        {
            rad += 2.0f * Mathf.PI;
        }
        while (rad > Mathf.PI)
        {
            rad -= 2.0f * Mathf.PI;
        }
        return rad;
    }

    float clampToRange(float val, float minVal, float maxVal)
    {
        return Mathf.Min(Mathf.Max(val,minVal), maxVal);
    }

    private void drawNode(BDG_Node node, RoadRenderer r)
    {
        Debug.Log("drawing node "+node.Name);

        Vector2 v1 = new Vector2(node.x1, node.y1);
        Vector2 v2 = new Vector2(node.x2, node.y2);

        v1 = v1 - carPos;
        v2 = v2 - carPos;

        float theta1 = Mathf.Atan2(v1.y, v1.x);
        float theta2 = Mathf.Atan2(v2.y, v2.x);

        theta1 -= carHeading;
        theta1 = normalizeRadians(theta1);
        theta2 -= carHeading;
        theta2 = normalizeRadians(theta2);

        Debug.Log(string.Format("T1: {0} T2: {1}", theta1 * Mathf.Rad2Deg, theta2* Mathf.Rad2Deg));
        
        float FOV = 45.0f;
        int SCREEN_WIDTH = 64;
        float WALL_HEIGHT = 80.0f;
        float sx1 = theta1 * Mathf.Rad2Deg / FOV * SCREEN_WIDTH/2 + SCREEN_WIDTH / 2;
        float sx2 = theta2 * Mathf.Rad2Deg / FOV * SCREEN_WIDTH/2 + SCREEN_WIDTH / 2;

        sx1 = clampToRange(sx1, 0.0f, 64.0f);
        sx2 = clampToRange(sx2, 0.0f, 64.0f);

        Debug.Log(string.Format("sx1: {0} sx2: {1}", sx1, sx2));

        for (int sx = (int)sx2; sx < (int)sx1; ++sx)
        {
            float eyeAngle = (sx - SCREEN_WIDTH / 2) * FOV * Mathf.Deg2Rad;
            
            // project the distance
            float f = (sx - sx2) / (sx1 - sx2);
            Vector2 p = (v1- v2) * f + v2;
            float dist = p.magnitude;
            
            // correct for angular effect
            //dist /= Mathf.Cos(eyeAngle);

            float elevationAngle = Mathf.Atan2(WALL_HEIGHT, dist) * Mathf.Rad2Deg * SCREEN_WIDTH / (2.0f * FOV);

            // clip to screen height
            float sy = clampToRange(elevationAngle, 0.0f, 32.0f);
            Debug.Log("sy:"+sy);
            // draw
            Debug.Log(string.Format("rgb: {0}, {1}, {2}", node.r, node.g, node.b));
            r.VlinRGB(SCREEN_WIDTH/2 - (int)sy, SCREEN_WIDTH/2 + (int)sy, sx, node.r, node.g, node.b);
        }
    }

    public void Tick(RoadRenderer r)
    {
        r.ClearScreen(0);

        Debug.Assert(city != null);
        Debug.Assert(city.BSP_Root != null);

        drawNode(city.BSP_Root, r);

        float degreesPerSecond = 20.0f;
        float rotationSpeed = degreesPerSecond * Mathf.Deg2Rad;
        float rotation = Input.GetAxis("Horizontal") * rotationSpeed;
        rotation *= Time.deltaTime;
        carHeading += rotation;
    }
}
