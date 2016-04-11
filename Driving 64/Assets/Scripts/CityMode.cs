using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;


public class RgbColor
{
    public int Red;
    public int Green;
    public int Blue;
}

public class BspNode
{
    public string Name;
    public RgbColor Color;
    public string edge;
}

public class BdgRect
{
    public float Left;
    public float Right;
    public float Top;
    public float Bottom;
    public RgbColor Color;
}

[XmlRoot("City")]
public class CityContainer
{
    public string Name;
    
    [XmlArray("Rects"), XmlArrayItem("BdgRect")]
    public List<BdgRect> Rects;

    [XmlArray("BspNodes"), XmlArrayItem("BspNode")]
    public List<BspNode> BspNodes;

    public static CityContainer LoadFromPath(string path)
    {
        XmlSerializer serializer = new XmlSerializer(typeof(CityContainer));
        using (FileStream stream = new FileStream(path, FileMode.Open))
        {
            return serializer.Deserialize(stream) as CityContainer;
        }
    }

    public static CityContainer LoadFromText(string text)
    {
        XmlSerializer serializer = new XmlSerializer(typeof(CityContainer));
        return serializer.Deserialize(new StringReader(text)) as CityContainer;
    }
}


public class CityMode
{
    CityContainer cityContainer;
    
    void ReadCity(City city)
    {
        string filename = null;
        string cityDirectoryName = "Cities";
        switch (city)
        {
            case City.Boston:
                Debug.Log("Reading Boston");
                filename = "bos-buildings.xml";
                break;
            default:
                break;
        }
        string directoryPath = System.IO.Path.Combine(
            Application.streamingAssetsPath,
            cityDirectoryName);
        string filePath = System.IO.Path.Combine(
            directoryPath,
            filename);
        Debug.Log("filePath: " + filePath);

        if (filePath.Contains("://"))
        {
            WWW www = new WWW(filePath);
            // TODO - I need to spin to load the www.
            //yield return www;
            this.cityContainer = CityContainer.LoadFromText(www.text);
        }
        else
        {
            this.cityContainer = CityContainer.LoadFromPath(filePath);
        }
    }
    
    public void Start(City city)
    {
        Debug.Log("Starting City Mode");
        ReadCity(city);
        Debug.Log("City: " + cityContainer);
        Debug.Log("CityRects: " + cityContainer.Rects.Count);
        Debug.Log("BspNodes: " + cityContainer.BspNodes.Count);
        Debug.Log("Root BSP Node: " + cityContainer.BspNodes[0].Name);
    }

    public void Tick(RoadRenderer r)
    {
        r.ClearScreen(0);
    }
}
