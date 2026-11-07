using System;
using UnityEditor;
using UnityEngine;

namespace Utilities.Audio.Editor
{
    public sealed class AudioCueCatalogPostprocessor : AssetPostprocessor
    {
        private static bool _scheduled;
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
                if (path == AudioReferencesWindow.DEFAULT_CATALOG_PATH) { Schedule(); break; }
        }

        private static void Schedule()
        {
            if (_scheduled) return;
            _scheduled = true;
            EditorApplication.delayCall += Generate;
        }

        [MenuItem("Tools/Fast And Fractured/Generate Audio Keys")]
        public static void Generate()
        {
            _scheduled = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { Schedule(); return; }
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(AudioReferencesWindow.DEFAULT_CATALOG_PATH);
            if (catalog == null) return;
            try { AudioCueCodeGenerator.Generate(catalog); }
            catch (Exception e) { Debug.LogError("Audio key generation failed: " + e.Message); }
        }
    }
}
