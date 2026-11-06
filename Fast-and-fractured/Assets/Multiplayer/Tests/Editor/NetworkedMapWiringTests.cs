using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>Checks the storm wiring of every generated networked map (the migration's prefab swaps used to drop it).</summary>
    public class NetworkedMapWiringTests
    {
        private const string MultiplayerFolder = "Assets/Multiplayer";

        private static IEnumerable<string> NetworkedMapPaths()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { MultiplayerFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.StartsWith("Networked", StringComparison.Ordinal) && name.EndsWith("Map", StringComparison.Ordinal))
                    yield return path;
            }
        }

        private static IEnumerable<TestCaseData> MapCases()
        {
            foreach (string path in NetworkedMapPaths())
                yield return new TestCaseData(path).SetName($"Map_{Path.GetFileNameWithoutExtension(path)}");
        }

        [Test]
        public void There_are_networked_maps_to_validate()
        {
            Assert.That(new List<string>(NetworkedMapPaths()), Is.Not.Empty,
                $"No Networked*Map scenes found under {MultiplayerFolder}; the other tests are vacuous.");
        }

        [TestCaseSource(nameof(MapCases))]
        public void Storm_is_wired_like_in_singleplayer(string scenePath)
        {
            // Map loads log their own content errors, also in singleplayer.
            LogAssert.ignoreFailingMessages = true;
            WithScene(scenePath, scene =>
            {
                var storms = FindInScene<SandstormController>(scene);
                Assert.That(storms, Is.Not.Empty, "The map has no SandstormController.");
                foreach (var storm in storms)
                {
                    Assert.That(Reference(storm, "sphereCenter"), Is.Not.Null,
                        $"{storm.name}: sphereCenter is empty, so the storm cannot pick where it spawns (UnassignedReferenceException).");
                }

                foreach (var level in FindInScene<LevelController>(scene))
                {
                    Assert.That(Reference(level, "_sandStormController"), Is.Not.Null,
                        $"{level.name}: no storm, so the level cannot time it and the AI cannot tell where it is.");
                }
            });
        }

        [TestCaseSource(nameof(MapCases))]
        public void Storm_and_lava_keep_the_singleplayer_maps_tuning(string scenePath)
        {
            // Prefab defaults instead of the map's values made the golf lava kill a car seconds into the match.
            LogAssert.ignoreFailingMessages = true;
            string singleplayerPath = SingleplayerSourceOf(scenePath);
            Assert.That(singleplayerPath, Is.Not.Null, $"No singleplayer map found for {scenePath}.");

            var expected = new Dictionary<string, float>();
            WithScene(singleplayerPath, scene => ReadTuning(scene, expected));
            var actual = new Dictionary<string, float>();
            WithScene(scenePath, scene => ReadTuning(scene, actual));

            Assert.That(expected, Is.Not.Empty, "Nothing to compare: no storm or lava found in the singleplayer map.");
            foreach (var entry in expected)
            {
                Assert.That(actual.ContainsKey(entry.Key), Is.True, $"{entry.Key} is missing from the networked map.");
                Assert.That(actual[entry.Key], Is.EqualTo(entry.Value).Within(1e-4f),
                    $"{entry.Key} differs from the singleplayer map.");
            }
        }

        private static readonly string[] StormTuning = { "sphereRadius", "maxCharacterKillTime", "colliderLessGrowMultiplicator" };
        private static readonly string[] LavaTuning = { "_delayBeforeRise", "_riseDuration", "_riseDistance" };

        private static void ReadTuning(Scene scene, Dictionary<string, float> into)
        {
            foreach (var storm in FindInScene<SandstormController>(scene))
                foreach (string field in StormTuning)
                    into[$"{storm.name}.{field}"] = new SerializedObject(storm).FindProperty(field).floatValue;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null || behaviour.GetType().Name != "RisingLava") continue;
                    foreach (string field in LavaTuning)
                        into[$"{behaviour.name}.{field}"] = new SerializedObject(behaviour).FindProperty(field).floatValue;
                }
        }

        private static string SingleplayerSourceOf(string networkedScenePath)
        {
            string name = Path.GetFileNameWithoutExtension(networkedScenePath).Substring("Networked".Length);
            foreach (string guid in AssetDatabase.FindAssets($"{name} t:Scene", new[] { "Assets/FastAndFractured" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return path;
            }
            return null;
        }

        private static UnityEngine.Object Reference(UnityEngine.Object owner, string field)
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(field);
            Assert.That(property, Is.Not.Null, $"{owner.GetType().Name}.{field} no longer exists; update this test.");
            return property.objectReferenceValue;
        }

        private static List<T> FindInScene<T>(Scene scene) where T : Component
        {
            var found = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<T>(true));
            return found;
        }

        /// <summary>Opens the scene additively (unless it is already open) and closes it afterwards.</summary>
        private static void WithScene(string scenePath, Action<Scene> check)
        {
            Scene alreadyOpen = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = !alreadyOpen.isLoaded;
            Scene scene = openedHere ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive) : alreadyOpen;
            try
            {
                check(scene);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }
    }
}
