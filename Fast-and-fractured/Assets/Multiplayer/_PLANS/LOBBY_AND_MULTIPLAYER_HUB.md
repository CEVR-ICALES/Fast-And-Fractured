# Plan: Multiplayer Hub, Lobby Room, Map Vote, AI Slots, Steam-Ready

> **Audience:** Claude Sonnet (or any implementer). This is a pure architecture document — every public type signature, RPC contract, namespace, and file path is fixed. Fill in private logic; do not redesign the public surface without flagging it.

---

## 0. Goal

Ship a complete pre-match flow before opening the multiplayer PR:

1. Player clicks **MULTIPLAYER** on main menu.
2. Player picks **Host** or **Browse / Direct IP / (Steam later)**.
3. Host creates a room (max 8 players). Joiners enter the same room.
4. In the lobby room: pick character (reusing existing UI), vote for map, host adds/removes AI slots, ready up.
5. When all human players ready (and ≥ `MinPlayersToStart`): countdown → resolve map vote → load winning scene → game starts.

## 1. Hard Constraints

- **Singleplayer code in `Assets/FastAndFractured/` is touchable ONLY for the UI changes explicitly listed in §3.** Everything else stays untouched. Wrappers go in `Assets/Multiplayer/`.
- No new Unity scene for the lobby. The existing main-menu scene (build index 1) hosts every new screen as a child of `MainMenuManager`.
- No P2P. Server-authoritative. Host migration is out of scope.
- Local clones (ParrelSync) and direct-IP joining must keep working through every refactor.
- All public types: namespace `FastAndFractured.Multiplayer` (or `FastAndFractured.Multiplayer.Lobby` / `.Abstractions` for sub-areas).

## 2. End-to-End State Flow

```
[MainMenu]
   │  click MULTIPLAYER
   ▼
[MultiplayerHub]──────┐
   │                  │ click SETTINGS / etc → existing screens
   │
   ├── click HOST ──────────► [HostSetup] ── click START ──┐
   │                                                       │
   ├── click BROWSE ────────► [LobbyBrowser] ── pick ──────┤
   │                                                       │
   └── click DIRECT IP ─────► [DirectJoin] ── connect ─────┤
                                                           ▼
                                                   [LobbyRoom]
                                                       │
                                                       │  internal sub-panels:
                                                       │   ┌─ player list + ready
                                                       │   ├─ character pick (reuse CharacterSelectorManager)
                                                       │   ├─ map vote (carousel + vote button)
                                                       │   └─ AI slot +/- (host only)
                                                       │
                                                       │  all ready + countdown finishes
                                                       ▼
                                                  [Loading]
                                                       │
                                                       ▼
                                                 [Game scene]
```

Every box is a `MenuScreen` registered with `MainMenuManager`. The flow is driven by `MainMenuManager.TransitionBetweenScreens(...)` — no new manager.

## 3. UI Architecture (Singleplayer Touch Points)

User has approved touching singleplayer code **for these specific edits only**:

### 3.1 `Assets/FastAndFractured/!Common/EnumLibrary/EnumLibrary.cs`

Add to `ScreensType`:
```csharp
public enum ScreensType
{
    MAIN_MENU,
    SETTINGS,
    CREDITS,
    GAMEMODE_SELECTION,
    CHARACTER_SELECTION,
    LOADING,
    SPLASH_SCREEN,
    EXIT_POPUP,
    PAUSE,
    WIN_LOSE,
    HOW_TO_PLAY,
    MAP_SELECTION,
    // === ADD BELOW ===
    MULTIPLAYER_HUB,
    MULTIPLAYER_HOST_SETUP,
    MULTIPLAYER_BROWSER,
    MULTIPLAYER_DIRECT_JOIN,
    LOBBY_ROOM,
    LOBBY_MAP_VOTE
}
```

`MAP_SELECTION` (singleplayer carousel) is **left alone**. `LOBBY_MAP_VOTE` is the multiplayer voting screen.

### 3.2 `Assets/FastAndFractured/!Common/Managers/Scripts/MainMenuManager.cs`

**No code changes.** The existing `TransitionBetweenScreens(ScreensType, float)` already handles new enum values via the `_menuScreens` dictionary populated by `RegisterScreens()`.

### 3.3 Main menu scene (`Assets/.../MainMenu.unity` or build index 1)

