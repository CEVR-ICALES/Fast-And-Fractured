using NUnit.Framework;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>
    /// Pins the collision-mask derivation in NetworkedProjectile, which exists because of the
    /// mine-through-the-floor bug (fixed 2026-08-25).
    ///
    /// The sweep used to name its layers by hand:
    ///     LayerMask.GetMask("Default", "Characters", "Ground", "Obstacles")
    /// No layer called "Obstacles" exists in this project, and GetMask contributes nothing,
    /// silently, for an unknown name — which hid that Static was missing. Static holds most of
    /// every map (722 colliders on NetworkedMountainVillageMap against Ground's 3), so mines and
    /// push bullets swept straight through the level.
    ///
    /// These tests exist so that cannot happen again, whatever way the mask is built in the future.
    /// </summary>
    public class NetworkedProjectileCollisionMaskTests
    {
        private const int ProjectileLayer = 8; // GameElementsColision, what the projectile prefabs ship on

        private static int Layer(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            Assert.GreaterOrEqual(layer, 0, $"Layer '{name}' does not exist in this project. If it was " +
                                            "renamed, update these tests AND read NetworkedProjectile's " +
                                            "mask derivation before touching anything else.");
            return layer;
        }

        [Test]
        public void GetMask_WithUnknownLayerName_IsSilentlyZero()
        {
            // The trap that caused the bug, kept as an executable warning: a typo'd layer name
            // returns 0 with no error, so a hand-written mask list fails invisibly.
            Assert.AreEqual(0, LayerMask.GetMask("Obstacles"));
            Assert.AreEqual(0, LayerMask.GetMask("This Layer Does Not Exist"));
        }

        [Test]
        public void Mask_CoversTheLayersTheMapIsMadeOf()
        {
            int mask = NetworkedProjectile.GetCollisionMaskForLayer(ProjectileLayer);

            Assert.IsNotEmpty(mask.ToString(), "mask is empty - the sweep would hit nothing at all");
            Assert.IsTrue((mask & (1 << Layer("Static"))) != 0,
                "Static missing: it holds most map geometry, bullets fall through the world");
            Assert.IsTrue((mask & (1 << Layer("Ground"))) != 0, "Ground missing");
            Assert.IsTrue((mask & (1 << Layer("Characters"))) != 0,
                "Characters missing: bullets would stop exploding on cars");
        }

        [Test]
        public void Mask_EqualsTheLayerCollisionMatrix_ForEveryLayer()
        {
            // The contract, not the implementation: whatever the projectile's layer is, the sweep
            // must see exactly what a real Rigidbody collision on that layer would touch. Derived
            // from the matrix, a renamed or newly relevant layer can never be lost to a typo.
            for (int layer = 0; layer < 32; layer++)
            {
                if (System.String.IsNullOrEmpty(LayerMask.LayerToName(layer))) continue;

                int expected = 0;
                for (int other = 0; other < 32; other++)
                {
                    if (!Physics.GetIgnoreLayerCollision(layer, other))
                        expected |= 1 << other;
                }

                Assert.AreEqual(expected, NetworkedProjectile.GetCollisionMaskForLayer(layer),
                    $"Mask for layer {layer} ({LayerMask.LayerToName(layer)}) diverged from the collision matrix");
            }
        }

        [Test]
        public void Mask_OutOfRangeLayer_FallsBackToDefaultSafely()
        {
            // The guard exists so a corrupt layer value cannot index out of bounds; it should
            // produce the Default layer's mask (layer 0 always exists).
            Assert.AreEqual(NetworkedProjectile.GetCollisionMaskForLayer(0),
                NetworkedProjectile.GetCollisionMaskForLayer(-1));
            Assert.AreEqual(NetworkedProjectile.GetCollisionMaskForLayer(0),
                NetworkedProjectile.GetCollisionMaskForLayer(99));
        }
    }
}
