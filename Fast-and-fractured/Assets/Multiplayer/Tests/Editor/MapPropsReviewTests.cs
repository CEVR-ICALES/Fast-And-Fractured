using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Utilities;

namespace FastAndFractured.Multiplayer.Tests
{
    public class MapPropsReviewTests
    {
        private const float TICK = 0.1f;
        private GameObject _timerHost;
        private TimerSystem _timers;

        [SetUp]
        public void SetUp()
        {
            _timers = TimerSystem.ExistingInstance;
            if (_timers != null) return;
            _timerHost = new GameObject("MatchTimers test TimerSystem");
            _timers = _timerHost.AddComponent<TimerSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_timerHost != null) Object.DestroyImmediate(_timerHost);
        }

        [Test]
        public void MegaChickenKeepsItsStateMachineAwayFromTheVehicleDriver()
        {
            Assert.IsTrue(typeof(IOwnsStateMachineTick).IsAssignableFrom(typeof(NetworkedMegaChickenBoss)),
                "Only NetworkedVehicle ticks a taken-over Controller: the chicken would never leave its jump.");
            Assert.IsFalse(typeof(IOwnsStateMachineTick).IsAssignableFrom(typeof(NetworkedVehicle)));
        }

        [TestCase(true, true, true)]
        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(false, false, false)]
        public void SuckingBuildingPullsOnlyTheCarsThisPeerSimulates(bool isServer, bool isLocallyOwned, bool expected)
        {
            Assert.AreEqual(expected, NetworkedSuckingBuilding.ShouldPull(isServer, isLocallyOwned));
        }

        [TestCase(true, true, false, true)]
        [TestCase(true, false, false, true)]
        [TestCase(false, true, true, true)]
        [TestCase(false, true, false, false)]
        [TestCase(false, false, true, false)]
        public void SuckingBuildingLaunchesOnlyWhatThisPeerSimulates(bool isServer, bool isCar, bool isLocallyOwned, bool expected)
        {
            Assert.AreEqual(expected, NetworkedSuckingBuilding.ShouldLaunch(isServer, isCar, isLocallyOwned));
        }

        [TestCase("Assets/FastAndFractured/SuckingBuildings/Prefabs/SuckingBuildingPrefab.prefab")]
        [TestCase("Assets/FastAndFractured/SuckingBuildings/Prefabs/BigSuckingBuildingPrefab Variant.prefab")]
        [TestCase("Assets/FastAndFractured/SuckingBuildings/Prefabs/MediumSuckingBuildingPrefab Variant.prefab")]
        [TestCase("Assets/FastAndFractured/SuckingBuildings/Prefabs/SmallSuckingBuildingPrefab Variant.prefab")]
        public void EverySuckingBuildingHasANetworkedVariantThatLaunchesInsteadOfKilling(string singleplayerPath)
        {
            var singleplayer = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(singleplayerPath);
            Assert.IsNotNull(singleplayer, singleplayerPath);
            var networked = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Multiplayer/Props/Networked{singleplayer.name}.prefab");
            Assert.IsNotNull(networked, $"Networked{singleplayer.name} is missing: the scene migration falls back to the base building and loses its size.");
            Assert.AreEqual(singleplayer, UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(networked));
            Assert.IsNotNull(networked.GetComponent<NetworkedSuckingBuilding>());
            Assert.IsNotNull(networked.GetComponentInChildren<ParabolicImpulse>(true));
            Assert.IsNull(networked.GetComponentInChildren<NetworkedTrampoline>(true), "A server-only launcher would leave the owner pulling its car back in.");
            Assert.IsNull(networked.GetComponentInChildren<NetworkedBuildingDeadZone>(true), "The building no longer kills.");
        }

        [Test]
        public void SuckingBuildingKeepsTheSingleplayerPullOnClients()
        {
            var property = typeof(NetworkedSuckingBuilding).GetProperty("KeepWrappedOnClients",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(property);
            var go = new GameObject("Sucking building test");
            try
            {
                go.SetActive(false);
                var wrapper = go.AddComponent<NetworkedSuckingBuilding>();
                Assert.IsTrue((bool)property.GetValue(wrapper));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void MatchTimerRunsItsActionOnlyWhenTheDelayElapses()
        {
            int calls = 0;
            var timer = MatchTimers.After(TICK * 3, () => calls++);
            Assert.IsNotNull(timer);
            _timers.Tick(TICK * 2);
            Assert.AreEqual(0, calls);
            _timers.Tick(TICK * 2);
            Assert.AreEqual(1, calls);
            Assert.IsFalse(MatchTimers.IsRunning(timer));
        }

        [Test]
        public void PausedMatchTimerAccumulatesInsteadOfRestarting()
        {
            int calls = 0;
            var timer = MatchTimers.After(TICK * 4, () => calls++);
            _timers.Tick(TICK * 3);
            timer.PauseTimer();
            _timers.Tick(TICK * 10);
            Assert.AreEqual(0, calls, "A paused platform countdown must not advance.");
            timer.ResumeTimer();
            _timers.Tick(TICK * 2);
            Assert.AreEqual(1, calls, "Resuming continues from where it stopped, as BreakablePlatform does.");
        }

        [Test]
        public void StoppedMatchTimerNeverRunsAndClearsItsHandle()
        {
            int calls = 0;
            var timer = MatchTimers.After(TICK, () => calls++);
            MatchTimers.Stop(ref timer);
            Assert.IsNull(timer);
            _timers.Tick(TICK * 5);
            Assert.AreEqual(0, calls);
            Assert.DoesNotThrow(() => MatchTimers.Stop(ref timer));
        }

        [Test]
        public void MapPropsNoLongerUseCoroutines()
        {
            foreach (var type in new[] { typeof(NetworkedBreakablePlatform), typeof(NetworkedMegaChickenBoss),
                         typeof(NetworkedDeadWallsBehaviour), typeof(NetworkedPointExplosion) })
            {
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    Assert.AreNotEqual(typeof(System.Collections.IEnumerator), method.ReturnType, $"{type.Name}.{method.Name}");
            }
        }
    }
}
