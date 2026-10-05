using System.Collections.Generic;
using System.Reflection;
using FishNet.Object;
using NUnit.Framework;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>
    /// Map props that break (tennis nets, trees) are matched between peers by their place in the
    /// hierarchy, so every peer must find the same props in the same order.
    /// </summary>
    public class NetworkedWorldBreakablesTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created) if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        private GameObject Make(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent);
            else _created.Add(go);
            return go;
        }

        private static FixedJoint Joint(GameObject go, float breakForce)
        {
            go.AddComponent<Rigidbody>();
            var joint = go.AddComponent<FixedJoint>();
            joint.breakForce = breakForce;
            return joint;
        }

        [Test]
        public void Nets_are_found_in_the_same_order_whatever_order_they_were_created_in()
        {
            var court = Make("Court");
            var b = Joint(Make("Cell", court.transform), 500f);
            var a = Joint(Make("Cell", court.transform), 300f);    // same name: told apart by position
            var c = Joint(Make("Anchor", court.transform), 500f);

            NetworkedWorldBreakables.Collect(new[] { court }, out var joints, out _);

            Assert.AreEqual(new Joint[] { c, b, a }, joints);
            Assert.AreNotEqual(NetworkedWorldBreakables.PathKey(a.transform), NetworkedWorldBreakables.PathKey(b.transform));
        }

        [Test]
        public void Unbreakable_joints_and_networked_objects_are_left_out()
        {
            var map = Make("Map");
            var net = Joint(Make("Net", map.transform), 500f);
            Joint(Make("Post", map.transform), float.PositiveInfinity);
            var car = Make("Car", map.transform);
            car.AddComponent<NetworkObject>();
            Joint(Make("Trailer", car.transform), 500f);

            NetworkedWorldBreakables.Collect(new[] { map }, out var joints, out _);

            Assert.AreEqual(new Joint[] { net }, joints);
        }

        [Test]
        public void Every_destructible_tree_script_can_be_driven()
        {
            foreach (var type in new[] { typeof(DestructibleTree), typeof(DestructibleOakTree), typeof(DestructibleCarobTree) })
            {
                var go = Make(type.Name);
                var tree = (MonoBehaviour)go.AddComponent(type);
                Assert.IsTrue(DestructibleTreeHandle.IsTree(tree), $"{type.Name} no longer has _hasSwapped / normalTree / SwapTree()");
            }
        }

        [Test]
        public void A_restored_tree_is_broken_without_its_effects()
        {
            var go = Make("Oak");
            var normal = Make("Normal", go.transform);
            var tree = go.AddComponent<DestructibleOakTree>();
            typeof(DestructibleOakTree).GetField("normalTree", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tree, normal);

            NetworkedWorldBreakables.Collect(new[] { go }, out _, out var trees);
            Assert.AreEqual(1, trees.Length);
            Assert.IsFalse(trees[0].IsBroken);

            trees[0].Break(playEffects: false);

            Assert.IsTrue(trees[0].IsBroken);
            Assert.IsFalse(normal.activeSelf);
        }
    }
}