Add new `MenuScreen` GameObjects under the main menu canvas, one per new `ScreensType`. Each screen has:
- `MenuScreen` component with matching `screenType`
- `CanvasGroup` for fading
- `defaultInteractable` set to its primary action button
- `backButton` set to the "Back" button

**Sonnet should not author the scene.** Sonnet writes the scripts that drive each screen. Scene wiring is the user's job, but Sonnet must document inspector requirements at the top of each new MonoBehaviour as `[Tooltip]` attributes and a `// SCENE SETUP:` comment block.

### 3.4 `Assets/FastAndFractured/!Common/Managers/Scripts/CharacterSelectorManager.cs`

Add **only** these public hooks (no behaviour change, no removal):

```csharp
// existing class — additions only

/// <summary>Fired whenever the displayed character/skin combination changes. Emits the canonical character code (e.g. "Pepe_0").</summary>
public event Action<string> OnCharacterCodeChanged;

/// <summary>Read-only canonical code of the currently displayed character (e.g. "MariaAntonia_2").</summary>
public string CurrentCharacterCode =>
    $"{allCharacters[_currentCharacterIndex].CharacterName}_{_currentSkinIndex}";

// Inside ChangeCurrentDisplayedModel(...) at the end:
//     OnCharacterCodeChanged?.Invoke(CurrentCharacterCode);
// Inside ChangeCharacter(...) at the end:
//     OnCharacterCodeChanged?.Invoke(CurrentCharacterCode);
```

Multiplayer subscribes to `OnCharacterCodeChanged`; singleplayer ignores the event. **Zero behaviour change for SP.**

### 3.5 No other singleplayer file is touched.

If you find yourself wanting to modify another singleplayer file: **stop and flag it in the PR description.**

## 4. Multiplayer Hub Controller

**File:** `Assets/Multiplayer/Lobby/MultiplayerHubController.cs`

```csharp
namespace FastAndFractured.Multiplayer.Lobby
{
    /// SCENE SETUP: place on the MULTIPLAYER_HUB MenuScreen GameObject.
    /// Wire HostButton, BrowseButton, DirectJoinButton, BackButton in inspector.
    public sealed class MultiplayerHubController : MonoBehaviour
    {
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _browseButton;
        [SerializeField] private Button _directJoinButton;
        [SerializeField] private Button _backButton;

        private void Awake() { /* wire button.onClick → MainMenuManager.TransitionBetweenScreens */ }
    }
}
```

Pure UI delegate. No network calls. No singletons created here.

## 5. Host Setup & Direct Join Screens

### 5.1 `MultiplayerHostSetupController`

**File:** `Assets/Multiplayer/Lobby/MultiplayerHostSetupController.cs`

Inputs (UI):
- `TMP_InputField` for room name (default: `"$DisplayName's Room"`)
- `TMP_InputField` for port (default: `7777`)
- `Toggle` for "Public" (LAN broadcast) / "Friends only" (LAN broadcast off; Steam later)
- `Slider`/spinner for `_aiSlotCount` initial value (0..7)
- Start Hosting button

On **Start Hosting**:
1. Resolve `ILobbyTransport` from `LobbyServiceLocator.Current` (§9).
2. Call `await transport.CreateLobbyAsync(new LobbyCreateOptions { RoomName, Port, MaxPlayers=8, InitialAiSlots, IsPublic })`.
3. On success: `MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.LOBBY_ROOM, fadeDuration)`.
4. On failure: show toast/error label; do not transition.

### 5.2 `MultiplayerDirectJoinController`

**File:** `Assets/Multiplayer/Lobby/MultiplayerDirectJoinController.cs`

Inputs: IP input field, port input field, Connect button, Back button.
On Connect: `await transport.JoinLobbyAsync(LobbyHandle.FromAddress(ip, port))`. Transition to `LOBBY_ROOM` on success.

### 5.3 `LobbyBrowserController`

**File:** `Assets/Multiplayer/Lobby/LobbyBrowserController.cs`

- Calls `transport.ListLobbiesAsync(LobbyFilter.Default)` on `OnEnable` and on a refresh button.
- Renders entries via `LobbyBrowserEntry` prefab (host name, players `X/8`, ping if available, Join button).
- Click Join → `JoinLobbyAsync(handle)` → transition.

`OnDisable` cancels in-flight refresh. No polling loop unless explicitly requested.

## 6. Lobby Room Screen

