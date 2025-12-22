using UnityEngine;
using UnityEditor;

namespace Utilities
{
    public static class EditorGUILayoutArrays
    {

        public class ArrayFieldSettings
        {

            public string label;
            public bool open;

            public ArrayFieldSettings(string label = "Array", bool open = true)
            {
                this.label = label;
                this.open = open;
            }
        }

        public static string[] StringArrayField(ArrayFieldSettings settings, string[] array)
        {
            return StringArrayField(settings.label, ref settings.open, array);
        }

        public static string[] StringArrayField(string label, ref bool open, string[] array)
        {
            open = EditorGUILayout.Foldout(open, label);
            int newSize = array.Length;

            if (open)
            {
                newSize = EditorGUILayout.IntField("Size", newSize);
                newSize = newSize < 0 ? 0 : newSize;

                if (newSize != array.Length)
                {
                    array = ResizeArray<string>(array, newSize);
                }

                for (var i = 0; i < newSize; i++)
                {
                    array[i] = EditorGUILayout.TextField("Value " + i, array[i]);
                }
            }
            return array;
        }

        public static void LabelArrayField(ArrayFieldSettings settings, string[] array)
        {
            LabelArrayField(settings.label, ref settings.open, array);
        }

        public static string[] LabelArrayField(string label, ref bool open, string[] array)
        {
            open = EditorGUILayout.Foldout(open, label);
            int newSize = array.Length;

            if (open)
            {
                //newSize = EditorGUILayout.IntField("Size", newSize);
                //newSize = newSize < 0 ? 0 : newSize;

                //if (newSize != array.Length)
                //{
                //    array = ResizeArray<string>(array, newSize);
                //}

                for (var i = 0; i < newSize; i++)
                {
                    EditorGUILayout.LabelField(array[i]);
                }
            }
            return array;
        }

        public static int[] IntArrayField(ArrayFieldSettings settings, int[] array)
        {
            return IntArrayField(settings.label, ref settings.open, array);
        }

        public static int[] IntArrayField(string label, ref bool open, int[] array)
        {
            open = EditorGUILayout.Foldout(open, label);
            int newSize = array.Length;

            if (open)
            {
                newSize = EditorGUILayout.IntField("Size", newSize);
                newSize = newSize < 0 ? 0 : newSize;

                if (newSize != array.Length)
                {
                    array = ResizeArray<int>(array, newSize);
                }

                for (var i = 0; i < newSize; i++)
                {
                    array[i] = EditorGUILayout.IntField("Value " + i, array[i]);
                }
            }
            return array;
        }

        public static float[] FloatArrayField(ArrayFieldSettings settings, float[] array)
        {
            return FloatArrayField(settings.label, ref settings.open, array);
        }

        public static float[] FloatArrayField(string label, ref bool open, float[] array)
        {
            open = EditorGUILayout.Foldout(open, label);
            int newSize = array.Length;

            if (open)
            {
                newSize = EditorGUILayout.IntField("Size", newSize);
                newSize = newSize < 0 ? 0 : newSize;

                if (newSize != array.Length)
                {
                    array = ResizeArray<float>(array, newSize);
                }

                for (var i = 0; i < newSize; i++)
                {
                    array[i] = EditorGUILayout.FloatField("Value " + i, array[i]);
                }
            }
            return array;
        }

        public static double[] DoubleArrayField(ArrayFieldSettings settings, double[] array)
        {
            return DoubleArrayField(settings.label, ref settings.open, array);
        }

        public static double[] DoubleArrayField(string label, ref bool open, double[] array)
        {
            open = EditorGUILayout.Foldout(open, label);
            int newSize = array.Length;

            if (open)
            {
                newSize = EditorGUILayout.IntField("Size", newSize);
                newSize = newSize < 0 ? 0 : newSize;

                if (newSize != array.Length)
                {
                    array = ResizeArray<double>(array, newSize);
                }

                for (var i = 0; i < newSize; i++)
                {
                    array[i] = EditorGUILayout.DoubleField("Value " + i, array[i]);
                }
            }
            return array;
        }

        public static Vector2[] Vector2ArrayField(ArrayFieldSettings settings, Vector2[] array)
        {
            return Vector2ArrayField(settings.label, ref settings.open, array);
        }

