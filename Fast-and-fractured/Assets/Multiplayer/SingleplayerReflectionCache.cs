using System.Reflection;
using FastAndFractured;
using UnityEngine;
using Utilities;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Centralized cache for reflection accessors into private singleplayer types. The multiplayer
    /// wrappers cannot modify Assets/FastAndFractured/, so they reach into private fields/methods to
    /// coordinate state. Doing this lookup once per type per session (instead of per-call) avoids
    /// redundant work, and concentrating the references here means a singleplayer rename surfaces
    /// as a single failure (fields below become null) instead of silent failures across the codebase.
    ///
    /// All members are static and thread-safe-after-init. First access of each cached field triggers
    /// the lookup; subsequent accesses are direct field reads.
    /// </summary>
    public static class SingleplayerReflectionCache
    {
        private const BindingFlags InstancePrivate = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags InstancePublic = BindingFlags.Public | BindingFlags.Instance;

        // ---- StatsController ----
        private static FieldInfo _statsCurrentEndurance;
        private static FieldInfo _statsIsPlayer;
        private static FieldInfo _statsDeadTimer;
        private static FieldInfo _statsCurrentKiller;

        public static FieldInfo StatsController_CurrentEndurance =>
            _statsCurrentEndurance ??= typeof(StatsController).GetField("currentEndurance", InstancePrivate);

        public static FieldInfo StatsController_IsPlayer =>
            _statsIsPlayer ??= typeof(StatsController).GetField("_isPlayer", InstancePrivate);

        public static FieldInfo StatsController_DeadTimer =>
            _statsDeadTimer ??= typeof(StatsController).GetField("_deadTimer", InstancePrivate);

        public static FieldInfo StatsController_CurrentKiller =>
            _statsCurrentKiller ??= typeof(StatsController).GetField("_currentKiller", InstancePrivate);

        /// <summary>
        /// Temporarily flips StatsController._isPlayer to false around an action so HUD updates
        /// don't bleed into the local player's HUD when applying state to a non-owner vehicle.
        /// </summary>
        public static void RunWithoutPlayerFlag(StatsController stats, System.Action action)
        {
            if (stats == null || action == null) return;

            var field = StatsController_IsPlayer;
            if (field == null) { action(); return; }

            object previous = field.GetValue(stats);
            field.SetValue(stats, false);
            try { action(); }
            finally { field.SetValue(stats, previous); }
        }

        // ---- BaseUniqueAbility ----
        private static FieldInfo _abilityData;
        private static FieldInfo _abilityActivePrivate;
        private static FieldInfo _abilityActivePublic;
        private static FieldInfo _abilityIsOnCooldown;
        private static FieldInfo _abilityIsAI;
        private static FieldInfo _abilityCooldownTimer;
        private static FieldInfo _mariaAbilityDurationTimer;

        public static FieldInfo BaseUniqueAbility_IsOnCooldown =>
            _abilityIsOnCooldown ??= typeof(BaseUniqueAbility).GetField("_isOnCooldown", InstancePrivate);

        /// <summary>The singleplayer cooldown timer. Multiplayer scene teardown stops it before
        /// destroying Maria's ability component so TimerSystem cannot retain callbacks to a
        /// destroyed component for the final frame of the map.</summary>
        public static FieldInfo BaseUniqueAbility_CooldownTimer =>
            _abilityCooldownTimer ??= typeof(BaseUniqueAbility).GetField("_cooldownTimer", InstancePrivate);

        /// <summary>Maria's active-ability duration timer. Its completion delegate is the only
        /// singleplayer path which restores hair/VFX/croquettes, so multiplayer invokes that
        /// delegate deliberately before the map starts unloading.</summary>
        public static FieldInfo MariaAntoniaUniqueAbility_DurationTimer =>
            _mariaAbilityDurationTimer ??= typeof(MariaAntoniaUniqueAbility).GetField("_timer", InstancePrivate);

        /// <summary>Gates the ability's UniqueUIPrefab overlay; forced true on non-owned replicas
        /// so a remote player's ult never spawns UI on the local screen.</summary>
        public static FieldInfo BaseUniqueAbility_IsAI =>
            _abilityIsAI ??= typeof(BaseUniqueAbility).GetField("_isAI", InstancePrivate);

        public static FieldInfo BaseUniqueAbility_AbilityData =>
            _abilityData ??= typeof(BaseUniqueAbility).GetField("abilityData", InstancePrivate);

        public static FieldInfo BaseUniqueAbility_IsAbilityActivePrivate =>
            _abilityActivePrivate ??= typeof(BaseUniqueAbility).GetField("_isAbilityActive", InstancePrivate);

        public static FieldInfo BaseUniqueAbility_IsAbilityActivePublic =>
            _abilityActivePublic ??= typeof(BaseUniqueAbility).GetField("isAbilityActive", InstancePublic);

        // ---- HUDManager ----
        private static FieldInfo _hudSelectedPlayer;
        private static FieldInfo _hudSelectedPlayerHalfBody;
        private static FieldInfo _hudSelectedPlayerUA;
        private static FieldInfo _hudSelectedPlayerPush;
        private static FieldInfo _hudSelectedPlayerShoot;
        private static MethodInfo _hudSetPlayerStartingSprites;
        private static MethodInfo _hudRegisterSelectedPlayer;

        public static FieldInfo HUDManager_SelectedPlayer =>
            _hudSelectedPlayer ??= typeof(HUDManager).GetField("selectedPlayer", InstancePrivate);

        public static FieldInfo HUDManager_SelectedPlayerHalfBody =>
            _hudSelectedPlayerHalfBody ??= typeof(HUDManager).GetField("selectedPlayerHalfBody", InstancePrivate);

        public static FieldInfo HUDManager_SelectedPlayerUA =>
            _hudSelectedPlayerUA ??= typeof(HUDManager).GetField("selectedPlayerUA", InstancePrivate);

        public static FieldInfo HUDManager_SelectedPlayerPush =>
            _hudSelectedPlayerPush ??= typeof(HUDManager).GetField("selectedPlayerPush", InstancePrivate);

        public static FieldInfo HUDManager_SelectedPlayerShoot =>
            _hudSelectedPlayerShoot ??= typeof(HUDManager).GetField("selectedPlayerShoot", InstancePrivate);

        public static MethodInfo HUDManager_SetPlayerStartingSprites =>
            _hudSetPlayerStartingSprites ??= typeof(HUDManager).GetMethod("SetPlayerStartingSprites", InstancePrivate);

        public static MethodInfo HUDManager_RegisterSelectedPlayer =>
            _hudRegisterSelectedPlayer ??= typeof(HUDManager).GetMethod("RegisterSelectedPlayer", InstancePrivate);

        // ---- PlayerHUD ----
        private static MethodInfo _playerHudInitUpdateHUDEvents;

        /// <summary>
        /// PlayerHUD.InitUpdateHUDEvents — subscribes the car's overheat / push / dash / ultimate
        /// events to HUDManager. Singleplayer only ever reaches it as an onLevelPreStart listener,
        /// so a peer whose PlayerHUD is initialized AFTER that event has fired never gets the
        /// subscriptions and shows a dead HUD. Calling it directly makes the wiring independent of
        /// when the HUD happens to be initialized relative to level start.
        /// </summary>
        public static MethodInfo PlayerHUD_InitUpdateHUDEvents =>
            _playerHudInitUpdateHUDEvents ??= typeof(PlayerHUD).GetMethod("InitUpdateHUDEvents", InstancePrivate);

        // ---- AbstractAutoInitializableMonoBehaviour ----
        private static FieldInfo _abstractIsConstructed;
        private static FieldInfo _abstractIsInitialized;
        private static FieldInfo _abstractAutoConstructOnAwake;
        private static FieldInfo _abstractAutoInitializeOnStart;
        private static FieldInfo _abstractAutoInitializeOnEnable;

        public static FieldInfo AbstractAutoInit_IsConstructed =>
            _abstractIsConstructed ??= typeof(AbstractAutoInitializableMonoBehaviour).GetField("_isConstructed", InstancePrivate);

        public static FieldInfo AbstractAutoInit_IsInitialized =>
            _abstractIsInitialized ??= typeof(AbstractAutoInitializableMonoBehaviour).GetField("_isInitialized", InstancePrivate);

        public static FieldInfo AbstractAutoInit_AutoConstructOnAwake =>
            _abstractAutoConstructOnAwake ??= typeof(AbstractAutoInitializableMonoBehaviour).GetField("_autoConstructOnAwake", InstancePrivate);

        public static FieldInfo AbstractAutoInit_AutoInitializeOnStart =>
            _abstractAutoInitializeOnStart ??= typeof(AbstractAutoInitializableMonoBehaviour).GetField("_autoInitializeOnStart", InstancePrivate);

        public static FieldInfo AbstractAutoInit_AutoInitializeOnEnable =>
            _abstractAutoInitializeOnEnable ??= typeof(AbstractAutoInitializableMonoBehaviour).GetField("_autoInitializeOnEnable", InstancePrivate);

        // ---- LevelController ----
        private static FieldInfo _levelControllerGameEndData;

        /// <summary>
        /// The GameEndData ScriptableObject asset the LevelController writes match results into.
        /// The multiplayer manager reuses this to drive the same win/lose screen flow the
        /// singleplayer GameEndHandler uses (write asset, load main menu, StartMode shows screen).
        /// </summary>
        public static FieldInfo LevelController_GameEndData =>
            _levelControllerGameEndData ??= typeof(LevelController).GetField("gameEndDataScriptableObject", InstancePrivate);

        private static FieldInfo _levelControllerCharactersData;

        /// <summary>
        /// The ListOfCharactersData ScriptableObject the LevelController reads its roster from.
        /// Singleplayer assigns it in the inspector, but the multiplayer LevelController is created
        /// at runtime by the singleton accessor (no LevelController is serialized in any scene), so
        /// this field is always null there and LevelController.Construct dereferences it without a
        /// null check. The wrapper assigns it from Resources before constructing.
        /// </summary>
        public static FieldInfo LevelController_CharactersDataScriptableObject =>
            _levelControllerCharactersData ??= typeof(LevelController).GetField("charactersDataScriptableObject", InstancePrivate);

        // ---- CarMovementController ----
        private static FieldInfo _carDashTimer;
        private static FieldInfo _carDashForce;
        private static FieldInfo _carCurrentSteerAngle;
        private static FieldInfo _carCurrentRbMaxVelocity;
        private static FieldInfo _carIsDrifting;
        private static FieldInfo _carDriftDirection;
        private static FieldInfo _carInitialSpeedWhenDrifting;
        private static FieldInfo _carDriftIsBackwards;

        /// <summary>The per-frame impulse timer the SP dash creates (frame-rate dependent; the
        /// networked vehicle stops it and applies the dash force per tick instead).</summary>
        public static FieldInfo CarMovement_DashTimer =>
            _carDashTimer ??= typeof(CarMovementController).GetField("_dashTimer", InstancePrivate);

        public static FieldInfo CarMovement_DashForce =>
            _carDashForce ??= typeof(CarMovementController).GetField("dashForce", InstancePrivate);

        public static FieldInfo CarMovement_CurrentSteerAngle =>
            _carCurrentSteerAngle ??= typeof(CarMovementController).GetField("_currentSteerAngle", InstancePrivate);

        public static FieldInfo CarMovement_CurrentRbMaxVelocity =>
            _carCurrentRbMaxVelocity ??= typeof(CarMovementController).GetField("_currentRbMaxVelocity", InstancePrivate);

        public static FieldInfo CarMovement_IsDrifting =>
            _carIsDrifting ??= typeof(CarMovementController).GetField("_isDrifting", InstancePrivate);

        public static FieldInfo CarMovement_DriftDirection =>
            _carDriftDirection ??= typeof(CarMovementController).GetField("_driftDirection", InstancePrivate);

        public static FieldInfo CarMovement_InitialSpeedWhenDrifting =>
            _carInitialSpeedWhenDrifting ??= typeof(CarMovementController).GetField("_initialSpeedWhenDrifting", InstancePrivate);

        public static FieldInfo CarMovement_DriftIsBackwards =>
            _carDriftIsBackwards ??= typeof(CarMovementController).GetField("_driftIsBackwards", InstancePrivate);

        // ---- StateMachine.Controller ----
        private static MethodInfo _controllerUpdateStateActions;
        private static MethodInfo _controllerEvaluateStateTransitions;

        public static MethodInfo Controller_UpdateStateActions =>
            _controllerUpdateStateActions ??= typeof(StateMachine.Controller).GetMethod("UpdateStateActions", InstancePrivate);

        public static MethodInfo Controller_EvaluateStateTransitions =>
            _controllerEvaluateStateTransitions ??= typeof(StateMachine.Controller).GetMethod("EvaluateStateTransitions", InstancePrivate);
    }
}
