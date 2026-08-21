using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    [InitializeOnLoad]
    public static class ForceRecompile
    {
        static ForceRecompile()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool("MultiplayerRecompileForced", false))
                {
                    SessionState.SetBool("MultiplayerRecompileForced", true);
                    Debug.Log("[Multiplayer] Forcing recompilation to refresh assembly references...");
                    AssetDatabase.Refresh();
                    UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                }
            };
        }
    }
}