The `LOBBY_ROOM` screen is **one** `MenuScreen` containing four sub-panels (siblings, all visible at once — no nested screen system):

1. **PlayerListPanel** — driven by an enhanced `LobbyReadyUI`.
2. **CharacterPickPanel** — embeds the existing `CharacterSelectorManager` UI (the world-canvas car preview is fine; we just need it visible inside this screen). The existing `_characterDropdown` in `LobbyReadyUI` is **removed** in favour of subscribing to `CharacterSelectorManager.OnCharacterCodeChanged`.
3. **MapVotePanel** — driven by `MapVoteUI` (§7).
4. **AiSlotsPanel** — driven by `AiSlotsUI` (§8). Hidden for non-host clients.

Plus a **Ready** button and a **Force Start** button (host only) at the bottom.

### 6.1 Extending `NetworkedLobbyManager`

**File:** `Assets/Multiplayer/NetworkedLobbyManager.cs` (modify in place)

Add the following members. Keep existing ones intact.

```csharp
// === MAP VOTE ===
public readonly SyncDictionary<int, int> PlayerMapVotes = new(); // clientId → mapId

public readonly SyncVar<int> ResolvedMapId = new(-1); // set when game starts; -1 = unresolved

public event Action<int /*clientId*/, int /*mapId*/> OnPlayerMapVoteChanged;
public event Action<int /*resolvedMapId*/> OnMapResolved;

[ServerRpc(RequireOwnership = false)]
public void ServerVoteMap(int mapId, NetworkConnection conn = null);

// === AI SLOTS ===
public readonly SyncVar<int> AiSlotCount = new(0);

public const int MAX_PLAYERS = 8;

public event Action<int /*aiSlotCount*/> OnAiSlotCountChanged;

[ServerRpc(RequireOwnership = false)]
public void ServerSetAiSlotCount(int count, NetworkConnection conn = null);
//  ↑ rejects non-host. Clamps to [0, MAX_PLAYERS - HumanPlayerCount].

// === HELPERS ===
public int HumanPlayerCount => PlayerReadyStates.Count;
public int TotalSlotCount   => HumanPlayerCount + AiSlotCount.Value; // ≤ MAX_PLAYERS
public int FreeSlotCount    => MAX_PLAYERS - TotalSlotCount;
```

Behaviour:
- `ServerVoteMap`: if `GameStarted.Value`, ignore. Else `PlayerMapVotes[conn.ClientId] = mapId` (overwriting any prior vote — last vote wins).
- `ServerSetAiSlotCount`: only `conn.IsHost` or `conn.ClientId == 0` may set. Clamp to `[0, MAX_PLAYERS - HumanPlayerCount]`. Reject if `GameStarted.Value`.
- `HandleRemoteConnectionState` (existing) must also clamp `AiSlotCount` down when a human joins (so total never exceeds MAX_PLAYERS) and remove their vote when they disconnect.
- `StartGameInternal` (existing) must, **before** firing `OnGameStarting`:
  1. Resolve map vote → assign to `ResolvedMapId.Value` (§7.2).
  2. Fire `OnMapResolved`.
  3. Spawn AI placeholders into `NetworkedLevelControllerManager` (§8.2).
  4. Then continue with existing character-registration flow.

### 6.2 `LobbyReadyUI` cleanup

**File:** `Assets/Multiplayer/LobbyReadyUI.cs`

- **Remove** the `_characterDropdown` field and `OnCharacterSelected(int)` handler.
- Replace with a `Start()`-time subscription:
  ```csharp
  CharacterSelectorManager.Instance.OnCharacterCodeChanged += code =>
      _lobbyManager?.ServerSelectCharacter(code);
  ```
  Unsubscribe in `OnDestroy`.
- **Remove** the `FindObjectOfType<NetworkedLobbyManager>` polling loop. The lobby manager always exists by the time we enter `LOBBY_ROOM` (we just connected). Look it up once in `OnEnable` from `InstanceFinder.NetworkManager` via a helper `LobbyLocator.GetLobbyManager()` — see §9.4.
- Player list rendering: keep, extend each entry with the player's voted-map thumbnail + chosen character icon.

## 7. Map Vote System

### 7.1 Map Catalog

**File:** `Assets/Multiplayer/Lobby/MapCatalog.cs`

