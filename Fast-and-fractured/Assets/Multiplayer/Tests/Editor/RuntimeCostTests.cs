using System.Collections.Concurrent;
using FastAndFractured.Multiplayer.Testing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Tests
{
    public class RuntimeCostTests
    {
        private static readonly string[] NPC_PREFABS =
        {
            "Assets/FastAndFractured/Characters/zzzNPCs/ElderNPC/Prefabs/ElderNPCPancracio.prefab",
            "Assets/FastAndFractured/Characters/zzzNPCs/ElderNPC/Prefabs/ElderNPCEusebio Variant.prefab",
            "Assets/FastAndFractured/Characters/zzzNPCs/ElderNPC/Prefabs/ElderNPCIsidoro Variant.prefab",
            "Assets/FastAndFractured/Characters/zzzNPCs/Alien/Prefabs/AlienNPC.prefab",
            "Assets/FastAndFractured/Characters/zzzNPCs/MercadilloWoman/Prefabs/Vane.prefab",
            "Assets/FastAndFractured/Characters/zzzNPCs/MercadilloWoman/Prefabs/Jesi Variant.prefab",
            "Assets/FastAndFractured/Characters/zzzNPCs/MercadilloWoman/Prefabs/Lore Variant.prefab",
        };

        [Test]
        public void MenuScan_RunsRightAfterATriggerAndThenOnlyRarely()
        {
            var schedule = new ScanSchedule(new[] { 0f, 0.5f }, 10f);

            Assert.IsTrue(schedule.IsDue(100f, trigger: 1), "A new scene or player is scanned at once.");
            Assert.IsFalse(schedule.IsDue(100.2f, trigger: 1));
            Assert.IsTrue(schedule.IsDue(100.5f, trigger: 1), "Menus that arrive a moment later are still picked up.");
            Assert.IsFalse(schedule.IsDue(105f, trigger: 1), "No half-second polling once the burst is over.");
            Assert.IsTrue(schedule.IsDue(110.5f, trigger: 1));
            Assert.IsTrue(schedule.IsDue(111f, trigger: 2), "The next trigger starts a new burst.");
        }

        [Test]
        public void Diagnostics_StayOffInReleaseBuildsUnlessAsked()
        {
            Assert.IsFalse(DiagnosticsSwitch.IsEnabled(isEditor: false, isDebugBuild: false, new[] { "game.exe" }));
            Assert.IsTrue(DiagnosticsSwitch.IsEnabled(false, false, new[] { "game.exe", DiagnosticsSwitch.COMMAND_LINE_FLAG }));
            Assert.IsTrue(DiagnosticsSwitch.IsEnabled(true, false, null));
            Assert.IsTrue(DiagnosticsSwitch.IsEnabled(false, true, null));
        }

        [Test]
        public void Canary_WritesQueuedLinesInOrderAsOneBlock()
        {
            var queue = new ConcurrentQueue<string>();
            queue.Enqueue(MpCanary.FormatLine(new System.DateTime(2026, 11, 20, 21, 5, 7, 12), "p=4 ", 9, "first"));
            queue.Enqueue(MpCanary.FormatLine(new System.DateTime(2026, 11, 20, 21, 5, 7, 40), "p=4 ", 10, "second"));

            string block = MpCanary.Drain(queue);

            Assert.AreEqual("[21:05:07.012] p=4 f=9 first\n[21:05:07.040] p=4 f=10 second\n", block);
            Assert.AreEqual(string.Empty, MpCanary.Drain(queue));
        }

        [Test]
        public void FishNet_StillHasTheSwitchesThatKeepSlowOrJitteryClientsInStep()
        {
            Assert.IsNotNull(MultiplayerTimingBootstrap.DropExcessiveReplicatesField,
                "Without it the server queues a late client's inputs instead of catching up (240 ms measured).");
            Assert.AreEqual(typeof(bool), MultiplayerTimingBootstrap.DropExcessiveReplicatesField.FieldType);
            Assert.IsNotNull(MultiplayerTimingBootstrap.ReduceReconcilesField);
            Assert.AreEqual(typeof(bool), MultiplayerTimingBootstrap.ReduceReconcilesField.FieldType);
        }

        [Test]
        public void StateMachineTick_BindsToTheSingleplayerControllerWithoutReflectionCalls()
        {
            var go = new GameObject("controller");
            try
            {
                var controller = go.AddComponent<StateMachine.Controller>();
                Assert.IsNotNull(SingleplayerReflectionCache.Controller_UpdateStateActions);
                Assert.IsNotNull(SingleplayerReflectionCache.Controller_EvaluateStateTransitions);
                Assert.DoesNotThrow(() => System.Delegate.CreateDelegate(typeof(System.Action), controller, SingleplayerReflectionCache.Controller_UpdateStateActions));
                Assert.DoesNotThrow(() => System.Delegate.CreateDelegate(typeof(System.Action), controller, SingleplayerReflectionCache.Controller_EvaluateStateTransitions));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PathKey_IsComputedOncePerProp()
        {
            var parent = new GameObject("PathKeyParent");
            try
            {
                var child = new GameObject("Prop");
                child.transform.SetParent(parent.transform);

                string first = NetworkedWorldBreakables.CachedPathKey(child.transform);

                Assert.AreEqual(NetworkedWorldBreakables.PathKey(child.transform), first);
                Assert.AreSame(first, NetworkedWorldBreakables.CachedPathKey(child.transform));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void DecorationNpcs_StopMovingBonesWhenNobodySeesThem()
        {
            foreach (string path in NPC_PREFABS)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, path);
                foreach (var animator in prefab.GetComponentsInChildren<Animator>(true))
                    Assert.AreNotEqual(AnimatorCullingMode.AlwaysAnimate, animator.cullingMode,
                        path + ": a map has over a hundred of these and each one animated every frame.");
            }
        }

        [Test]
        public void Minimap_IsRedrawnWhenThePictureRunsOutOfMarginOrGrowsOld()
        {
            Assert.IsFalse(MinimapRenderThrottle.ShouldRender(0.1f, offsetPixels: 2f, marginPixels: 12f));
            Assert.IsTrue(MinimapRenderThrottle.ShouldRender(MinimapRenderThrottle.REFRESH_SECONDS, 0f, 12f), "Pickup icons come and go: refresh a still picture too.");
            Assert.IsTrue(MinimapRenderThrottle.ShouldRender(0.1f, offsetPixels: 11f, marginPixels: 12f), "A dash outruns the margin: draw now.");
            Assert.IsTrue(MinimapRenderThrottle.ShouldRender(float.PositiveInfinity, 0f, 12f), "Never drawn yet.");
        }

        [Test]
        public void Minimap_WiderPictureKeepsItsSharpness()
        {
            int wide = MinimapRenderThrottle.WideSize(256);
            Assert.GreaterOrEqual(wide / MinimapRenderThrottle.OVERSCAN, 256f - 1f, "Same pixels per metre as the authored texture.");
        }

        [Test]
        public void Minimap_PictureSlidesBackAsTheCarAdvances()
        {
            Vector2 north = MinimapRenderThrottle.PictureOffset(Vector3.zero, new Vector3(0f, 0f, 4f), cameraYaw: 0f, pixelsPerMetre: 2f);
            Assert.AreEqual(0f, north.x, 0.001f);
            Assert.AreEqual(-8f, north.y, 0.001f, "Driving up the screen leaves the old picture below the centre.");

            Vector2 east = MinimapRenderThrottle.PictureOffset(Vector3.zero, new Vector3(4f, 0f, 0f), cameraYaw: 90f, pixelsPerMetre: 2f);
            Assert.AreEqual(0f, east.x, 0.001f);
            Assert.AreEqual(-8f, east.y, 0.001f, "The same when the camera looks east and the car drives east.");
        }

        [Test]
        public void Minimap_PictureTurnsAgainstTheCameraAndTheIconKeepsItsHeading()
        {
            Assert.AreEqual(20f, MinimapRenderThrottle.PictureTurn(renderedYaw: 350f, cameraYaw: 10f), 0.001f);
            Assert.AreEqual(0f, MinimapRenderThrottle.IconTurn(cameraYaw: 45f, playerYaw: 45f), 0.001f);
            Assert.AreEqual(-90f, MinimapRenderThrottle.IconTurn(cameraYaw: 0f, playerYaw: 90f), 0.001f, "Facing east with the camera north: the arrow points right.");
        }
    }
}
