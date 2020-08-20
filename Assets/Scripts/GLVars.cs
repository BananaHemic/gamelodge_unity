using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GLVars : GenericSingleton<GLVars>
{
    public bool UseProd;

    public static float DefaultHeight
    {
        get
        {
            return 1.65f;
        }
    }
    public string GameServerAddress
    {
        get
        {
            //return UseProd ? "54.146.248.56" : "127.0.0.1";
            return UseProd ? "34.230.129.62" : "127.0.0.1";
        }
    }
    public int GameServerPort
    {
        get
        {
            return 4296;
        }
    }
    public string APIAddress
    {
        get
        {
            //return UseProd ? "ec2-3-91-72-0.compute-1.amazonaws.com" : "127.0.0.1";
            return UseProd ? "http://3.91.72.0" : "http://127.0.0.1";
        }
    }
    public int APIPort
    {
        get
        {
            return 8080;
        }
    }
}
