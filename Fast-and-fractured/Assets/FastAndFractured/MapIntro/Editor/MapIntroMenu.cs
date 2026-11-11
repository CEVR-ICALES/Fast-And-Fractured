using UnityEditor;
using UnityEngine;

namespace FastAndFractured.EditorTools
{
    public static class MapIntroMenu
    {
        private const string MENU_PATH = "Tools/Fast And Fractured/Map Intro/Play Map Intros";

        [MenuItem(MENU_PATH)]
        private static void Toggle()
        {
            MapIntroLibrary library = MapIntroLibrary.Load();
            if (library == null)
            {
                Debug.LogError($"[MapIntro] No {MapIntroLibrary.RESOURCES_PATH} asset in a Resources folder.");
                return;
            }
            library.PlayIntros = !library.PlayIntros;
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MapIntro] Map intros {(library.PlayIntros ? "ENABLED" : "DISABLED")}.");
        }

        [MenuItem(MENU_PATH, true)]
        private static bool ToggleValidate()
        {
            MapIntroLibrary library = MapIntroLibrary.Load();
            Menu.SetChecked(MENU_PATH, library != null && library.PlayIntros);
            return library != null;
        }
    }
}
