using System;

namespace DestroyIt
{
    public static class StringExtensions
    {
        public static string SceneFolder(this string scenePath)
        {
            string[] pathParts = scenePath.Split('/');
            
            if (pathParts.Length > 1)
            {
                string[] folderPath = new string[pathParts.Length - 1];
                
                for (int i = 0; i < pathParts.Length - 1; i++)
                    folderPath[i] = pathParts[i];
                return String.Join("/", folderPath);
            }

            return scenePath;
        }
    }
}
