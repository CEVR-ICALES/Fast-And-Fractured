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
    /// Only a match's players see its objects: an object in a scene is visible to the connections that have that
    /// scene loaded; objects outside match scenes stay visible to everyone. Installed as a default ObserverManager
    /// condition.
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

        /// <summary>Adds the rule before the server spawns anything: default conditions are copied on spawn.</summary>
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
