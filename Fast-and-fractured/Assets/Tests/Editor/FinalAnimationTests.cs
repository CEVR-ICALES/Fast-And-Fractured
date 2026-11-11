using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace FastAndFractured.Tests
{
    /// <summary>One win and one lose cutscene per character, recoloured per skin.</summary>
    public class FinalAnimationTests
    {
        private const float MAX_CUTSCENE_NEAR_CLIP = 0.5f;

        [Test]
        public void GetFinalAnimation_UsesOverrideOnlyForItsSkin()
        {
            var data = ScriptableObject.CreateInstance<CharacterData>();
            var win = new GameObject("Win");
            var lose = new GameObject("Lose");
            var goldWin = new GameObject("GoldWin");
            try
            {
                data.WinAnimation = win;
                data.LoseAnimation = lose;
                data.WinAnimationOverrides.Add(new GameObjectStringPair { GameObject = goldWin, StringValue = "Pepe_2" });

                Assert.AreSame(win, data.GetFinalAnimation("Pepe_0", true));
                Assert.AreSame(goldWin, data.GetFinalAnimation("Pepe_2", true));
                Assert.AreSame(lose, data.GetFinalAnimation("Pepe_2", false));
            }
            finally
            {
                Object.DestroyImmediate(data);
                Object.DestroyImmediate(win);
                Object.DestroyImmediate(lose);
                Object.DestroyImmediate(goldWin);
            }
        }

        [Test]
        public void FinalAnimationSkins_SwapsListedMaterialsOnly()
        {
            var root = new GameObject("Cutscene");
            var original = new Material(Shader.Find("Hidden/InternalErrorShader"));
            var other = new Material(original);
            var gold = new Material(original);
            try
            {
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { original, other };
                var skins = root.AddComponent<FinalAnimationSkins>();
                var entry = new FinalAnimationSkins.SkinMaterials { skin = 2 };
                entry.swaps.Add(new FinalAnimationSkins.MaterialSwap { original = original, replacement = gold });
                skins.Skins.Add(entry);

                skins.Apply(1);
                Assert.AreSame(original, renderer.sharedMaterials[0]);

                skins.Apply(2);
                Assert.AreSame(gold, renderer.sharedMaterials[0]);
                Assert.AreSame(other, renderer.sharedMaterials[1]);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(other);
                Object.DestroyImmediate(gold);
            }
        }

        [Test]
        public void EveryCharacter_HasPlayableCutscenes()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (GameObject cutscene in new[] { data.WinAnimation, data.LoseAnimation })
                {
                    Assert.IsNotNull(cutscene, $"{data.name} is missing a win or lose cutscene.");
                    Assert.IsNotNull(cutscene.GetComponentInChildren<PlayableDirector>(true), $"{cutscene.name} has no PlayableDirector.");
                    Assert.IsNotNull(cutscene.GetComponent<FinalAnimationSkins>(), $"{cutscene.name} has no FinalAnimationSkins.");
                    // Blender cameras import with a 10 m near plane, which clips the close-ups.
                    foreach (Camera camera in cutscene.GetComponentsInChildren<Camera>(true))
                        Assert.Less(camera.nearClipPlane, MAX_CUTSCENE_NEAR_CLIP, $"{cutscene.name}/{camera.name} near clip plane.");
                }
            }
        }
    }
}