```csharp
namespace FastAndFractured.Multiplayer.Lobby
{
    [CreateAssetMenu(menuName = "FastAndFractured/Multiplayer/Map Catalog")]
    public sealed class MapCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public int Id;                  // stable, hand-assigned (don't reorder)
            public string DisplayNameKey;   // localization key
            public string SceneName;        // build-settings scene name
            public Sprite Thumbnail;
        }

        [SerializeField] private Entry[] _entries;

        public IReadOnlyList<Entry> Entries => _entries;

        public bool TryGetById(int id, out Entry entry)
        {
            for (int i = 0; i < _entries.Length; i++)
                if (_entries[i].Id == id) { entry = _entries[i]; return true; }
            entry = default; return false;
        }
    }
}
```

**Asset:** `Assets/Multiplayer/Lobby/Resources/MapCatalog.asset` (loadable via `Resources.Load`). Three initial entries matching the existing `MapSelection` sprites: GOLF2 (id 0), OBRAS (id 1), PUEBLO (id 2). The user populates the SceneName per actual build-settings scene.

### 7.2 Vote Resolution

**File:** `Assets/Multiplayer/Lobby/MapVoteResolver.cs`

```csharp
public static class MapVoteResolver
{
    public static int Resolve(IReadOnlyDictionary<int, int> votes, MapCatalog catalog, System.Random rng)
    {
        // 1. Tally votes. Players who didn't vote contribute nothing.
        // 2. If no votes at all, return random entry from catalog.
        // 3. Find max vote count. Collect all mapIds with that count.
        // 4. If single winner, return it. If tie, rng.Next over tied list.
    }
}
```

Server calls `MapVoteResolver.Resolve(PlayerMapVotes, catalog, _rng)` inside `StartGameInternal()` and writes to `ResolvedMapId.Value` **before** the scene load happens. `_rng` is `new System.Random(unchecked((int)TimeManager.LocalTick))` — deterministic per tick, ties look fair.

### 7.3 `MapVoteUI`

**File:** `Assets/Multiplayer/Lobby/MapVoteUI.cs`

- Carousel (or grid) of map thumbnails sourced from `MapCatalog`.
- One button per map: clicking calls `_lobbyManager.ServerVoteMap(mapId)`.
- Subscribes to `PlayerMapVotes.OnChange` and renders, on each map card, the count of voters + the avatar list of voters.
- Highlights the local player's currently voted map.

## 8. AI Slot System

### 8.1 `AiSlotsUI`

**File:** `Assets/Multiplayer/Lobby/AiSlotsUI.cs`

- `+` and `−` buttons.
- Disabled for non-host (`!InstanceFinder.IsServerStarted`).
- Counter label: `$"AI: {AiSlotCount.Value}    Total slots: {TotalSlotCount}/{MAX_PLAYERS}"`.
- `+` calls `ServerSetAiSlotCount(AiSlotCount.Value + 1)`. `−` calls `ServerSetAiSlotCount(AiSlotCount.Value - 1)`. Server clamps.
- Subscribes to `AiSlotCount.OnChange` and re-renders.

### 8.2 AI Spawning at Match Start

**File:** modify `Assets/Multiplayer/NetworkedLevelControllerManager.cs`

Add:
```csharp
[Server]
public void SpawnAiPlaceholders(int count, IReadOnlyList<string> aiCharacterCodes)
{
    // Reserve `count` spawn slots assigned to a synthetic "AI" connection (null or a sentinel).
    // For each AI slot, pick a character code from aiCharacterCodes (round-robin from
    // GetFallbackCharacterCode) and call AddCharacterSkin(null /*or AiConnection*/, code).
}
```

`NetworkedLobbyManager.StartGameInternal` calls this **before** `levelController.StartGame()`. The actual AI vehicle prefabs are existing AI cars wrapped with `NetworkedAIShoot` — but no `NetworkedVehicle` (AI runs entirely server-side, replicates as a regular `NetworkBehaviour` rigidbody).

⚠ **Open question for Sonnet:** the existing spawning code in `NetworkedLevelControllerManager.StartGame()` keys characters off `NetworkConnection`. AI slots have no connection. Either:
- (a) Extend the spawn data structure to carry a discriminator (`PlayerSlot { kind: Human|Ai, conn?: NetworkConnection, charCode: string }`).
- (b) Use `null` connection to mean "AI, no ownership transfer".

