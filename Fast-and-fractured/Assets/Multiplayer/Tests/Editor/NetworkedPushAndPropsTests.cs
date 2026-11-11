using System.Collections.Generic;
using FishNet.Object;
using NUnit.Framework;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>
    /// Push shots push like singleplayer's, and the loose map props are matched between peers by
    /// their place in the hierarchy.
    /// </summary>
    public class NetworkedPushAndPropsTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created) if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        [Test]
        public void DirectHit_PushesTowardsTheCarNotStraightUp()
        {
            // The shot lands inside the car's box, beside its centre.
            var car = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(2f, 1.5f, 4f));
            Vector3 impact = new Vector3(-0.8f, 1f, 0f);

            Vector3 direction = NetworkedProjectile.PushDirection(impact, impact, 3f, car);

            Assert.AreEqual(1f, direction.magnitude, 1e-4f);
            Assert.Greater(direction.x, 0.3f, "away from the impact, across the car");
            Assert.Less(direction.y, 0.95f, "not a vertical launch");
        }

        [Test]
        public void NearMiss_PushesAwayFromTheBlast()
        {
            var car = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(2f, 1.5f, 4f));
            Vector3 impact = new Vector3(0f, 0.2f, -4f);   // on the ground behind the car

            Vector3 direction = NetworkedProjectile.PushDirection(impact, impact, 3f, car);

            Assert.Greater(direction.z, 0.5f);
        }

        private GameObject Make(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent);
            else _created.Add(go);
            return go;
        }

        [Test]
        public void LooseProps_DynamicBodiesInHierarchyOrder_WithoutNetworkedOrTrampolineBodies()
        {
            var root = Make("Props");
            var coneB = Make("Cone", root.transform); coneB.AddComponent<Rigidbody>();
            var coneA = Make("Brick", root.transform); coneA.AddComponent<Rigidbody>();
            var still = Make("Kinematic", root.transform); still.AddComponent<Rigidbody>().isKinematic = true;
            var networked = Make("Networked", root.transform);
            networked.AddComponent<NetworkObject>();
            Make("Part", networked.transform).AddComponent<Rigidbody>();

            var bodies = NetworkedLooseProps.Collect(new[] { root });

            Assert.AreEqual(2, bodies.Length);
            Assert.AreSame(coneA.GetComponent<Rigidbody>(), bodies[0], "sorted by path: Brick before Cone");
            Assert.AreSame(coneB.GetComponent<Rigidbody>(), bodies[1]);
        }
    }
}
