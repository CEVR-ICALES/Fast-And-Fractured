using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using FastAndFractured.Multiplayer;
using FastAndFractured;
using FishNet.Object;
using Utilities;

namespace FastAndFractured.Multiplayer.Tests
{
    [TestFixture]
    public class MultiplayerComponentTests
    {
        #region NetworkedStatsController Tests

        [Test]
        public void NetworkedStatsController_HasRequiredFields()
        {
            var type = typeof(NetworkedStatsController);

            // FishNet's IL weaving rewrites SyncVar fields to public at compile time,
            // so reflection must include Public even though the source declares them private.
            var enduranceField = type.GetField("_syncedEndurance",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(enduranceField, "NetworkedStatsController should have _syncedEndurance field");

            var invulnerableField = type.GetField("_syncedInvulnerable",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(invulnerableField, "NetworkedStatsController should have _syncedInvulnerable field");
        }

        [Test]
        public void NetworkedStatsController_HasPublicAPI()
        {
            var type = typeof(NetworkedStatsController);

            Assert.IsNotNull(type.GetMethod("GetSyncedEndurance"),
                "NetworkedStatsController should have GetSyncedEndurance method");
            Assert.IsNotNull(type.GetMethod("GetMaxEndurance"),
                "NetworkedStatsController should have GetMaxEndurance method");
            Assert.IsNotNull(type.GetMethod("IsInvulnerable"),
                "NetworkedStatsController should have IsInvulnerable method");
            Assert.IsNotNull(type.GetMethod("IsAlive"),
                "NetworkedStatsController should have IsAlive method");
        }

        [Test]
        public void NetworkedStatsController_HasServerRpcs()
        {
            var type = typeof(NetworkedStatsController);

            Assert.IsNotNull(type.GetMethod("DealDamageServerRpc"),
                "NetworkedStatsController should have DealDamageServerRpc method");
            Assert.IsNotNull(type.GetMethod("HealServerRpc"),
                "NetworkedStatsController should have HealServerRpc method");
            Assert.IsNotNull(type.GetMethod("SetInvulnerabilityServerRpc"),
                "NetworkedStatsController should have SetInvulnerabilityServerRpc method");
        }

        #endregion

        #region NetworkedVehicle Tests

        [Test]
        public void NetworkedVehicle_HasPredictionStructs()
        {
            var moveDataType = typeof(NetworkedVehicle).GetNestedType("MoveData");
            Assert.IsNotNull(moveDataType, "NetworkedVehicle should have MoveData struct");

            var reconcileDataType = typeof(NetworkedVehicle).GetNestedType("ReconcileData");
            Assert.IsNotNull(reconcileDataType, "NetworkedVehicle should have ReconcileData struct");
        }

        [Test]
        public void NetworkedVehicle_MoveData_HasRequiredFields()
        {
            var moveDataType = typeof(NetworkedVehicle).GetNestedType("MoveData");
            Assert.IsNotNull(moveDataType);

            Assert.IsNotNull(moveDataType.GetField("MoveInput"),
                "MoveData should have MoveInput field");
            Assert.IsNotNull(moveDataType.GetField("AccelerateInput"),
                "MoveData should have AccelerateInput field");
            Assert.IsNotNull(moveDataType.GetField("HandbrakeInput"),
                "MoveData should have HandbrakeInput field");
        }

        #endregion

        #region NetworkedShoot Tests

        [Test]
        public void NetworkedShoot_HasShootDataStruct()
        {
            var shootDataType = typeof(NetworkedShoot).GetNestedType("ShootData");
            Assert.IsNotNull(shootDataType, "NetworkedShoot should have ShootData struct");

            Assert.IsNotNull(shootDataType.GetField("NormalShoot"),
                "ShootData should have NormalShoot field");
            Assert.IsNotNull(shootDataType.GetField("PushShoot"),
                "ShootData should have PushShoot field");
            Assert.IsNotNull(shootDataType.GetField("AimDirection"),
                "ShootData should have AimDirection field");
        }

        #endregion

        #region NetworkedProjectile Tests

        [Test]
        public void NetworkedProjectile_IsNotReconciled()
        {
            // A shot has no input to predict: every peer flies it from the same launch state and the
            // server's despawn ends it. Reconciling it (the previous design) snapped the owner's shot
            // back to the server's older position and could not re-simulate the gap under
            // PhysicsMode.Unity - see NetworkedProjectile's class doc for the measurements.
            Assert.IsNull(typeof(NetworkedProjectile).GetNestedType("ReconcileData"),
                "NetworkedProjectile must not be reconciled");
            Assert.IsNull(typeof(NetworkedProjectile).GetNestedType("MoveData"),
                "NetworkedProjectile has no input to replicate");
        }

        [Test]
        public void NetworkedProjectile_RpcInitialize_UsesRunLocallyObserversRpc()
        {
            var method = typeof(NetworkedProjectile).GetMethod("RpcInitialize",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method, "NetworkedProjectile should expose RpcInitialize");

            var observersRpc = method.GetCustomAttribute<ObserversRpcAttribute>();
            Assert.IsNotNull(observersRpc, "RpcInitialize should be marked with ObserversRpc");
            Assert.IsTrue(observersRpc.RunLocally,
                "RpcInitialize must use RunLocally=true so server initializes authoritative projectile state.");
        }

        #endregion

        #region NetworkedUniqueAbility Tests

        [Test]
        public void NetworkedUniqueAbility_HasNetworkedPrefabField()
        {
            var type = typeof(NetworkedUniqueAbility);
            var field = type.GetField("_networkedAbilityPrefab",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "NetworkedUniqueAbility should have _networkedAbilityPrefab field");
        }

        #endregion

        #region NormalizeCharacterCode Tests

        private static MethodInfo GetNormalizeCharacterCodeMethod()
        {
            return typeof(NetworkedLobbyManager).GetMethod(
                "NormalizeCharacterCode",
                BindingFlags.NonPublic | BindingFlags.Static);
        }

        private static string InvokeNormalize(string input)
        {
            var method = GetNormalizeCharacterCodeMethod();
            Assert.IsNotNull(method, "NetworkedLobbyManager should have NormalizeCharacterCode helper.");
            return method.Invoke(null, new object[] { input }) as string;
        }

        [Test]
        public void NormalizeCharacterCode_BareNameAppendsSuffix()
        {
            Assert.AreEqual("Pepe_0", InvokeNormalize("Pepe"));
        }

        [Test]
        public void NormalizeCharacterCode_AlreadyCodedStaysUnchanged()
        {
            Assert.AreEqual("Josefino_2", InvokeNormalize("Josefino_2"));
        }

        [Test]
        public void NormalizeCharacterCode_EmptyStringReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, InvokeNormalize(""));
        }