Pick (a) — explicit beats implicit. Refactor `_playersCharacterNameCodes` from `Dictionary<NetworkConnection, string>` to `List<PlayerSlot>` with a server-only lookup helper. Document the migration in the PR.

### 8.3 AI character selection

For now: server picks AI characters via `NetworkedLobbyManager.GetFallbackCharacterCode(humanCount + i)`. No UI for picking individual AI characters — out of scope.

## 9. Steam-Ready Abstraction Layer

### 9.1 Folder

`Assets/Multiplayer/Abstractions/`

### 9.2 Interfaces

**File:** `Assets/Multiplayer/Abstractions/ILobbyTransport.cs`

```csharp
namespace FastAndFractured.Multiplayer.Abstractions
{
    public readonly struct LobbyHandle : IEquatable<LobbyHandle>
    {
        public readonly string PlatformId;   // Steam: lobbyId, LAN: "ip:port"
        public readonly string Address;      // For direct connect (LAN); empty for Steam
        public readonly ushort Port;
        public LobbyHandle(string platformId, string address, ushort port);
        public static LobbyHandle FromAddress(string ip, ushort port);
    }

    public sealed class LobbyCreateOptions
    {
        public string RoomName;
        public ushort Port = 7777;
        public int MaxPlayers = 8;
        public int InitialAiSlots = 0;
        public bool IsPublic = true;       // LAN broadcast / Steam visibility
        public IReadOnlyDictionary<string, string> Metadata; // freeform key/value, e.g. "gamemode"="br"
    }

    public sealed class LobbyFilter
    {
        public string GameVersion;
        public IReadOnlyDictionary<string, string> RequiredMetadata;
        public static LobbyFilter Default => new() { GameVersion = Application.version };
    }

    public sealed class LobbyListing
    {
        public LobbyHandle Handle;
        public string RoomName;
        public int CurrentPlayers;
        public int MaxPlayers;
        public int PingMs;
        public IReadOnlyDictionary<string, string> Metadata;
    }

    public interface ILobbyTransport
    {
        Task<LobbyHandle> CreateLobbyAsync(LobbyCreateOptions options, CancellationToken ct = default);
        Task<bool>        JoinLobbyAsync(LobbyHandle handle, CancellationToken ct = default);
        Task              LeaveLobbyAsync(CancellationToken ct = default);
        Task<IReadOnlyList<LobbyListing>> ListLobbiesAsync(LobbyFilter filter, CancellationToken ct = default);

        void   SetLobbyMetadata(string key, string value); // host only
        string GetLobbyMetadata(string key);

        event Action<LobbyHandle> OnLobbyMemberJoined;
        event Action<LobbyHandle> OnLobbyMemberLeft;
        event Action              OnLobbyClosed;
    }
}
```

**File:** `Assets/Multiplayer/Abstractions/IPlayerIdentity.cs`

```csharp
namespace FastAndFractured.Multiplayer.Abstractions
{
    public interface IPlayerIdentity
    {
        string DisplayName { get; }
        string PlatformId  { get; } // Steam: SteamID, LAN: machine name + suffix
        Task<Texture2D> GetAvatarAsync(CancellationToken ct = default);
    }

    public interface IPlayerIdentityProvider
    {
        Task<IPlayerIdentity> GetLocalIdentityAsync(CancellationToken ct = default);
        Task<IPlayerIdentity> GetIdentityAsync(string platformId, CancellationToken ct = default);
    }
}
```

### 9.3 LAN Implementation (now)

**File:** `Assets/Multiplayer/Abstractions/Tugboat/TugboatLobbyTransport.cs`
**File:** `Assets/Multiplayer/Abstractions/Tugboat/TugboatPlayerIdentityProvider.cs`

- `CreateLobbyAsync`: starts FishNet server via `InstanceFinder.NetworkManager.ServerManager.StartConnection()`. Begins LAN UDP broadcast advertising `RoomName|Port|Players|GameVersion` on a known port (e.g. 47777) every 1s.
- `JoinLobbyAsync`: configures Tugboat with `handle.Address`/`handle.Port`, calls `ClientManager.StartConnection()`. Awaits `OnClientConnectionState == Started`.
- `ListLobbiesAsync`: opens UDP socket on broadcast port, listens for 1.5s, parses messages, deduplicates by `Address:Port`.
- `LeaveLobbyAsync`: stops connection / broadcast.
- Metadata: an in-memory dictionary on the host, broadcast as part of the discovery payload (small — keep under 512 bytes).
- `TugboatPlayerIdentityProvider.GetLocalIdentityAsync` returns `Environment.UserName` + machine GUID. Avatar: a default texture from `Resources/UI/DefaultAvatar.png`.

