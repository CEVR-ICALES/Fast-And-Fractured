using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Tests
{
    public class GhostShotTests
    {
        private const string PLAYER_PREFAB = "Assets/Multiplayer/Player/Prefabs/NetworkedPlayer.prefab";

        [Test]
        public void PlayerPrefabCarriesTheGhostShotOnItsOwnedRoot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB);
            Assert.IsNotNull(prefab);
            var ghost = prefab.GetComponent<GhostShot>();
            Assert.IsNotNull(ghost, "The shot lives on the player root: it survives the car's death and is owned by the player.");
            var serialized = new SerializedObject(ghost);
            Assert.IsNotNull(serialized.FindProperty("_trailMaterial").objectReferenceValue, "Without a trail nobody sees where the shot came from.");
            Assert.IsNotNull(serialized.FindProperty("_impactVfx").objectReferenceValue);
        }

        [Test]
        public void ShotWithoutADirectionIsRefused()
        {
            Assert.IsFalse(GhostShot.IsValidDirection(Vector3.zero));
            Assert.IsFalse(GhostShot.IsValidDirection(new Vector3(float.NaN, 0f, 1f)));
            Assert.IsTrue(GhostShot.IsValidDirection(Vector3.forward));
        }

        [Test]
        public void GhostPushIsWeakerThanAPushShotAndHasACooldown()
        {
            Assert.Less(GhostShot.FORCE_SCALE, 1f);
            Assert.Greater(GhostShot.COOLDOWN_SECONDS, 0f);
        }
    }
}
