using System.Collections.Generic;
using FastAndFractured.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// Tunes the CinemachineCollider on the networked player's camera rig so it stops oscillating.
    ///
    /// Measured on the host in MpAutoRecord.log (2026-08-21, MountainVillage, frames 4818-4827):
    /// with the car completely still — body, Visuals and Rigidbody all within a millimetre of each
    /// other, speed 0.00 — the camera swung 0.66 m back and forth along its own view axis, up to
    /// 0.37 m in a single frame. Nothing that follows the car can move the camera 350x further than
    /// the car moved, and no impulse was fired, so the motion came from the camera collider pulling
    /// the camera in and letting it back out. The excursion also stayed under m_DistanceLimit (1 m),
    /// which is the collider's own cap.
    ///
    /// The shipped values are the textbook configuration for that oscillation: react to an occlusion
    /// instantly (m_MinimumOcclusionTime 0), pull in with no damping (m_DampingWhenOccluded 0), hold
    /// nothing (m_SmoothingTime 0), then ease back out over m_Damping 5 — which re-occludes, which
    /// snaps in again. The values below break that loop: ignore very brief occlusions, damp the pull
    /// in, and hold the pulled-in distance briefly before easing out.
    ///
    /// It also attaches CameraTargetSmoother to the rig, which is what actually removes the
    /// tick-rate stepping; see that script for the measurements.
    ///
    /// Only the multiplayer player prefab is touched. The singleplayer prefabs keep their own values.
    ///
    /// Run from Tools ▸ Fast And Fractured ▸ Camera Rig - Fix Collider Oscillation (Headless).
    /// </summary>
    public static class CameraRigSetupTool
    {
        private const string PlayerPrefabPath = "Assets/Multiplayer/Player/Prefabs/NetworkedPlayer.prefab";

        /// <summary>Occlusions shorter than this are ignored, so a ray clipping an edge for one
        /// frame no longer yanks the camera.</summary>
        private const float MinimumOcclusionTime = 0.2f;

        /// <summary>How long the camera holds its closest distance before easing back out. This is
        /// the setting that actually stops the in/out ping-pong.</summary>
        private const float SmoothingTime = 0.5f;

        /// <summary>Damping while pulling in. Zero made every occlusion a snap.</summary>
        private const float DampingWhenOccluded = 0.2f;

        [MenuItem("Tools/Fast And Fractured/Camera Rig - Fix Collider Oscillation (Headless)")]
        public static void Run()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[CameraRigSetupTool] Player prefab not found at '{PlayerPrefabPath}'.");
                return;
            }

            var changed = new List<string>();
            int found = 0;

            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;

                string typeName = component.GetType().Name;
                if (typeName != "CinemachineCollider" && typeName != "CinemachineDeoccluder") continue;
                found++;

                var so = new SerializedObject(component);
                Apply(so, "m_MinimumOcclusionTime", MinimumOcclusionTime, component.name, changed);
                Apply(so, "m_SmoothingTime", SmoothingTime, component.name, changed);
                Apply(so, "m_DampingWhenOccluded", DampingWhenOccluded, component.name, changed);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                if (component.GetType().Name != "CinemachineFreeLook") continue;

                if (component.GetComponent<CameraTargetSmoother>() == null)
                {
                    component.gameObject.AddComponent<CameraTargetSmoother>();
                    changed.Add($"{component.name}: added CameraTargetSmoother");
                }
            }

            if (found == 0)
                Debug.LogWarning($"[CameraRigSetupTool] No camera collider found under '{PlayerPrefabPath}'.");

            if (changed.Count == 0)
            {
                Debug.Log($"[CameraRigSetupTool] {found} camera collider(s) already configured; no change.");
                return;
            }

            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CameraRigSetupTool] Updated {found} camera collider(s):\n  " + string.Join("\n  ", changed));
        }

        private static void Apply(SerializedObject so, string path, float value, string owner, List<string> changed)
        {
            SerializedProperty prop = so.FindProperty(path);
            if (prop == null)
            {
                Debug.LogWarning($"[CameraRigSetupTool] '{owner}' has no property '{path}'; skipped.");
                return;
            }

            if (Mathf.Approximately(prop.floatValue, value)) return;

            changed.Add($"{owner}.{path}: {prop.floatValue} -> {value}");
            prop.floatValue = value;
        }
    }
}
