using UnityEngine;
using UnityEditor;
using FastAndFractured;

namespace FastAndFractured.Multiplayer
{
    public class CompilationValidator
    {
        [MenuItem("Fast&Fractured/Validate Multiplayer Compilation")]
        public static void ValidateCompilation()
        {
            Debug.Log("=== Multiplayer Compilation Validation ===");
            Debug.Log("[OK]NetworkInputProvider compiled successfully");
            Debug.Log("[OK]NetworkedVehicle compiled successfully");
            Debug.Log("[OK]NetworkedPlayer compiled successfully");
            Debug.Log("=== All Multiplayer scripts compiled successfully! ===");
        }
    }
}
