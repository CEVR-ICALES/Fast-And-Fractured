using System.Collections.Generic;
using System.Reflection;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Observing;
using FishNet.Observing;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Only the players of a match see its objects. The map is loaded for the match's players
    /// alone (NetworkedLobbyManager loads it as a connection scene), while others wait in the
    /// room: someone who joined mid-match, or left it early. Without a visibility rule FishNet
    /// shows every spawned object to every client, so the cars, bullets and props of the
    /// match would be sent to - and spawned into - the menu of the players in the room.
    ///
    /// The rule: an object that lives in a scene is visible to the connections that have that
    /// scene loaded. Objects outside any match scene (the room itself is a global object,
    /// which FishNet never filters) stay visible to everyone. Added at runtime as a default
    /// condition of the ObserverManager, so no prefab or map is edited.
    /// </summary>
    [CreateAssetMenu(menuName = "Fast And Fractured/Multiplayer/Match Scene Condition", fileName = "MatchSceneCondition")]
    public sealed class MatchSceneVisibility : ObserverCondition
    {
        private static readonly FieldInfo DefaultConditionsField =
            typeof(ObserverManager).GetField("_defaultConditions", BindingFlags.NonPublic | BindingFlags.Instance);

        private static NetworkManager _installedOn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _installedOn = null;

        public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
        {
            notProcessed = false;
            if (NetworkObject == null || connection == null) return false;
            return IsVisible(NetworkObject.gameObject.scene, connection.Scenes);
        }

        /// <summary>Pure rule (unit tested): objects outside a loaded scene, or in a scene the connection has, are visible.</summary>
        public static bool IsVisible(UnityEngine.SceneManagement.Scene objectScene, ICollection<UnityEngine.SceneManagement.Scene> connectionScenes)
        {
            if (!objectScene.IsValid() || objectScene.buildIndex < 0) return true;   // DontDestroyOnLoad, runtime-only scenes
            return connectionScenes != null && connectionScenes.Contains(objectScene);
        }

        public override ObserverConditionType GetConditionType() => ObserverConditionType.Normal;

        /// <summary>
        /// Adds the rule to the ObserverManager before the server spawns anything. Default
        /// conditions are copied onto each object when it is spawned, so it must be in place
        /// first; the server start is early enough (the room itself is global and unaffected).
        /// </summary>
        internal static void Install(NetworkManager nm)
        {
            if (nm == null || nm == _installedOn) return;
            var observers = nm.ObserverManager;
            if (observers == null || DefaultConditionsField == null)
            {
                Debug.LogError("[MatchSceneVisibility] Cannot reach the ObserverManager default conditions - " +
                               "players waiting in the room would receive the match's objects.");
                return;
            }
            if (DefaultConditionsField.GetValue(observers) is List<ObserverCondition> conditions)
            {
                bool present = false;
                foreach (var condition in conditions)
                    if (condition is MatchSceneVisibility) present = true;
                if (!present)
                {
                    var rule = CreateInstance<MatchSceneVisibility>();
                    rule.name = "MatchSceneVisibility (runtime)";
                    rule.hideFlags = HideFlags.DontSave;
                    conditions.Add(rule);
                }
                _installedOn = nm;
            }
        }

        /// <summary>Installs on every NetworkManager as soon as it exists.</summary>
        private sealed class Installer : MonoBehaviour
        {
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
            private static void Bootstrap()
            {
                var go = new GameObject("[MatchSceneVisibility]");
                DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideInHierarchy;
                go.AddComponent<Installer>();
            }

            private void Update()
            {
                var nm = InstanceFinder.NetworkManager;
                if (nm != null && nm != _installedOn) Install(nm);
            }
        }
    }
}
