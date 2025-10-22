using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public static class DictionaryLibrary
{
    //DLS FSR2 Modes
    public static Dictionary<string, int> DynamicResolutionMode = new Dictionary<string, int>()
    {
        {"Max Quality",2 },
        {"Balanced",1 },
        {"Performance",0 },
        {"Ultra Performance",3 },
    };

    public static Dictionary<int, int> TranslationDynamicResolution = new Dictionary<int, int>()
    {
        {0,2 },
        {1,1 },
        {2,0 },
        {3,3 },
    };
}
