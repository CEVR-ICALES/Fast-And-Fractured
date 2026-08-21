using System.Collections.Generic;
using FishNet.Component.Transforming.Beta;
using FishNet.Object;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>
    /// Guards the network configuration of the player vehicles.
    ///
    /// Every bug we chased in the vehicle motion work came from a SETTING silently drifting, not
    /// from an algorithm being wrong: a second smoother switching itself on because a field was
    /// populated, prediction being disabled on one variant, a prefab shipping without the smoother
    /// its siblings have. Those are invisible in code review and only show up as "it feels bad",
    /// which is exactly the kind of regression that survives for weeks.
    ///
    /// These tests assert the intended setup on every networked car prefab so a drifting field
    /// fails CI instead of shipping.
    /// </summary>
    public class NetworkedVehicleSetupTests
    {
        private const string CarPrefabFolder = "Assets/Multiplayer";

        private static IEnumerable<GameObject> NetworkedCarPrefabs()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { CarPrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                if (prefab.GetComponent<NetworkedVehicle>() == null) continue;
                yield return prefab;
            }
        }

        private static IEnumerable<TestCaseData> CarPrefabCases()
        {
            foreach (GameObject prefab in NetworkedCarPrefabs())
                yield return new TestCaseData(prefab).SetName($"Car_{prefab.name.Replace(" ", "_")}");
        }

        [Test]
        public void There_are_networked_car_prefabs_to_validate()
        {
            // If this fails the other tests are silently vacuous.
            Assert.That(new List<GameObject>(NetworkedCarPrefabs()), Is.Not.Empty,
                $"No prefabs with a NetworkedVehicle found under {CarPrefabFolder}.");
        }

        [TestCaseSource(nameof(CarPrefabCases))]
        public void Car_has_prediction_enabled_as_rigidbody(GameObject prefab)
        {
            NetworkObject nob = prefab.GetComponent<NetworkObject>();
            Assert.That(nob, Is.Not.Null, $"{prefab.name} has no NetworkObject.");

            Assert.That((bool)GetPrivateObject(nob, "_enablePrediction"), Is.True,
                $"{prefab.name}: prediction is off, so the owner would not simulate locally.");

            // PredictionType.Rigidbody — the car is a physics body, not a transform mover.
            Assert.That(System.Convert.ToInt32(GetPrivateObject(nob, "_predictionType")), Is.EqualTo(1),
                $"{prefab.name}: prediction type must be Rigidbody.");
        }

        [TestCaseSource(nameof(CarPrefabCases))]
        public void Car_smoothing_has_exactly_one_owner(GameObject prefab)
        {
            NetworkObject nob = prefab.GetComponent<NetworkObject>();
            var smoothers = prefab.GetComponentsInChildren<NetworkTickSmoother>(true);
            Transform graphical = GetPrivate<Transform>(nob, "_graphicalObject");

            Assert.That(smoothers, Is.Not.Empty,
                $"{prefab.name}: no NetworkTickSmoother, so nothing smooths the visuals between ticks.");

            // Both systems interpolate the same visuals; running them together is what produced the
            // visible tremble. _graphicalObject is what switches FishNet's built-in PredictionSmoother
            // on, so with a NetworkTickSmoother present it must stay empty.
            Assert.That(graphical, Is.Null,
                $"{prefab.name}: _graphicalObject is set ('{(graphical == null ? "" : graphical.name)}') while " +
                $"{smoothers.Length} NetworkTickSmoother(s) exist — two smoothers fight over the same transforms.");
        }

        [TestCaseSource(nameof(CarPrefabCases))]
        public void Car_visuals_and_turret_are_smoothed(GameObject prefab)
        {
            // The body and the turret are separate transforms; smoothing only one leaves the other
            // stepping at tick rate, which reads as the turret shaking against a smooth car.
            AssertChildSmoothed(prefab, "Visuals");

            if (prefab.transform.Find("Turret") != null)
                AssertChildSmoothed(prefab, "Turret");
        }

        [TestCaseSource(nameof(CarPrefabCases))]
        public void Car_body_is_a_non_kinematic_rigidbody(GameObject prefab)
        {
            var rb = prefab.GetComponent<Rigidbody>();
            Assert.That(rb, Is.Not.Null, $"{prefab.name}: prediction needs a Rigidbody on the vehicle root.");
            Assert.That(rb.isKinematic, Is.False,
                $"{prefab.name}: a kinematic body cannot be driven by the prediction simulation.");

            // Rigidbody interpolation would be a THIRD smoother on top of the tick smoother.
            Assert.That(rb.interpolation, Is.EqualTo(RigidbodyInterpolation.None),
                $"{prefab.name}: Rigidbody interpolation is '{rb.interpolation}', which double-smooths " +
                "motion that NetworkTickSmoother already handles.");
        }

        [TestCaseSource(nameof(CarPrefabCases))]
        public void Car_has_the_multiplayer_rigidbody_adapter(GameObject prefab)
        {
            // NetworkedVehicle drives physics through this adapter's PredictionRigidbody; without it
            // reconcile has nothing to restore and the car desyncs from the server.
            Assert.That(prefab.GetComponent<MultiplayerRigidbodyAdapter>(), Is.Not.Null,
                $"{prefab.name}: missing MultiplayerRigidbodyAdapter.");
        }

        private static void AssertChildSmoothed(GameObject prefab, string childName)
        {
            Transform child = prefab.transform.Find(childName);
            Assert.That(child, Is.Not.Null, $"{prefab.name}: expected a '{childName}' child.");
            Assert.That(child.GetComponent<NetworkTickSmoother>(), Is.Not.Null,
                $"{prefab.name}/{childName}: no NetworkTickSmoother, so it steps at tick rate while its siblings are smoothed.");
        }

        private static T GetPrivate<T>(object target, string field) where T : class
            => GetPrivateObject(target, field) as T;

        private static object GetPrivateObject(object target, string field)
        {
            var info = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(info, Is.Not.Null, $"FishNet field '{field}' not found — the version may have renamed it.");
            return info.GetValue(target);
        }
    }
}
