using System.Collections.Generic;
using System.Reflection;
using FishNet.Component.Ownership;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Serializing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Outcome = FastAndFractured.Multiplayer.NetworkedProjectile.SurfaceHitOutcome;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>
    /// The rules a networked shot follows, pinned against the values that broke them (ShootingProbe
    /// measurements, 2026-09-30):
    ///   * bounce strength multiplied the reflected velocity (50x): push shots and mines left at
    ///     ~1500 m/s after their first bounce and "teleported" 12-47 km in one frame;
    ///   * lobbed shots were given a straight-bullet lifetime (Range / speed) and exploded in the air
    ///     before landing, so mines never rested;
    ///   * the server checked the cadence against its own timer, one latency late, and rejected up to
    ///     half of the shots of a client firing exactly on cadence.
    /// </summary>
    public class NetworkedShootingRulesTests
    {
        // Values of the shipped characters (CharacterData): all four share them.
        private const float PushRange = 15f, PushAngle = 40f, MineRange = 10f, MineAngle = 40f;
        private const float GravityMultiplier = 30f, BounceForce = 50f;
        private const int PushBounces = 2;

        // ---------------------------------------------------------------- bounces and contacts

        [Test]
        public void Push_shot_bounces_until_its_last_bounce_then_explodes()
        {
            int used = 0;
            Assert.AreEqual(Outcome.Bounce, NetworkedProjectile.ResolveSurfaceHit(ref used, PushBounces, 0f));
            Assert.AreEqual(Outcome.Explode, NetworkedProjectile.ResolveSurfaceHit(ref used, PushBounces, 0f),
                "PushBulletBehaviour explodes on the contact that spends its last bounce");
        }

        [Test]
        public void Mine_rests_on_its_first_contact()
        {
            int used = 0;
            Assert.AreEqual(Outcome.Rest, NetworkedProjectile.ResolveSurfaceHit(ref used, 0, 5f));
        }

        [Test]
        public void Normal_bullet_ends_on_its_first_contact()
        {
            int used = 0;
            Assert.AreEqual(Outcome.Explode, NetworkedProjectile.ResolveSurfaceHit(ref used, 0, 0f));
        }

        [Test]
        public void Bounce_adds_the_strength_along_the_normal_instead_of_multiplying_the_speed()
        {
            Vector3 incoming = new Vector3(30f, -40f, 0f); // 50 m/s, hitting flat ground
            Vector3 after = NetworkedProjectile.BounceVelocity(incoming, Vector3.up, BounceForce);

            Assert.AreEqual(30f, after.x, 1e-4f, "motion along the surface is kept");
            Assert.AreEqual(BounceForce, after.y, 1e-4f, "the bounce launches off the surface at the configured strength");
            Assert.Less(after.magnitude, incoming.magnitude + BounceForce,
                "a bounce can never multiply the speed (the old rule left at ~1500 m/s)");
        }

        // ---------------------------------------------------------------- lifetime

        [Test]
        public void Straight_bullet_lives_for_its_range()
        {
            float lifetime = NetworkedProjectile.FlightLifetimeSeconds(new Vector3(0f, 0f, 120f), 150f, lobbed: false);
            Assert.AreEqual(1.25f, lifetime, 1e-4f);
        }

        [TestCase(PushRange, PushAngle)]
        [TestCase(MineRange, MineAngle)]
        public void Lobbed_shot_outlives_its_whole_arc(float range, float angleDegrees)
        {
            // The launch PushShootHandle computes, and the time that arc takes to come back down.
            float gravity = Mathf.Abs(Physics.gravity.y * GravityMultiplier);
            float speed = Mathf.Sqrt(range * gravity / Mathf.Sin(2f * angleDegrees * Mathf.Deg2Rad));
            float flightTime = 2f * speed * Mathf.Sin(angleDegrees * Mathf.Deg2Rad) / gravity;
            Vector3 launch = new Vector3(0f, speed * Mathf.Sin(angleDegrees * Mathf.Deg2Rad), speed * Mathf.Cos(angleDegrees * Mathf.Deg2Rad));

            float straightRule = range / speed;
            Assert.Less(straightRule, flightTime, "sanity: the old straight-bullet rule ends the shot in mid-air");

            float lifetime = NetworkedProjectile.FlightLifetimeSeconds(launch, range, lobbed: true);
            Assert.Greater(lifetime, flightTime * (PushBounces + 1),
                "a lobbed shot must be able to land and use its bounces before any timeout");
        }

        // ---------------------------------------------------------------- server validation

        // Server-side cadence: a rate limit on the server's real-time clock (TryTakeShotCredit).

        private static int AcceptedShots(float cooldownSeconds, params double[] arrivalTimes)
        {
            var credit = new NetworkedShoot.ShotCredit();
            int accepted = 0;
            foreach (double t in arrivalTimes)
                if (NetworkedShoot.TryTakeShotCredit(ref credit, t, cooldownSeconds)) accepted++;
            return accepted;
        }

        [Test]
        public void First_shot_is_always_within_cadence()
        {
            Assert.AreEqual(1, AcceptedShots(0.5f, 100.0));
        }

        [Test]
        public void Shots_on_cadence_are_accepted_despite_arrival_jitter()
        {
            // Pepe, 0.5 s: fired every 0.5 s, arriving up to 0.2 s early or late.
            var arrivals = new List<double>();
            for (int i = 0; i < 40; i++) arrivals.Add(i * 0.5 + (i % 2 == 0 ? 0.2 : -0.2));
            Assert.AreEqual(40, AcceptedShots(0.5f, arrivals.ToArray()));
        }

        [Test]
        public void Two_shots_merged_by_a_server_hitch_are_accepted()
        {
            // A host hitch delivers two shots fired 0.5 s apart in the same server frame.
            Assert.AreEqual(3, AcceptedShots(0.5f, 10.0, 11.5, 11.5));
        }

        [Test]
        public void A_client_hitch_between_shots_does_not_reject_the_next_one()
        {
            // The 2026-09-30 case: 4 shots 0.5 s apart, a 197 ms client hitch between the 3rd and
            // the 4th. The shooter's tick clock dropped ticks there; real arrival times did not.
            Assert.AreEqual(4, AcceptedShots(0.5f, 31.717, 32.196, 32.727, 33.238));
        }

        [Test]
        public void Firing_faster_than_the_weapon_is_limited_to_its_rate()
        {
            // A modified client firing every 0.3 s for 30 s with a 0.5 s weapon.
            var arrivals = new List<double>();
            for (double t = 0; t < 30.0; t += 0.3) arrivals.Add(t);
            int accepted = AcceptedShots(0.5f, arrivals.ToArray());
            Assert.That(accepted, Is.LessThanOrEqualTo(30.0 / 0.5 + 2), "sustained rate above the weapon's");
            Assert.That(accepted, Is.GreaterThanOrEqualTo(30.0 / 0.5 - 1), "legitimate shots in between must still land");
        }

        [Test]
        public void A_push_cannot_be_fired_twice_in_a_row()
        {
            // Push, 6 s cooldown: the allowance is 0.5 s, not a whole shot.
            Assert.AreEqual(1, AcceptedShots(6f, 50.0, 51.0));
            Assert.AreEqual(2, AcceptedShots(6f, 50.0, 55.6), "0.4 s early is within the allowance");
        }

        [Test]
        public void Launch_tick_must_be_recent_and_not_in_the_future()
        {
            // Both on the shooter's own clock: its LocalTick when it fired, and the LocalTick
            // stamped on the packet that carried the shot.
            Assert.IsTrue(NetworkedShoot.IsPlausibleLaunchTick(995u, 1000u, out _), "sent a few ticks after firing: fine");
            Assert.IsTrue(NetworkedShoot.IsPlausibleLaunchTick(1000u, 1000u, out _), "sent the tick it was fired: fine");
            Assert.IsFalse(NetworkedShoot.IsPlausibleLaunchTick(1011u, 1000u, out _), "from the future");
            Assert.IsFalse(NetworkedShoot.IsPlausibleLaunchTick(940u, 1000u, out _), "older than a second");
        }

        [Test]
        public void Shooter_launch_is_used_only_while_plausible_for_the_car()
        {
            Vector3 gun = new Vector3(10f, 5f, 0f);
            Vector3 velocity = new Vector3(0f, 0f, 120f);

            Assert.IsTrue(NetworkedShoot.IsPlausibleLaunch(gun + Vector3.forward * 2f, velocity, gun, velocity));
            Assert.IsFalse(NetworkedShoot.IsPlausibleLaunch(gun + Vector3.forward * 10f, velocity, gun, velocity), "too far from the gun");
            Assert.IsFalse(NetworkedShoot.IsPlausibleLaunch(gun, velocity * 2f, gun, velocity), "twice the weapon's speed");
            Assert.IsFalse(NetworkedShoot.IsPlausibleLaunch(gun, Quaternion.Euler(0f, 60f, 0f) * velocity, gun, velocity), "aimed elsewhere");
            Assert.IsFalse(NetworkedShoot.IsPlausibleLaunch(new Vector3(float.NaN, 0f, 0f), velocity, gun, velocity), "NaN");
        }

        [Test]
        public void Launch_parameters_survive_the_spawn_payload()
        {
            var sent = new NetworkShootParams
            {
                Pooltype = Enums.Pooltype.PUSH_BULLET_MELON,
                InitialVelocity = new Vector3(1.5f, 42f, -7.25f),
                Range = 15f, Damage = 22f, PushForce = 130000f, ExplosionRadius = 7f,
                ExplosionCenterOffset = new Vector3(0f, 1f, 0f), CustomGravity = new Vector3(0f, -294.3f, 0f),
                BouncingNum = 2, BouncingStrenght = 50f, TimeToExplode = 5f, IsMine = true,
            };
            var writer = new Writer();
            NetworkedPredictedProjectileSpawn.WriteParams(writer, sent);
            var reader = new Reader(writer.GetArraySegment(), null);
            NetworkShootParams received = NetworkedPredictedProjectileSpawn.ReadParams(reader);

            Assert.AreEqual(sent.Pooltype, received.Pooltype);
            Assert.AreEqual(sent.InitialVelocity, received.InitialVelocity);
            Assert.AreEqual(sent.CustomGravity, received.CustomGravity);
            Assert.AreEqual(sent.ExplosionCenterOffset, received.ExplosionCenterOffset);
            Assert.AreEqual(sent.Range, received.Range);
            Assert.AreEqual(sent.Damage, received.Damage);
            Assert.AreEqual(sent.PushForce, received.PushForce);
            Assert.AreEqual(sent.ExplosionRadius, received.ExplosionRadius);
            Assert.AreEqual(sent.BouncingNum, received.BouncingNum);
            Assert.AreEqual(sent.BouncingStrenght, received.BouncingStrenght);
            Assert.AreEqual(sent.TimeToExplode, received.TimeToExplode);
            Assert.AreEqual(sent.IsMine, received.IsMine);
        }
    }

    /// <summary>
    /// Every character's weapons must be wired for networked, client-predicted shots: the prefab
    /// each weapon spawns exists, is a NetworkedProjectile with its visuals assigned, can be spawned
    /// by a client, and is registered with the NetworkManager. And whatever prediction settings a
    /// projectile prefab ships with, the runtime normalisation must leave every one of them the same
    /// - the drift between them (prediction on for some, off or Rigidbody-typed for others) is what
    /// made each character's shots behave differently.
    /// </summary>
    public class NetworkedWeaponSetupTests
    {
        private static readonly string[] Characters = { "Pepe", "Josefino", "Carme", "MariaAntonia" };
        private static readonly string[] WeaponFields = { "_networkedNormalBulletPrefab", "_networkedPushBulletPrefab", "_networkedMinePrefab" };
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [Test]
        public void Josefino_heal_amount_is_readable_by_the_server()
        {
            // The server applies Josefino's heal from this field (the owner's reported endurance
            // is read before the heal runs). A rename would silently drop the heal in multiplayer.
            FieldInfo field = typeof(JosefinoUniqueAbility).GetField("enduranceRecoveryAmount", Private);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(typeof(float)));
        }

        private static IEnumerable<TestCaseData> WeaponCases()
        {
            foreach (string character in Characters)
                foreach (string field in WeaponFields)
                    yield return new TestCaseData(character, field).SetName($"{character}_{field.Trim('_')}");
        }

        private static GameObject WeaponPrefab(string character, string field)
        {
            var car = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Multiplayer/Vehicles/Prefabs/Networked{character}GameplayCar Variant.prefab");
            Assert.IsNotNull(car, $"{character}: networked car prefab not found");
            var shoot = car.GetComponentInChildren<NetworkedShoot>(true);
            Assert.IsNotNull(shoot, $"{character}: car has no NetworkedShoot");
            return typeof(NetworkedShoot).GetField(field, Private).GetValue(shoot) as GameObject;
        }

        [TestCaseSource(nameof(WeaponCases))]
        public void Weapon_spawns_a_client_predictable_networked_projectile(string character, string field)
        {
            GameObject prefab = WeaponPrefab(character, field);
            Assert.IsNotNull(prefab, $"{character}.{field} is not assigned");

            Assert.IsNotNull(prefab.GetComponent<NetworkObject>(), $"{prefab.name}: no NetworkObject");
            var projectile = prefab.GetComponent<NetworkedProjectile>();
            Assert.IsNotNull(projectile, $"{prefab.name}: no NetworkedProjectile");
            Assert.IsNotNull(typeof(NetworkedProjectile).GetField("_visuals", Private).GetValue(projectile),
                $"{prefab.name}: _visuals is not assigned, the shot could not be hidden on impact");

            var predicted = prefab.GetComponent<NetworkedPredictedProjectileSpawn>();
            Assert.IsNotNull(predicted, $"{prefab.name}: no NetworkedPredictedProjectileSpawn - the shooter would wait a round trip to see it");
            Assert.IsTrue((bool)typeof(PredictedSpawn).GetField("_allowSpawning", Private).GetValue(predicted),
                $"{prefab.name}: clients are not allowed to spawn it");
        }

        [TestCaseSource(nameof(WeaponCases))]
        public void Weapon_prefab_is_registered_with_the_network_manager(string character, string field)
        {
            GameObject prefab = WeaponPrefab(character, field);
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/DefaultPrefabObjects.asset");
            Assert.IsNotNull(registry, "Assets/DefaultPrefabObjects.asset not found");

            bool found = false;
            for (int i = 0; i < registry.GetObjectCount(); i++)
                found |= registry.GetObject(true, i) == prefab.GetComponent<NetworkObject>();
            Assert.IsTrue(found, $"{prefab.name} is not in DefaultPrefabObjects: spawning it would fail on the other peers");
        }

        [TestCaseSource(nameof(WeaponCases))]
        public void Runtime_normalisation_leaves_every_projectile_with_the_same_network_settings(string character, string field)
        {
            GameObject instance = Object.Instantiate(WeaponPrefab(character, field));
            try
            {
                var projectile = instance.GetComponent<NetworkedProjectile>();
                typeof(NetworkedProjectile).GetMethod("NormalizeRuntimeNetworkSettings", Private).Invoke(projectile, null);

                var nob = instance.GetComponent<NetworkObject>();
                Assert.IsFalse((bool)typeof(NetworkObject).GetField("_enablePrediction", Private).GetValue(nob),
                    "prediction must be off: every peer flies the shot itself, nothing is reconciled");
                Assert.AreEqual(0, System.Convert.ToInt32(typeof(NetworkObject).GetField("_predictionType", Private).GetValue(nob)),
                    "prediction type must be Other");
                Assert.IsNull(typeof(NetworkObject).GetField("_graphicalObject", Private).GetValue(nob),
                    "no FishNet graphical smoothing: LateUpdate draws the shot between ticks");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
