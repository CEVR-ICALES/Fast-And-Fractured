using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ListUtilities  
{
    public static T GetRandomValueFromList<T>(this List<T> list, T defaultValue)
    {
        if (list == null || list.Count == 0)
        {
            return defaultValue;
        }
        return list[Random.Range(0, list.Count)];
    }
    public static void ShuffleList<T>(this IList<T> list)
    {
        if (list == null) return;
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
