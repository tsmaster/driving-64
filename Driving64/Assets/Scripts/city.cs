using UnityEngine;
using System.Collections;
using System.Collections.Generic;


abstract class BDG_City
{
    public List<BDG_Rect> Rects;
    public BDG_Node BSP_Root;
}

class BDG_Rect
{
    public BDG_Rect(float left, float right, float bottom, float top, int r, int g, int b)
    {
        
    }
}

class BDG_Node : IEnumerable<BDG_Node>
{
    public float x1, y1, x2, y2;
    public int r, g, b;
    public BDG_Node front, back;
    public string Name;
    
    public BDG_Node(float x1, float y1, float x2, float y2, int r, int g, int b, string name)
    {
        this.Name = name;
        this.x1 = x1;
        this.y1 = y1;
        this.x2 = x2;
        this.y2 = y2;

        this.r = r;
        this.g = g;
        this.b = b;
    }

    public void Add(BDG_Node node)
    {
        if (front == null)
        {
            front = node;
        }
        else if (back == null)
        {
            back = node;
        }
        else
        {
            Debug.Assert(false, "already full");
        }
    }

    public IEnumerator<BDG_Node> GetEnumerator()
    {
        return null;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }
}
