using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace FastAndFractured.Tests
{
    public sealed class DebugMenuTests
    {
        private const float TOLERANCE = 0.001f;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void DestroyCreated()
        {
            foreach (UnityEngine.Object created in _created)
            {
                if (created == null) continue;
                if (created is GameObject go && go.TryGetComponent(out Volume volume) && volume.HasInstantiatedProfile())
                    UnityEngine.Object.DestroyImmediate(volume.profile);
                UnityEngine.Object.DestroyImmediate(created);
            }
            _created.Clear();
        }

        [TestCase(true, false, false, false, true)]
        [TestCase(false, true, false, false, true)]
        [TestCase(false, false, true, false, true)]
        [TestCase(false, false, false, true, true)]
        [TestCase(false, false, false, false, false)]
        public void MenuIsOnlyThereWhereItIsWanted(bool isEditor, bool isDebugBuild, bool enabledInBuilds, bool asked, bool expected)
        {
            string[] commandLine = asked ? new[] { "game.exe", DebugMenuSwitch.COMMAND_LINE_FLAG } : new[] { "game.exe" };
            Assert.AreEqual(expected, DebugMenuSwitch.IsAvailable(isEditor, isDebugBuild, enabledInBuilds, commandLine));
        }

        [Test]
        public void MenuCopesWithNoCommandLine()
        {
            Assert.IsFalse(DebugMenuSwitch.IsAvailable(false, false, false, null));
        }

        [TestCase(0, 6, 1, 1)]
        [TestCase(5, 6, 1, 0)]
        [TestCase(0, 6, -1, 5)]
        [TestCase(-1, 6, 1, 0)]
        [TestCase(-1, 6, -1, 5)]
        [TestCase(0, 0, 1, DebugVolumes.NONE)]
        public void VolumeStepGoesRoundTheList(int current, int count, int step, int expected)
        {
            Assert.AreEqual(expected, DebugVolumes.Step(current, count, step));
        }

        [Test]
        public void ShowingAVolumeHidesTheOneBefore()
        {
            global::VolumeManager manager = ManagerWith("BV_Morning", "BV_Night");

            Assert.IsTrue(DebugVolumes.Show(manager, 1));

            Assert.AreEqual(1, DebugVolumes.CurrentIndex(manager));
            Assert.AreEqual("BV_Night", DebugVolumes.NameOf(manager, 1));
            Assert.IsTrue(manager.VolumeList[1].activeSelf);
            Assert.IsTrue(DebugVolumes.Show(manager, 0));
            Assert.IsFalse(manager.VolumeList[1].activeSelf, "Only one mood shows at a time.");
            Assert.AreSame(manager.VolumeList[0].GetComponent<Volume>(), DebugVolumes.VolumesOf(manager)[0]);
            Assert.IsEmpty(DebugVolumes.VolumesOf(null));
        }

        [Test]
        public void AVolumeThatIsNotInTheListChangesNothing()
        {
            global::VolumeManager manager = ManagerWith("BV_Morning", "BV_Night");
            DebugVolumes.Show(manager, 0);

            Assert.IsFalse(DebugVolumes.Show(manager, 2));
            Assert.IsFalse(DebugVolumes.Show(manager, -1));
            Assert.IsFalse(DebugVolumes.Show(null, 0));

            Assert.AreEqual(0, DebugVolumes.CurrentIndex(manager));
            Assert.AreEqual(DebugVolumes.NONE, DebugVolumes.CurrentIndex(null));
        }

        [Test]
        public void FreeCameraFliesWhereItLooks()
        {
            var pose = new DebugFreeCameraPose { yaw = 90f };

            pose = DebugFreeCamera.Fly(pose, Vector3.forward, Vector2.zero, 10f, 0.5f);

            Assert.AreEqual(5f, pose.position.x, TOLERANCE);
            Assert.AreEqual(0f, pose.position.z, TOLERANCE);
        }

        [Test]
        public void FreeCameraClimbsStraightUpWhateverItLooksAt()
        {
            var pose = new DebugFreeCameraPose { pitch = 60f };

            pose = DebugFreeCamera.Fly(pose, Vector3.up, Vector2.zero, 10f, 1f);

            Assert.AreEqual(10f, pose.position.y, TOLERANCE);
            Assert.AreEqual(0f, pose.position.z, TOLERANCE);
        }

        [Test]
        public void FreeCameraNeverGoesFasterOnADiagonal()
        {
            var pose = DebugFreeCamera.Fly(new DebugFreeCameraPose(), new Vector3(1f, 1f, 1f), Vector2.zero, 10f, 1f);

            Assert.AreEqual(10f, pose.position.magnitude, TOLERANCE);
        }

        [Test]
        public void FreeCameraNeverTurnsUpsideDown()
        {
            var pose = DebugFreeCamera.Fly(new DebugFreeCameraPose(), Vector3.zero, new Vector2(0f, 500f), 10f, 1f);
            Assert.AreEqual(-DebugFreeCamera.MAX_PITCH, pose.pitch, TOLERANCE, "Mouse up looks up.");

            pose = DebugFreeCamera.Fly(pose, Vector3.zero, new Vector2(725f, -1000f), 10f, 1f);
            Assert.AreEqual(DebugFreeCamera.MAX_PITCH, pose.pitch, TOLERANCE);
            Assert.AreEqual(5f, pose.yaw, TOLERANCE, "The heading stays within one turn.");
        }

        [Test]
        public void FreeCameraStartsFromTheViewItTakesOver()
        {
            Transform view = Track(new GameObject("View")).transform;
            view.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(-30f, 200f, 0f));

            DebugFreeCameraPose pose = DebugFreeCamera.PoseOf(view);

            Assert.AreEqual(new Vector3(1f, 2f, 3f), pose.position);
            Assert.AreEqual(200f, pose.yaw, TOLERANCE);
            Assert.AreEqual(-30f, pose.pitch, TOLERANCE);
        }

        [Test]
        public void FreeCameraSpeedStaysInItsRange()
        {
            Assert.Greater(DebugFreeCamera.SpeedAfter(20f, 1f), 20f);
            Assert.Less(DebugFreeCamera.SpeedAfter(20f, -1f), 20f);
            Assert.AreEqual(DebugFreeCamera.MAX_SPEED, DebugFreeCamera.SpeedAfter(20f, 100f), TOLERANCE);
            Assert.AreEqual(DebugFreeCamera.MIN_SPEED, DebugFreeCamera.SpeedAfter(20f, -100f), TOLERANCE);
            Assert.AreEqual(DebugFreeCamera.FAST_FACTOR, DebugFreeCamera.SpeedFactor(true, true), "Fast wins when both are held.");
            Assert.AreEqual(DebugFreeCamera.SLOW_FACTOR, DebugFreeCamera.SpeedFactor(false, true));
            Assert.AreEqual(1f, DebugFreeCamera.SpeedFactor(false, false));
        }

        [Test]
        public void FreeCameraGivesTheCameraBackAsItWas()
        {
            Camera camera = Track(new GameObject("Camera")).AddComponent<Camera>();
            camera.fieldOfView = 55f;
            var freeCamera = new DebugFreeCamera();

            Assert.IsTrue(freeCamera.Enter(camera));
            Assert.IsFalse(freeCamera.Enter(camera), "It is already flying.");
            freeCamera.FieldOfView = 90f;
            freeCamera.Tick(Vector3.forward, Vector2.zero, 1f, 1f);
            Assert.AreEqual(90f, camera.fieldOfView, TOLERANCE);
            Assert.AreEqual(DebugFreeCamera.DEFAULT_SPEED, camera.transform.position.z, TOLERANCE);

            freeCamera.Exit();

            Assert.IsFalse(freeCamera.Active);
            Assert.AreEqual(55f, camera.fieldOfView, TOLERANCE);
        }

        [Test]
        public void FreeCameraStopsWhenItsCameraIsGone()
        {
            Camera camera = Track(new GameObject("Camera")).AddComponent<Camera>();
            var freeCamera = new DebugFreeCamera();
            freeCamera.Enter(camera);

            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            freeCamera.Tick(Vector3.forward, Vector2.zero, 1f, 1f);

            Assert.IsFalse(freeCamera.Active);
            Assert.IsFalse(new DebugFreeCamera().Enter(null));
        }

        [Test]
        public void HidingTheHudOnlyTouchesWhatWasShowing()
        {
            Canvas shown = Track(new GameObject("Shown")).AddComponent<Canvas>();
            Canvas alreadyOff = Track(new GameObject("AlreadyOff")).AddComponent<Canvas>();
            alreadyOff.enabled = false;
            var child = new GameObject("Nested");
            child.transform.SetParent(shown.transform, false);
            Canvas nested = child.AddComponent<Canvas>();
            var hud = new DebugHud();

            hud.Hide(new[] { shown, alreadyOff, nested, null });

            Assert.IsTrue(hud.Hidden);
            Assert.IsFalse(shown.enabled);
            Assert.IsTrue(nested.enabled, "A nested canvas goes with its root.");

            hud.Show();

            Assert.IsFalse(hud.Hidden);
            Assert.IsTrue(shown.enabled);
            Assert.IsFalse(alreadyOff.enabled, "What the game had switched off stays off.");
        }

        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(false, false, false)]
        public void CheatsNeedAMatchThatIsNotOnTheNetwork(bool hasMatch, bool inNetworkSession, bool expected)
        {
            Assert.AreEqual(expected, DebugCheats.Allowed(hasMatch, inNetworkSession));
        }

        [Test]
        public void KillingTheRivalsSparesThePlayerAndTheAlreadyDead()
        {
            GameObject player = Track(new GameObject("Player"));
            GameObject rival = Track(new GameObject("Rival"));
            GameObject gone = Track(new GameObject("Gone"));
            gone.SetActive(false);

            Assert.IsTrue(DebugCheats.IsRivalToKill(rival, player, false));
            Assert.IsFalse(DebugCheats.IsRivalToKill(player, player, false));
            Assert.IsFalse(DebugCheats.IsRivalToKill(rival, player, true), "Its death is already playing.");
            Assert.IsFalse(DebugCheats.IsRivalToKill(gone, player, false));
            Assert.IsFalse(DebugCheats.IsRivalToKill(null, player, false));
        }

        [Test]
        public void TheMenuNeverShrinksBelowItsDesignSize()
        {
            Assert.AreEqual(1f, DebugMenu.GuiScale(800), TOLERANCE, "Handheld screens keep readable text.");
            Assert.AreEqual(1f, DebugMenu.GuiScale(1080), TOLERANCE);
            Assert.AreEqual(2f, DebugMenu.GuiScale(2160), TOLERANCE);
        }

        [Test]
        public void TheInfoLineSaysWhatIsRunning()
        {
            string line = DebugMenu.InfoLine(50f, new[] { "Low", "High" }, 1, 1920, 1080, "Golf", "1.71.8");

            StringAssert.Contains("50 fps", line);
            StringAssert.Contains("20.0 ms", line.Replace(',', '.'));
            StringAssert.Contains("High", line);
            StringAssert.Contains("1920x1080", line);
            StringAssert.Contains("Golf", line);
            StringAssert.Contains("v1.71.8", line);
            StringAssert.Contains("-- fps", DebugMenu.InfoLine(0f, new[] { "Low" }, 5, 1, 1, "S", "1"));
        }

        [Test]
        public void ScreenshotsSortByTheMomentTheyWereTaken()
        {
            string earlier = DebugMenu.ScreenshotName(new DateTime(2026, 11, 22, 9, 5, 7, 12));
            string later = DebugMenu.ScreenshotName(new DateTime(2026, 11, 22, 18, 0, 0, 0));

            Assert.AreEqual("FAF_2026-11-22_09-05-07-012.png", earlier);
            Assert.Less(string.CompareOrdinal(earlier, later), 0);
        }

        [TestCase("Assets/Maps/ConstructionMap.unity", true)]
        [TestCase("Assets/Maps/GolfMap.unity", true)]
        [TestCase("Assets/Training/TrainingSceneProvisional2.unity", true)]
        [TestCase("Assets/Multiplayer/Scenes/NetworkedGolfMap.unity", false)]
        [TestCase("Assets/Menu/MainMenuFinal_PostRelease.unity", false)]
        [TestCase("", false)]
        public void CustomMatchesOnlyOfferSingleplayerMaps(string scenePath, bool expected)
        {
            Assert.AreEqual(expected, DebugCustomMatch.IsPlayableMap(scenePath));
        }

        [Test]
        public void ACustomMatchAsksTheLevelForItsCarsAndSpawn()
        {
            DebugCustomMatch.Request(3, 5);
            Assert.AreEqual(4, LevelController.NextMatchCharacters, "The player counts as one car.");
            Assert.AreEqual(5, LevelController.NextMatchPlayerSpawn);

            DebugCustomMatch.Request(-2, DebugCustomMatch.RANDOM_SPAWN);
            Assert.AreEqual(1, LevelController.NextMatchCharacters);
            Assert.IsNull(LevelController.NextMatchPlayerSpawn);

            LevelController.NextMatchCharacters = null;
        }

        [TestCase(4, 8, 4)]
        [TestCase(16, 8, 8)]
        [TestCase(0, 8, 1)]
        [TestCase(3, 0, 1)]
        public void AMatchNeverHoldsMoreCarsThanItsMapHasRoomFor(int requested, int slots, int expected)
        {
            Assert.AreEqual(expected, LevelController.CharactersThatFit(requested, slots));
        }

        [Test]
        public void TheStormCanBeStoppedAndRunBothWays()
        {
            Assert.Contains(0f, DebugMenu.STORM_SPEEDS);
            Assert.Contains(1f, DebugMenu.STORM_SPEEDS);
            Assert.Less(DebugMenu.STORM_SPEEDS[0], 0f);
            Assert.Greater(DebugMenu.STORM_SPEEDS[DebugMenu.STORM_SPEEDS.Length - 1], 1f);
        }

        private global::VolumeManager ManagerWith(params string[] names)
        {
            global::VolumeManager manager = Track(new GameObject("VolumeManager")).AddComponent<global::VolumeManager>();
            var volumes = new List<GameObject>();
            foreach (string name in names)
            {
                GameObject volume = Track(new GameObject(name));
                Volume component = volume.AddComponent<Volume>();
                component.sharedProfile = Track(ScriptableObject.CreateInstance<VolumeProfile>());
                volume.SetActive(false);
                volumes.Add(volume);
            }
            manager.VolumeList = volumes;
            return manager;
        }

        private T Track<T>(T created) where T : UnityEngine.Object
        {
            _created.Add(created);
            return created;
        }
    }
}