### 9.4 Service Locator

**File:** `Assets/Multiplayer/Abstractions/LobbyServiceLocator.cs`

```csharp
public static class LobbyServiceLocator
{
    public static ILobbyTransport       Transport        { get; private set; }
    public static IPlayerIdentityProvider IdentityProvider { get; private set; }

    public static void Configure(ILobbyTransport transport, IPlayerIdentityProvider identity)
    {
        Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        IdentityProvider = identity ?? throw new ArgumentNullException(nameof(identity));
    }
}
```

**File:** `Assets/Multiplayer/Abstractions/LobbyServiceBootstrapper.cs`

```csharp
// Auto-runs once at app start. Wires the LAN implementation by default.
public static class LobbyServiceBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (LobbyServiceLocator.Transport != null) return;
        LobbyServiceLocator.Configure(
            new TugboatLobbyTransport(),
            new TugboatPlayerIdentityProvider());
    }
}
```

When Steam ships, ship a single new file `SteamLobbyServiceBootstrapper.cs` (or define-guarded `#if STEAMWORKS_NET`) that calls `LobbyServiceLocator.Configure(new SteamLobbyTransport(), new SteamPlayerIdentityProvider())` with a higher priority. **No other file changes.** That is the entire seam.

### 9.5 Helper: lobby manager lookup

**File:** `Assets/Multiplayer/Abstractions/LobbyLocator.cs`

```csharp
public static class LobbyLocator
{
    public static NetworkedLobbyManager GetLobbyManager() =>
        InstanceFinder.NetworkManager != null
            ? InstanceFinder.NetworkManager.GetComponentInChildren<NetworkedLobbyManager>()
            : null;
}
```

Replaces every `FindObjectOfType<NetworkedLobbyManager>` in `Assets/Multiplayer/`.

## 10. Match Start Hand-off

The current flow loads the game scene by build index. Replace with scene-name lookup from `MapCatalog`:

**File:** `Assets/Multiplayer/NetworkedLevelControllerManager.cs` (modify in place)

In `StartGame()` (server-only), after AI placeholders are spawned and characters are registered:

```csharp
[Server]
private void LoadResolvedSceneAndStart()
{
    var catalog = MapCatalogLoader.Get(); // Resources.Load on first use, cached
    if (!catalog.TryGetById(_lobbyManager.ResolvedMapId.Value, out var entry))
    {
        Debug.LogError($"Resolved mapId {_lobbyManager.ResolvedMapId.Value} not in catalog. Aborting.");
        return;
    }
    var sceneLoadData = new SceneLoadData(entry.SceneName) { ReplaceScenes = ReplaceOption.All };
    InstanceFinder.SceneManager.LoadGlobalScenes(sceneLoadData);
}
```

`MapCatalogLoader` lives in `Assets/Multiplayer/Lobby/MapCatalogLoader.cs` — single static, single `Resources.Load<MapCatalog>("MapCatalog")` cached.

The existing `LoadScene(2)` path stays for backwards compat as a fallback when `ResolvedMapId.Value < 0`.

## 11. New Files

