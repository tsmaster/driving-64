using UnityEngine;
using System.Collections;

enum GameMode
{
    Highway,
    City,
};

public enum City
{
    Watertown,
    Manchester,
    Buffalo,
    Syracuse,
    Albany,
    Boston,
    Scranton,
    NewYork,
    Providence,
    Pittsburgh,
    Harrisburg,
    Philadelphia,
    AtlanticCity,
    Baltimore,
    Dover,
    Washington,
};


public class RoadManager : Singleton<RoadManager> {
    protected RoadManager () {}

    GameMode mode;
    HighwayMode highwayMode = new HighwayMode();
    CityMode cityMode = new CityMode();
    
    // Highway cities
    City fromCity;
    City toCity;
    
    // City city
    City inCity;        
    
    
    public void Start()
    {
        highwayMode = new HighwayMode();
        highwayMode.Start();

        //SetCityModeIn(City.Boston);
    }
    
    private void SetMode(GameMode mode)
    {
        this.mode = mode;
    }
    
    void SetHighwayModeFromTo(City from, City to)
    {
        SetMode(GameMode.Highway);    
        fromCity = from;
        toCity = to;
    }

    void SetCityModeIn(City inCity)
    {
        SetMode(GameMode.City);
        this.inCity = inCity;
        cityMode.Start(inCity);
    }
    
    public void Tick(RoadRenderer r)
    {
        switch(mode)
        {
            case GameMode.Highway:
                highwayMode.Tick(r);
                break;
            case GameMode.City:
                cityMode.Tick(r);
                break;
        }
    }
}
