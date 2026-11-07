# Portable Unity Audio

`Utilities.Audio` contains the contracts and catalog. It has no FMOD, FishNet or gameplay dependency. Backends live in separate assemblies.

## Typed gameplay references

```csharp
IAudioVoice voice = GameplayAudio.Play(GameAudioCues.CommonDash, transform.position);
voice?.SetPitch(1.1f);
voice?.Stop();
```

For direct service use:

```csharp
var options = AudioPlaybackOptions.At(transform.position, 0.8f);
options.Follow = gameObject;
options.LoopPolicy = AudioLoopPolicy.RequireLoop;
IAudioVoice voice = AudioServices.Play(GameAudioCues.VehicleMovementEventsGolfCartEngine, options);
voice?.TrySetParameterLabel("CarState", "Running");
```

`GameAudioCues` is generated project code. Other projects can generate their own class. `AudioCueId` is an equatable logical identity, not a backend path, and does not implicitly convert from string. String values remain in serialized catalog data and generated definitions, not hand-written gameplay calls.

There are four independent fields per catalog entry:

- `id`: immutable logical identity, retained across migration even if its old spelling resembles a path.
- `codeName`: stable generated C# symbol, such as `CommonDash`.
- `displayName`: editable human-readable label.
- Backend binding: opaque stable identity plus a current path hint. In FMOD, the stable identity is the event GUID.

Unknown, empty or duplicate logical IDs do not play. A missing catalog is not silently interpreted as a backend path. Bindings are terminal resources, not recursive aliases.

## FMOD changes

- **Change event content:** build banks in Studio and refresh banks in Unity.
- **Rename or move the same event:** its GUID remains authoritative. After Unity refreshes its FMOD cache, the editor synchronizes the catalog's displayed path by GUID. Logical IDs and generated symbols do not change, and gameplay does not need edits.
- **Delete and recreate an event:** the new GUID is a different identity. The old binding stays missing until explicitly reassigned; reusing its old name does not silently rebind it.

The FMOD adapter can resolve an older path-only resource during migration. Normal migrated entries have stable identities. The core contains no native FMOD GUID or EventReference type.

## Catalog and editor

Project catalog: `Assets/Resources/GameAudioCatalog.asset`.
Open **Tools > Fast And Fractured > Audio References** and search `CommonDash`.

- Global rows edit the catalog binding used by ID-based callers. They do not replace independent legacy fields.
- C# usage rows recognize generated symbols and open the source line; they never rewrite gameplay.
- Serialized references edit their real asset, prefab, scene or Timeline subasset. Native FMOD fields update GUID and Path together.
- Inherited references require an explicit local override/shared-source choice.
- Open scenes remain dirty with Undo. Closed scenes require confirmation to save only that scene. Temporary contexts do not promise persistent Undo after unloading.
- **Play Assigned**, **Play Candidate**, and **Choose Audio > Play/Stop** audition audio in Edit Mode without applying changes. **Select**, then **Apply to Source**, commits a choice.

**Create / Add Cues** preserves existing selections and identities. **Tools > Fast And Fractured > Generate Audio Keys** regenerates the project accessors. Backend-path/display-only changes leave the generated file unchanged. Invalid or colliding C# names are reported, not silently assigned to another cue.

Indexing is incremental and cancelable; cancelled results are partial. Packages, generated caches, dynamic IDs and internal bank samples are outside automatic editing coverage. Studio still owns an event's internal sample arrangement.

## Presentation tuning

Game-specific tuning lives in `Assets/Resources/AudioPresentationProfile.asset`, separate from cue identities and the portable Core. Its typed sections cover vehicle engine/collision/falling response, UI gain/pitch/hover spacing, ability layers shared by SP and MP, chickens, and generated map zones. Values include their units in names or Inspector tooltips. Gains above one are supported.

Each consumer uses its own section. Cooldown history, voice ownership and movement state remain on the consumer, never in the shared asset. Technical limits and independent anti-repeat policies are named local constants, not design settings. Windmill loop gain remains a serialized field on the existing Windmill component.

The profile is loaded once from Resources; a missing asset produces a warning and an in-memory default profile, without creating gameplay singletons. Gameplay cue IDs, bindings and FMOD-authored sample arrangements are unchanged by presentation tuning.

## Lifecycle and compatibility

The composition root registers `AudioServices.Current` and `AudioServices.Catalog`. The host drives `Tick` and owns disposal. Temporary test overrides do not dispose the previous service. `SoundManager` installs the current backend/catalog and preserves volume and pause callbacks; hot-swapping a backend mid-match is not supported.

`SuspendCue` returns a nested pause scope. Results use it to prevent overlapping menu music and restore playback without changing volume preferences. External emitter instances remain owned by their emitter.

`*.FmodCompatibility.cs`, old serialized references and native Timeline tracks remain migration bridges. Removing FMOD entirely still requires converting those assets and providing another backend; this abstraction does not uninstall the plugin.