| Path | Type | Owner |
|---|---|---|
| `Assets/Multiplayer/Lobby/MultiplayerHubController.cs` | UI controller | Sonnet |
| `Assets/Multiplayer/Lobby/MultiplayerHostSetupController.cs` | UI controller | Sonnet |
| `Assets/Multiplayer/Lobby/MultiplayerDirectJoinController.cs` | UI controller | Sonnet |
| `Assets/Multiplayer/Lobby/LobbyBrowserController.cs` | UI controller | Sonnet |
| `Assets/Multiplayer/Lobby/LobbyBrowserEntry.cs` | UI item | Sonnet |
| `Assets/Multiplayer/Lobby/MapVoteUI.cs` | UI controller | Sonnet |
| `Assets/Multiplayer/Lobby/AiSlotsUI.cs` | UI controller | Sonnet |
| `Assets/Multiplayer/Lobby/MapCatalog.cs` | ScriptableObject | Sonnet |
| `Assets/Multiplayer/Lobby/MapCatalogLoader.cs` | Static | Sonnet |
| `Assets/Multiplayer/Lobby/MapVoteResolver.cs` | Pure logic | Sonnet |
| `Assets/Multiplayer/Lobby/PlayerSlot.cs` | Struct | Sonnet |
| `Assets/Multiplayer/Lobby/Resources/MapCatalog.asset` | Asset | User (Unity Editor) |
| `Assets/Multiplayer/Abstractions/ILobbyTransport.cs` | Interface + DTOs | Sonnet |
| `Assets/Multiplayer/Abstractions/IPlayerIdentity.cs` | Interface | Sonnet |
| `Assets/Multiplayer/Abstractions/LobbyServiceLocator.cs` | Static | Sonnet |
| `Assets/Multiplayer/Abstractions/LobbyServiceBootstrapper.cs` | Static + RIOLM | Sonnet |
| `Assets/Multiplayer/Abstractions/LobbyLocator.cs` | Static | Sonnet |
| `Assets/Multiplayer/Abstractions/Tugboat/TugboatLobbyTransport.cs` | LAN impl | Sonnet |
| `Assets/Multiplayer/Abstractions/Tugboat/TugboatPlayerIdentityProvider.cs` | LAN impl | Sonnet |
| `Assets/Multiplayer/Tests/Editor/MapVoteResolverTests.cs` | Tests | Sonnet |
| `Assets/Multiplayer/Tests/Editor/AiSlotClampTests.cs` | Tests | Sonnet |
| `Assets/Multiplayer/Tests/Editor/LobbyTransportContractTests.cs` | Tests | Sonnet |

## 12. Modified Files

| Path | What changes | Risk |
|---|---|---|
| `Assets/FastAndFractured/!Common/EnumLibrary/EnumLibrary.cs` | Add 6 enum values to `ScreensType` | None |
| `Assets/FastAndFractured/!Common/Managers/Scripts/CharacterSelectorManager.cs` | Add `OnCharacterCodeChanged` event + `CurrentCharacterCode` getter; raise event in two places | Low — pure addition |
| `Assets/Multiplayer/NetworkedLobbyManager.cs` | Map vote SyncDict + ServerRpc, AI slot SyncVar + ServerRpc, modify `StartGameInternal` ordering, modify `HandleRemoteConnectionState` to clamp | Medium — ordering matters |
| `Assets/Multiplayer/NetworkedLevelControllerManager.cs` | Refactor `_playersCharacterNameCodes` from dict-by-connection to `List<PlayerSlot>`, add `SpawnAiPlaceholders`, add `LoadResolvedSceneAndStart` | High — invariants change; cover with tests |
| `Assets/Multiplayer/LobbyReadyUI.cs` | Remove dropdown, subscribe to `CharacterSelectorManager.OnCharacterCodeChanged`, replace `FindObjectOfType` polling with `LobbyLocator.GetLobbyManager` | Low |

Every other file in `Assets/Multiplayer/` that calls `FindObjectOfType<NetworkedLobbyManager>` migrates to `LobbyLocator.GetLobbyManager()`. Grep for it and update — no behaviour change.

## 13. Implementation Order (PR Breakdown)

Each step independently mergeable + test-passing. Land in order; do not merge step N until step N-1 is green.

### PR 1 — Abstractions + LAN parity (no behaviour change)
- §9 in full. Bootstrapper wires LAN. `LobbyLocator` introduced.
- Existing host/join paths refactored to call `LobbyServiceLocator.Transport`. UI not yet rewritten.
- Tests: `LobbyTransportContractTests` exercises Create/List/Join/Leave round-trip against `TugboatLobbyTransport` using two in-process FishNet instances.
- **Acceptance:** ParrelSync clones still host/join exactly as before.

### PR 2 — `MultiplayerHub` + Host/Direct Join screens
- §3.1 enum, §4, §5. New screens added to scene by user.
- Old "Multiplayer" entry button on main menu now routes to `MULTIPLAYER_HUB`.
- **Acceptance:** Host → Direct Join still puts two clones into a working game with the existing lobby-ready flow.

### PR 3 — Map vote
- §7 in full. `NetworkedLobbyManager` extended for map vote only (no AI yet).
- `LOBBY_MAP_VOTE` is **not** a separate screen — `MapVoteUI` lives inside `LOBBY_ROOM`. The enum value exists for future expansion if the screen needs to be standalone; for v1 it's unused.
- Tests: `MapVoteResolverTests` (single winner, tie + rng, no votes, missing players, invalid mapId).
- **Acceptance:** 2 clones vote different maps; host vote breaks tie deterministically based on tick seed.