        public static Vector2[] Vector2ArrayField(string label, ref bool open, Vector2[] array)
        {
            open = EditorGUILayout.Foldout(open, label);
            int newSize = array.Length;

            if (open)
            {
                newSize = EditorGUILayout.IntField("Size", newSize);
                newSize = newSize < 0 ? 0 : newSize;

                if (newSize != array.Length)
                {
                    array = ResizeArray<Vector2>(array, newSize);
                }

                for (var i = 0; i < newSize; i++)
                {
                    array[i] = EditorGUILayout.Vector2Field("Value " + i, array[i]);
                }
            }
            return array;
        }

        public static Vector3[] Vector3ArrayField(ArrayFieldSettings settings, Vector3[] array)
        {
            return Vector3ArrayField(settings.label, ref settings.open, array);
        }

        public static Vector3[] Vector3ArrayField(string label, ref bool open, Vector3[] array)
        {
            open = EditorGUILayout.Foldout(open, label);
            int newSize = array.Length;

            if (open)
            {
                newSize = EditorGUILayout.IntField("Size", newSize);
                newSize = newSize < 0 ? 0 : newSize;

                if (newSize != array.Length)
                {
                    array = ResizeArray<Vector3>(array, newSize);
                }

                for (var i = 0; i < newSize; i++)
                {
                    array[i] = EditorGUILayout.Vector3Field("Value " + i, array[i]);
                }
            }
            return array;
        }

        public static Vector4[] Vector4ArrayField(ArrayFieldSettings settings, Vector4[] array)
        {
            return Vector4ArrayField(settings.label, ref settings.open, array);
        }

        public static Vector4[] Vector4ArrayField(string label, ref bool open, Vector4[] array)
        {
            open = EditorGUILayout.Foldout(open, label);
            int newSize = array.Length;

            if (open)
            {
                newSize = EditorGUILayout.IntField("Size", newSize);
                newSize = newSize < 0 ? 0 : newSize;

                if (newSize != array.Length)
                {
                    array = ResizeArray<Vector4>(array, newSize);
                }

                for (var i = 0; i < newSize; i++)
                {
                    array[i] = EditorGUILayout.Vector4Field("Value " + i, array[i]);
                }
            }
            return array;
        }

        public static bool[] BooleanArrayField(ArrayFieldSettings settings, bool[] array)
        {
            return BooleanArrayField(settings.label, ref settings.open, array);
        }

        public static bool[] BooleanArrayField(string label, ref bool open, bool[] array)
        {
            open = EditorGUILayout.Foldout(open, label);
            int newSize = array.Length;

            if (open)
            {
                newSize = EditorGUILayout.IntField("Size", newSize);
                newSize = newSize < 0 ? 0 : newSize;

                if (newSize != array.Length)
                {
                    array = ResizeArray<bool>(array, newSize);
                }

                for (var i = 0; i < newSize; i++)
                {
                    array[i] = EditorGUILayout.Toggle("Value " + i, array[i]);
                }
            }
            return array;
        }

        public static Color[] ColorArrayField(ArrayFieldSettings settings, Color[] array)
        {
            return ColorArrayField(settings.label, ref settings.open, array);
        }

        public static Color[] ColorArrayField(string label, ref bool open, Color[] array)
        {
            open = EditorGUILayout.Foldout(open, label);
            int newSize = array.Length;

            if (open)
            {
                newSize = EditorGUILayout.IntField("Size", newSize);
                newSize = newSize < 0 ? 0 : newSize;

                if (newSize != array.Length)
                {
                    array = ResizeArray<Color>(array, newSize);
                }

                for (var i = 0; i < newSize; i++)
                {
                    array[i] = EditorGUILayout.ColorField("Value " + i, array[i]);
                }
            }
            return array;
        }

        public static Material[] MaterialArrayField(ArrayFieldSettings settings, Material[] array)
        {
            return MaterialArrayField(settings.label, ref settings.open, array);
        }

        public static Material[] MaterialArrayField(string label, ref bool open, Material[] array)
        {
            open = EditorGUILayout.Foldout(open, label);
            int newSize = array.Length;

            if (open)
            {
                newSize = EditorGUILayout.IntField("Size", newSize);
                newSize = newSize < 0 ? 0 : newSize;

                if (newSize != array.Length)
                {
                    array = ResizeArray(array, newSize);
                }

                for (var i = 0; i < newSize; i++)
                {
                    array[i] = EditorGUILayout.ObjectField("Value " + i, array[i], typeof(Material), false) as Material;
                }
            }
            return array;
        }

        private static T[] ResizeArray<T>(T[] array, int size)
        {
            T[] newArray = new T[size];

            for (var i = 0; i < size; i++)
            {
                if (i < array.Length)
                {
                    newArray[i] = array[i];
                }
            }

            return newArray;
        }
    }
}
