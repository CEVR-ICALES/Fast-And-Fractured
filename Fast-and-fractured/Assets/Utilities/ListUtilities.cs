using NRandom;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ListUtilities
{
    public static T GetRandomValueFromList<T>(this List<T> list, T defaultValue = default(T))
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
    public static T GetRandomValueFromList<T>(this List<T> list, IRandom randomGenerator=null)
    {
        if (list == null || list.Count == 0) return default(T);
        if (randomGenerator == null)
            randomGenerator = Utilities.DeterministicRandom.Instance;

        int index = randomGenerator.NextInt(0, list.Count);
        return list[index];
    }
    public static void ShuffleList<T>(this IList<T> list, IRandom randomGenerator = null)
    {
        if (randomGenerator == null)
            randomGenerator = Utilities.DeterministicRandom.Instance;

        if (list == null) return;
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = randomGenerator.NextInt(0, n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }
} 