### PR 4 — AI slots
- §8 + §10. `PlayerSlot` refactor in `NetworkedLevelControllerManager`.
- Tests: `AiSlotClampTests` (clamps on add player, clamps on remove player, host-only RPC, rejects after `GameStarted`).
- **Acceptance:** 1 host + 0 clients + 7 AI slots → match starts with 8 vehicles.

### PR 5 — `LobbyBrowser` (LAN)
- §5.3. UDP broadcast in `TugboatLobbyTransport` (added in PR1 as a stub) is now functional.
- **Acceptance:** Two clones on the same machine: host advertises, second clone sees the lobby in its browser, joins.

### PR 6 — Polish + integration test
- Player avatars on lobby entries (via `IPlayerIdentity.GetAvatarAsync`).
- Vote-progress indicators on `MapVoteUI`.
- Smoke test: full flow MainMenu → Hub → Host → LobbyRoom → vote → ready → game.

## 14. Test Plan

### Editor tests (no Play mode required)
- `MapVoteResolver`: 8 unit tests covering tally, single winner, tie + rng determinism, empty votes, all-different votes, mapId not in catalog (filtered out).
- `AiSlotClampTests`: validates `ServerSetAiSlotCount` clamping logic via reflection on a non-spawned instance, OR extracted as pure function.
- `LobbyTransportContractTests`: every `ILobbyTransport` impl must pass the same contract suite (`CreateLobbyAsync` succeeds → `ListLobbiesAsync` includes it → `JoinLobbyAsync` connects → `LeaveLobbyAsync` cleans up). Useful sanity for the future Steam impl.
- Existing `MultiplayerComponentTests` regressions kept green.

### Play-mode / manual
- 2 ParrelSync clones: host + join, vote different maps, verify resolution shown identically on both.
- 1 host + 7 AI slots → 8 vehicles in scene.
- 1 host + 1 client + 6 AI slots → 8 vehicles. Host wins lobby; if client disconnects, AI count clamps if needed.
- Direct IP join from a different machine on LAN: works.
- LAN browser: host appears within 2s of starting; joining works.

## 15. Decisions Deferred to Sonnet (with Default)

| Question | Default if Sonnet doesn't choose |
|---|---|
| Async pattern: `Task` vs `UniTask`? | `Task` (no extra dep). If project already uses UniTask, switch the interface signatures to `UniTask`. |
| Where do AI character codes come from at spawn time? | `NetworkedLobbyManager.GetFallbackCharacterCode(humanCount + i)` for now. |
| LAN broadcast port? | `47777` UDP. Configurable via `TugboatLobbyTransport` ctor. |
| Should `LOBBY_ROOM` lock cursor like the game scene? | No. Cursor unlocked, visible, EventSystem keyboard nav enabled. |
| If a player joins mid-countdown (via direct IP)? | Cancel countdown on the server; force everyone back to "not ready". Mirrors the existing `MinPlayersToStart` cancel logic. |
| Steam metadata format for `LobbyCreateOptions.Metadata`? | UTF-8 JSON serialized into a single Steam lobby data key `"meta"`. LAN serializes the same way in the broadcast payload. |

## 16. Out of Scope (do not implement)

- Host migration.
- Voice chat.
- In-lobby chat.
- Anti-cheat beyond the existing damage/heal clamps.
- Cosmetic skins beyond what `CharacterSelectorManager` already exposes.
- Steam implementation (just the seam).
- Map preview cinematics.

## 17. Definition of Done for Multiplayer PR

- All PRs 1–6 merged.
- `Assets/Multiplayer/Tests/Editor/` is green (`Window > General > Test Runner > EditMode > Run All`).
- 2-clone manual smoke test passes the "Play-mode / manual" checklist (§14).
- No new `Debug.Log` in hot paths; only `Debug.LogWarning`/`Debug.LogError` for genuine anomalies.
- No `FindObjectOfType` calls in `Assets/Multiplayer/` (use `LobbyLocator`).
- `MEMORY.md` updated with one new entry: `multiplayer_lobby_architecture.md` summarizing the screen flow + the Steam seam.
