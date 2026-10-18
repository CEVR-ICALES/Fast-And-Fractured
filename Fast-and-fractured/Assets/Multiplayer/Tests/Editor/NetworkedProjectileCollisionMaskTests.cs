using NUnit.Framework;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>
    /// Pins NetworkedProjectile's collision mask: a hand-written list once named a non-existent layer and missed
    /// Static, so mines and push bullets went through the level.
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
            // GetMask returns 0 for a typo'd layer name without any error.
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
            // The sweep must see exactly what a Rigidbody collision on that layer would touch.
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
            // A corrupt layer value gets the Default layer's mask.
            Assert.AreEqual(NetworkedProjectile.GetCollisionMaskForLayer(0),
                NetworkedProjectile.GetCollisionMaskForLayer(-1));
            Assert.AreEqual(NetworkedProjectile.GetCollisionMaskForLayer(0),
                NetworkedProjectile.GetCollisionMaskForLayer(99));
        }
    }
}
