using System;

namespace DestroyIt
{
    public static class ArrayExtensions
    {
        /// <summary>Provides a quick way to remove elements from a standard array.</summary>
        public static T[] RemoveAllAt<T>(this T[] array, int[] removeIndices)
        {
            T[] newArray = new T[0];

            if (removeIndices.Length == 0) return array;
            if (removeIndices.Length >= array.Length) return newArray;

            newArray = new T[array.Length];
            int i = 0;
            int j = 0;
            int itemsKept = 0;
            while (i < array.Length)
            {
                bool keepItem = true;
                for (int x = 0; x < removeIndices.Length; x++)
                {
                    if (i == removeIndices[x])
                        keepItem = false;
                }
                if (keepItem)
                {
                    itemsKept++;
                    newArray[j] = array[i];
                    j++;
                }
                i++;
            }
            Array.Resize(ref newArray, itemsKept);
            return newArray;
        }
    }
}
