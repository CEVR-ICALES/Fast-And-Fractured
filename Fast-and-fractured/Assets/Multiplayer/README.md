# Fast And Fractured - Multiplayer

Online multiplayer for Fast And Fractured, built on **FishNet Pro 4.7.3** (Tugboat transport) on top of the
singleplayer game. Up to 8 players per room (humans and AI), on the three maps, with all four characters.

## Architecture

- **Wrapper pattern.** Networked components sit next to the singleplayer ones and drive or replace them at
  runtime; singleplayer code never references networking. Private singleplayer state is reached through
  `SingleplayerReflectionCache`, so a singleplayer rename fails in one place.
- **Server-authoritative.** The server decides damage, deaths, pickups, hazards and the match result.
  - **Cars** are client-predicted (`NetworkedVehicle`, `[Replicate]` / `[Reconcile]`) with a soft reconcile
    for the owned car. The singleplayer state machine is ticked from the network loop
    (`NetworkedStateMachineDriver`).
  - **Shots** are predicted spawns: every peer flies the same projectile from the same launch state and the
    server validates the fire rate and the launch (`NetworkedShoot`, `NetworkedProjectile`).
  - **Ultimates** run the singleplayer ability on every replica; the server validates heals, shields and
    networked spawns (`NetworkedUniqueAbility` and the per-character wrappers).
- **Timing.** 50 Hz tick, the project's fixed timestep, with `PhysicsMode.Unity`. Unity physics is not
  deterministic: prediction and reconciliation absorb the difference.
- **Rendering.** Cars are drawn from an interpolation buffer (`VehicleVisualSmoother`) and the camera follows
  a smoothed point (`CameraTargetSmoother`). Rigidbody interpolation stays **None** and FishNet's
  `NetworkTickSmoother` is stopped at runtime.
- **Singletons.** Player-root singletons stay dormant until ownership is known
  (`NetworkedLocalSingletonPreserver`). Outside a match, use `ExistingInstance` / `HasInstance`, never
  `Instance`, which creates phantom singletons.

## Folder layout

| Folder | Contents |
|--------|----------|
| `Core/` | Bootstrap and tick timing, singleton guards, reflection cache, network quality (ping, link, rollback), multiplayer settings, canary log |
| `Lobby/` | Hub, host setup, browser, direct join, room UI, map vote; `Transport/` (Tugboat LAN, Steam, direct IP); `Resources/` is loaded by name |
| `Match/` | Match lifecycle: level controller wrapper and manager, start gate, deaths and results, spectators, pause, room return, teardown |
| `HostMigration/` | Match host migration (opt-in) and room migration |
| `Player/` | Networked player root, input provider, state machine driver, stats, camera, screen shake |
| `Vehicles/` | Car prediction, visual smoothing, rigidbody adapter; the four networked car prefabs |
| `ShootingSystem/` | Networked shooting, projectiles, predicted spawns, server validation |
| `UniqueAbilities/` | Ultimates: Pepe's tomatoes, Carme's McChicken, predicted ability spawns |
| `Sandstorm/` | Networked storm schedule and server hitbox |
| `MapDynamicProps/` | Hazards and world sync: dead walls, TNT, platforms, trampolines, Mega Chicken, golf balls, loose props, breakables |
| `Interactables/` | Pickups and skin unlocks |
| `EnemyAI/` | AI targeting of human players |
| `Props/`, `Pool/`, `Scenes/` | **Generated** by the editor tools (prop variants, projectile variants, networked maps). Regenerate them with the tools; never edit by hand |
| `Editor/` | Editor tools |
| `Tests/Editor/` | EditMode tests |
| `Testing/` | Development diagnostics; excluded from release builds |

Related code outside this folder: `FastAndFractured/Progression` (XP, levels, accessories),
`Telemetry` (gameplay statistics), `FastAndFractured/Platform/Steam` (Steam manager).

## Features

- **Room** (`NetworkedLobbyManager`): it outlives the match. It holds ready states, characters, AI slots, a
  map vote with an animated pick and the countdown. Players who join mid-match wait in the room or watch the
  match live.
- **Match**: synced 3-2-1 start, spectator mode on death, names over cars, kill credit, results screen with
  placement and XP. The host can skip a match where only AIs are left.
- **Host leaves**: the room carries on under another player (**room migration**, on by default). In-match
  **host migration** is experimental and off by default
  (`Tools > Fast And Fractured > Host Migration > Enable / Disable`).
- **Network quality**: Wi-Fi or cable, ping, input delay and rollback per player, in the room and on an F3
  HUD in matches.
- **Settings > Multiplayer**: name tags, network panel, show player levels, willingness to take over the room.

## Editor tools

Entry point: **Tools > Fast And Fractured > Multiplayer Tools Hub**.

| Tool | Menu | Purpose |
|------|------|---------|
| Scene Migration | Tools > Fast And Fractured > Scene Migration | Generates the networked maps from the singleplayer ones (swaps hazards, keeps the map's own changes, assigns SceneIds). **Reapply All** after any map or prop change |
| Map Registration | Tools > Fast And Fractured > Map Registration Tool | Registers the maps in `MapCatalog` and the build settings |
| Networked Prop Setup | Tools > Fast And Fractured > Networked Prop Setup | Creates the networked variants of the hazard and pickup prefabs |
| Vehicle Setup | Tools > Fast And Fractured > Setup > Networked Vehicle Setup | Creates or updates the networked car variants and applies the car fixes |
| Bullet Converter | Tools > Fast And Fractured > Setup > Networked Bullet Converter | Networked projectile variants |
| AI Setup | Tools > Fast And Fractured > Setup > Networked AI Setup | Networked AI prefab |
| Lobby Setup Wizard | Tools > Fast And Fractured > Lobby Setup Wizard | Builds the multiplayer menu screens in the main menu scene |
| Localize Scene Texts | Tools > Fast And Fractured > Localization > Localize Multiplayer Scene Texts | Adds `LocalizedText` to tool-generated texts |
| Scene Validator / Log Filter | Tools > Fast And Fractured > Diagnostics | Scene checks and per-category multiplayer logs |

After generating prefabs, refresh FishNet's prefab list (`DefaultPrefabObjects`).

## Testing

- **EditMode tests**: `Tests/Editor` (rules, prefab setup, map wiring, localization, migration); run them with
  the Unity Test Runner.
- **Two players locally**: use ParrelSync. One editor hosts a room and the other joins over LAN or
  `127.0.0.1`. Play a match and return to the room.
- **Diagnostics** (`Testing/`): `NetworkedVehicleDiagnostics` (car motion), `ShootingProbe` (shots and
  ultimates), `MpAutoRecorder` (off by default, Tools > Fast And Fractured > Diagnostics). `MpCanary` writes
  `<project>/mp_canary.log`, whose last line shows where a frozen editor stopped.

## Notes

- Enter Play Mode runs with domain reload disabled: statics must be reset in a
  `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` method.
- FishNet 4.7 builds `NetworkObject.NetworkBehaviours` at spawn; an empty serialized list on a prefab is fine.
- A `[ServerRpc]` called on the host does not run its logic on nested car objects: call the server method
  directly when `IsServerInitialized`.
- The assembly definition is disabled (`.asmdef.backup`) to avoid a circular reference with Assembly-CSharp.

## Versions

- Unity 6000.3.26f1 (6.3 LTS), HDRP
- FishNet Pro 4.7.3R, Steamworks.NET (FishySteamworks)
- Input System package