        [Test]
        public void NormalizeCharacterCode_NullReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, InvokeNormalize(null));
        }

        [Test]
        public void NormalizeCharacterCode_WhitespaceOnlyReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, InvokeNormalize("   "));
        }

        [Test]
        public void NormalizeCharacterCode_TrimsWhitespace()
        {
            Assert.AreEqual("Pepe_0", InvokeNormalize("  Pepe  "));
        }

        [Test]
        public void NormalizeCharacterCode_MultipleUnderscoresPreserved()
        {
            // "Maria_Antonia_1" has last segment "1" which is numeric -> treated as already coded
            Assert.AreEqual("Maria_Antonia_1", InvokeNormalize("Maria_Antonia_1"));
        }

        [Test]
        public void NormalizeCharacterCode_TrailingUnderscoreNoNumber()
        {
            // "Pepe_" has empty string after last underscore -> not numeric -> appends _0
            Assert.AreEqual("Pepe__0", InvokeNormalize("Pepe_"));
        }

        [Test]
        public void NormalizeCharacterCode_ZeroSuffixPreserved()
        {
            Assert.AreEqual("Carme_0", InvokeNormalize("Carme_0"));
        }

        #endregion

        #region GetFallbackCharacterCode Tests

        private static MethodInfo GetFallbackCharacterCodeMethod()
        {
            return typeof(NetworkedLobbyManager).GetMethod(
                "GetFallbackCharacterCode",
                BindingFlags.Public | BindingFlags.Static);
        }

        private static string InvokeFallback(int index)
        {
            var method = GetFallbackCharacterCodeMethod();
            Assert.IsNotNull(method, "NetworkedLobbyManager should have GetFallbackCharacterCode helper.");
            return method.Invoke(null, new object[] { index }) as string;
        }

        [Test]
        public void GetFallbackCharacterCode_Index0ReturnsPepe()
        {
            Assert.AreEqual("Pepe_0", InvokeFallback(0));
        }

        [Test]
        public void GetFallbackCharacterCode_Index1ReturnsJosefino()
        {
            Assert.AreEqual("Josefino_0", InvokeFallback(1));
        }

        [Test]
        public void GetFallbackCharacterCode_WrapsAroundAfterFourCharacters()
        {
            Assert.AreEqual("Pepe_0", InvokeFallback(4));
        }

        [Test]
        public void GetFallbackCharacterCode_NegativeIndexClampedToZero()
        {
            Assert.AreEqual("Pepe_0", InvokeFallback(-1));
        }

        [Test]
        public void GetFallbackCharacterCode_LargeIndexWraps()
        {
            // index 7 % 4 = 3 -> Carme_0
            Assert.AreEqual("Carme_0", InvokeFallback(7));
        }

        #endregion

        #region NetworkShootParams Struct Tests

        [Test]
        public void NetworkShootParams_HasAllBulletParameters()
        {
            var type = typeof(NetworkShootParams);

            Assert.IsNotNull(type.GetField("Damage"), "NetworkShootParams should have Damage");
            Assert.IsNotNull(type.GetField("PushForce"), "NetworkShootParams should have PushForce");
            Assert.IsNotNull(type.GetField("ExplosionRadius"), "NetworkShootParams should have ExplosionRadius");
            Assert.IsNotNull(type.GetField("InitialVelocity"), "NetworkShootParams should have InitialVelocity");
            Assert.IsNotNull(type.GetField("IsMine"), "NetworkShootParams should have IsMine");
        }

        [Test]
        public void NetworkShootParams_DefaultValuesAreZero()
        {
            var p = new NetworkShootParams();
            Assert.AreEqual(0f, p.Damage);
            Assert.AreEqual(0f, p.PushForce);
            Assert.AreEqual(0f, p.ExplosionRadius);
            Assert.AreEqual(Vector3.zero, p.InitialVelocity);
            Assert.IsFalse(p.IsMine);
            Assert.AreEqual(0, p.BouncingNum);
            Assert.AreEqual(0f, p.TimeToExplode);
        }

        #endregion

        #region VehicleInputData Struct Tests

        [Test]
        public void VehicleInputData_DefaultIsNoInput()
        {
            var input = new VehicleInputData();
            Assert.AreEqual(Vector2.zero, input.MoveInput, "Default MoveInput should be zero");
            Assert.AreEqual(0f, input.AccelerateInput, "Default AccelerateInput should be zero");
            Assert.IsFalse(input.HandbrakeInput, "Default HandbrakeInput should be false");
            Assert.IsFalse(input.DashInput, "Default DashInput should be false");
        }

        #endregion

        #region BulletSpawnParameters Tests

        [Test]
        public void BulletSpawnParameters_HasIsMineField()
        {
            var field = typeof(BulletSpawnParameters).GetField("IsMine");
            Assert.IsNotNull(field, "BulletSpawnParameters should have IsMine field");
            Assert.AreEqual(typeof(bool), field.FieldType, "IsMine should be bool");
        }

        [Test]
        public void BulletSpawnParameters_DefaultIsMineIsFalse()
        {
            var p = default(BulletSpawnParameters);
            Assert.IsFalse(p.IsMine, "Default BulletSpawnParameters.IsMine should be false (no mine by default)");
        }

        [Test]
        public void NetworkShootParams_IsMine_PropagatesFromBulletSpawnParameters()
        {
            // Verify both structs have IsMine so NetworkedBulletSpawner can propagate the flag.
            Assert.IsNotNull(typeof(BulletSpawnParameters).GetField("IsMine"),
                "BulletSpawnParameters.IsMine missing — spawner cannot set mine type");
            Assert.IsNotNull(typeof(NetworkShootParams).GetField("IsMine"),
                "NetworkShootParams.IsMine missing — RpcInitialize cannot receive mine flag");
        }

        #endregion

        #region Prefab Validation Tests

        [Test]
        public void NetworkedVehiclePrefabs_ExistInMultiplayerFolder()
        {
            var guids = AssetDatabase.FindAssets("Networked t:prefab",
                new[] { "Assets/Multiplayer" });

            bool foundVehicle = false;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<NetworkedVehicle>() != null)
                {
                    foundVehicle = true;
                    break;
                }
            }

            Assert.IsTrue(foundVehicle,
                "At least one networked vehicle prefab should exist in Assets/Multiplayer/");
        }

        [Test]
        public void NetworkedProjectilePrefabs_HaveRequiredComponents()
        {
            var guids = AssetDatabase.FindAssets("Networked t:prefab",
                new[] { "Assets/Multiplayer/Pool" });

            int projectileCount = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) continue;

                var networkedProjectile = prefab.GetComponent<NetworkedProjectile>();
                if (networkedProjectile != null)
                {
                    projectileCount++;
                    var networkObject = prefab.GetComponent<NetworkObject>();
                    Assert.IsNotNull(networkObject,
                        $"Networked projectile '{prefab.name}' should have NetworkObject component");
                }
            }

            Assert.Greater(projectileCount, 0,
                "At least one networked projectile prefab should exist in Assets/Multiplayer/Pool/");
        }

        [Test]
        public void AllNetworkedProjectilePrefabs_HaveSerializedNetworkBehaviours()
        {
            var guids = AssetDatabase.FindAssets("Networked t:prefab",
                new[] { "Assets/Multiplayer/Pool" });

            int validated = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var networkObject = prefab.GetComponent<NetworkObject>();
                if (networkObject == null) continue;

                var networkedProjectile = prefab.GetComponent<NetworkedProjectile>();
                if (networkedProjectile == null) continue;

                Assert.IsNotNull(networkObject.NetworkBehaviours,
                    $"NetworkBehaviours list is null in {path}");
                Assert.Greater(networkObject.NetworkBehaviours.Count, 0,
                    $"NetworkBehaviours list is empty in {path}. Re-serialize this prefab with FishNet tools.");
                validated++;
            }

            Assert.Greater(validated, 0,
                "No networked projectile prefabs with NetworkObject found in Assets/Multiplayer/Pool/");
        }

        /// <summary>
        /// Regression test for the MariaAntonia/Carme divergence bug. The four vehicle
        /// prefabs must use Rigidbody prediction so FishNet auto-applies reconcile to the
        /// rb. With prediction type "Other" (=0) the client kept simulating without server
        /// correction and "se sale del mapa". Asserts every Networked*GameplayCar variant
        /// has _predictionType set to 1 (Rigidbody).
        /// </summary>
        /// <summary>
        /// Regression test for the MariaAntonia/Carme empty-NetworkBehaviours-list bug. When a
        /// FishNet prefab is created without re-serializing in the editor, NetworkObject.NetworkBehaviours
        /// can stay empty and components on the prefab won't receive RPCs or replicate state. The
        /// resulting symptom matches "still desynced" reports: the rb sort-of works but custom
        /// behaviours appear inert. Fix in Unity: open each prefab and save, or run
        /// Tools &gt; Fish-Networking &gt; Refresh All NetworkObjects.
        /// </summary>
        [Test]
        public void AllVehiclePrefabs_HaveSerializedNetworkBehaviours()
        {
            var guids = AssetDatabase.FindAssets("Networked t:prefab",
                new[] { "Assets/Multiplayer" });

            int validated = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("GameplayCar")) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var nob = prefab.GetComponent<NetworkObject>();
                if (nob == null) continue;

                Assert.IsNotNull(nob.NetworkBehaviours,
                    $"NetworkBehaviours list is null in '{prefab.name}'.");
                Assert.Greater(nob.NetworkBehaviours.Count, 0,
                    $"NetworkBehaviours list is EMPTY in '{prefab.name}'. " +
                    "Re-serialize the prefab in Unity (open it and save, or run " +
                    "Tools > Fish-Networking > Refresh All NetworkObjects).");
                validated++;
            }

            Assert.Greater(validated, 0,
                "No NetworkedGameplayCar prefabs found in Assets/Multiplayer/. Test cannot run.");
        }

        [Test]
        public void AllVehiclePrefabs_UseRigidbodyPredictionType()
        {
            var guids = AssetDatabase.FindAssets("NetworkedGameplayCar t:prefab",
                new[] { "Assets/Multiplayer" });

            // Fall back: scan more broadly if specific name search misses any.
            if (guids.Length == 0)
            {
                guids = AssetDatabase.FindAssets("Networked t:prefab",
                    new[] { "Assets/Multiplayer" });
            }

            int validated = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("GameplayCar")) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var nob = prefab.GetComponent<NetworkObject>();
                if (nob == null) continue;

                var predictionTypeField = typeof(NetworkObject).GetField("_predictionType",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(predictionTypeField,
                    "FishNet's NetworkObject._predictionType field changed; this test needs updating.");

                // _predictionType is a nested enum whose underlying type is not int;
                // unboxing straight to int throws InvalidCastException.
                int rawType = System.Convert.ToInt32(predictionTypeField.GetValue(nob));
                Assert.AreEqual(1, rawType,
                    $"Vehicle prefab '{prefab.name}' has _predictionType={rawType}; expected 1 (Rigidbody). " +
                    "Other (0) breaks reconcile and causes catastrophic client divergence.");
                validated++;
            }

            Assert.Greater(validated, 0,
                "No NetworkedGameplayCar prefabs found in Assets/Multiplayer/. Test cannot run.");
        }

        #endregion

        #region Server-Authoritative Wrapper Tests

        [Test]
        public void ServerAuthoritativeWrapper_ExposesGenericBase()
        {
            var type = typeof(ServerAuthoritativeWrapper<>);
            Assert.IsNotNull(type, "ServerAuthoritativeWrapper<T> should exist as the base for client-disable wrappers.");
            Assert.IsTrue(type.IsAbstract, "ServerAuthoritativeWrapper should be abstract; concrete wrappers extend it.");
        }

        [Test]
        public void NetworkedSuckingBuilding_DerivesFromBase()
        {
            var type = typeof(NetworkedSuckingBuilding);
            Assert.IsTrue(type.BaseType != null && type.BaseType.IsGenericType,
                "NetworkedSuckingBuilding should derive from ServerAuthoritativeWrapper<SuckingBuilding>.");
        }

        [Test]
        public void NetworkedTrampoline_DerivesFromBase()
        {
            var type = typeof(NetworkedTrampoline);
            Assert.IsTrue(type.BaseType != null && type.BaseType.IsGenericType,
                "NetworkedTrampoline should derive from ServerAuthoritativeWrapper<ParabolicImpulse>.");
        }

        [Test]
        public void NetworkedStatsBoostInteractable_DerivesFromBase()
        {
            var type = typeof(NetworkedStatsBoostInteractable);
            Assert.IsTrue(type.BaseType != null && type.BaseType.IsGenericType,
                "NetworkedStatsBoostInteractable should derive from ServerAuthoritativeWrapper<StatsBoostInteractable>.");
        }

        #endregion

        #region Legacy Hazard Isolation Tests

        [Test]
        public void NetworkedMegaChickenBoss_UsesExactKillZoneRelay()
        {
            const BindingFlags PrivateInstance =
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            Assert.IsNotNull(typeof(NetworkedMegaChickenBoss).GetField("_killZoneRelay", PrivateInstance),
                "Mega Chicken must retain its runtime kill-zone relay reference.");
            Assert.IsNotNull(typeof(NetworkedMegaChickenBoss).GetMethod("HandleKillZoneTrigger", PrivateInstance),
                "Mega Chicken must expose an internal relay endpoint.");
            Assert.IsNull(typeof(NetworkedMegaChickenBoss).GetMethod("OnTriggerEnter", PrivateInstance),
                "A root OnTriggerEnter receives unrelated child colliders; use the exact hitbox relay.");

            Assert.IsNotNull(typeof(NetworkedChickenKillZoneRelay).GetMethod("OnTriggerEnter", PrivateInstance),
                "The kill-zone relay must own the Unity trigger callback.");
        }

        [Test]
        public void NetworkedSandstormController_UsesPhysicallyIsolatedHitboxRelay()
        {
            const BindingFlags PrivateInstance =
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            Assert.IsNotNull(typeof(NetworkedSandstormController).GetField("_networkHitbox", PrivateInstance),
                "Sandstorm must keep a collider separate from the legacy SandstormController collider.");
            Assert.IsNotNull(typeof(NetworkedSandstormController).GetField("_hitboxRelay", PrivateInstance),
                "Sandstorm must keep its dedicated trigger relay.");
            Assert.IsNotNull(typeof(NetworkedSandstormController).GetMethod("HandleStormTriggerEnter", PrivateInstance));
            Assert.IsNotNull(typeof(NetworkedSandstormController).GetMethod("HandleStormTriggerExit", PrivateInstance));

            Assert.IsNotNull(typeof(NetworkedSandstormHitboxRelay).GetMethod("OnTriggerEnter", PrivateInstance));
            Assert.IsNotNull(typeof(NetworkedSandstormHitboxRelay).GetMethod("OnTriggerExit", PrivateInstance));
        }

        [Test]
        public void NetworkedSandstormController_OverlapRefCountWaitsForFinalExit()
        {
            const BindingFlags PrivateStatic =
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
            MethodInfo register = typeof(NetworkedSandstormController)
                .GetMethod("RegisterListOverlap", PrivateStatic);
            MethodInfo unregister = typeof(NetworkedSandstormController)
                .GetMethod("UnregisterListOverlap", PrivateStatic);
            Assert.IsNotNull(register);
            Assert.IsNotNull(unregister);

            var counts = new System.Collections.Generic.Dictionary<GameObject, int>();
            var tracked = new System.Collections.Generic.List<GameObject>();
            var target = new GameObject("Sandstorm overlap test target");
            try
            {
                register.Invoke(null, new object[] { counts, tracked, target });
                register.Invoke(null, new object[] { counts, tracked, target });
                unregister.Invoke(null, new object[] { counts, tracked, target });

                Assert.AreEqual(1, tracked.Count,
                    "The first collider exit must not eject a multi-collider object.");
                Assert.AreEqual(1, counts[target]);

                unregister.Invoke(null, new object[] { counts, tracked, target });
                Assert.IsEmpty(tracked, "The final collider exit must remove the tracked object.");
                Assert.IsFalse(counts.ContainsKey(target));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region SingleplayerReflectionCache Tests

        [Test]
        public void ReflectionCache_ResolvesStatsControllerFields()
        {
            Assert.IsNotNull(SingleplayerReflectionCache.StatsController_CurrentEndurance,
                "currentEndurance lookup failed; singleplayer rename?");
            Assert.IsNotNull(SingleplayerReflectionCache.StatsController_IsPlayer,
                "_isPlayer lookup failed; singleplayer rename?");
            Assert.IsNotNull(SingleplayerReflectionCache.StatsController_DeadTimer,
                "_deadTimer lookup failed; pending hazard deaths cannot be cancelled safely.");
            Assert.AreEqual(typeof(ITimer), SingleplayerReflectionCache.StatsController_DeadTimer.FieldType,
                "StatsController._deadTimer must remain an ITimer.");
            Assert.IsNotNull(SingleplayerReflectionCache.StatsController_CurrentKiller,
                "_currentKiller lookup failed; stale hazard ownership cannot be cleared safely.");
            Assert.AreEqual(typeof(IKillCharacters), SingleplayerReflectionCache.StatsController_CurrentKiller.FieldType,
                "StatsController._currentKiller type changed.");
        }

        [Test]
        public void ReflectionCache_ResolvesControllerMethods()
        {
            Assert.IsNotNull(SingleplayerReflectionCache.Controller_UpdateStateActions,
                "UpdateStateActions lookup failed; the state-machine driver will not tick.");
            Assert.IsNotNull(SingleplayerReflectionCache.Controller_EvaluateStateTransitions,
                "EvaluateStateTransitions lookup failed; the state-machine driver will not tick.");
        }

        [Test]
        public void ReflectionCache_ResolvesCarMovementPredictionState()
        {
            // Every CarMovementController field the car's reconcile state captures and restores.
            Assert.IsNotNull(SingleplayerReflectionCache.CarMovement_CurrentRbMaxVelocity, "_currentRbMaxVelocity renamed?");
            Assert.IsNotNull(SingleplayerReflectionCache.CarMovement_IsBeenImpulsed,
                "_isBeenImpulsed renamed? Trampoline/TNT flights will be re-clamped in the owner's replays.");
            Assert.AreEqual(typeof(bool), SingleplayerReflectionCache.CarMovement_IsBeenImpulsed.FieldType);
            Assert.IsNotNull(SingleplayerReflectionCache.CarMovement_CurrentSteerAngle);
            Assert.IsNotNull(SingleplayerReflectionCache.CarMovement_IsDrifting);
        }

        [Test]
        public void ReflectionCache_ResolvesAbstractAutoInitFields()
        {
            Assert.IsNotNull(SingleplayerReflectionCache.AbstractAutoInit_IsConstructed);
            Assert.IsNotNull(SingleplayerReflectionCache.AbstractAutoInit_IsInitialized);
            Assert.IsNotNull(SingleplayerReflectionCache.AbstractAutoInit_AutoConstructOnAwake);
            Assert.IsNotNull(SingleplayerReflectionCache.AbstractAutoInit_AutoInitializeOnStart);
            Assert.IsNotNull(SingleplayerReflectionCache.AbstractAutoInit_AutoInitializeOnEnable);
        }

        #endregion

        #region BUG-4 — Stray debug character-select dropdown

        /// <summary>
        /// BUG-4 (2026-09-16): a leftover DebugCharacterSelector "Dropdown" GameObject shipped
        /// active on the networked player prefab, floating visibly in every match, because nothing
        /// ever hid it once character selection was done. Confirmed live by inspecting a real match.
        /// Fixed by NetworkedLevelControllerWrapper.HideDebugCharacterSelectorDropdowns(), which -
        /// unlike the ownership-gated remote-canvas/HUD/camera passes it runs alongside - hides every
        /// instance, not just remote ones (the debug selector's job is done in the lobby, before this
        /// runs, regardless of who owns it). This test drives the real method against real
        /// GameObjects instead of just checking it exists, so a future edit that narrows the
        /// FindObjectsByType filter or re-adds an ownership check fails loudly.
        /// </summary>
        [Test]
        public void HideDebugCharacterSelectorDropdowns_DeactivatesEveryInstance()
        {
            var wrapperObject = new GameObject("NetworkedLevelControllerWrapper under test");
            var localDropdown = new GameObject("Dropdown (local player)");
            var remoteDropdown = new GameObject("Dropdown (remote player, already inactive)");

            try
            {
                var wrapper = wrapperObject.AddComponent<NetworkedLevelControllerWrapper>();
                localDropdown.AddComponent<DebugCharacterSelector>();
                remoteDropdown.AddComponent<DebugCharacterSelector>();
                remoteDropdown.SetActive(false);

                Assert.IsTrue(localDropdown.activeSelf, "Test setup should start with the dropdown active");

                var method = typeof(NetworkedLevelControllerWrapper).GetMethod(
                    "HideDebugCharacterSelectorDropdowns", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(method,
                    "HideDebugCharacterSelectorDropdowns renamed or removed? BUG-4 will come back - " +
                    "update this test alongside whatever replaced it.");

                method.Invoke(wrapper, null);

                Assert.IsFalse(localDropdown.activeSelf,
                    "BUG-4 regression: the debug dropdown must be hidden even though it's on the LOCAL " +
                    "player's own object - this pass has nothing to do with ownership, unlike the " +
                    "canvas/HUD/camera passes it runs alongside.");
                Assert.IsFalse(remoteDropdown.activeSelf,
                    "An already-inactive dropdown must stay inactive, not throw or get re-activated.");
            }
            finally
            {
                Object.DestroyImmediate(wrapperObject);
                Object.DestroyImmediate(localDropdown);
                Object.DestroyImmediate(remoteDropdown);
            }
        }

        #endregion
    }
}
