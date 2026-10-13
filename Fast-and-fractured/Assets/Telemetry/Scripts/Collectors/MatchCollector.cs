using System.Collections.Generic;
using FastAndFractured.Multiplayer;
using FastAndFractured.Multiplayer.Progression;
using FishNet;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.GameTelemetry
{
    /// <summary>
    /// Matches, singleplayer and multiplayer: start (map, character, how many), end (won, time,
    /// kills, damage, placement, XP, frame rate) or abandoned (left before the end). Listens to
    /// the level's own start event, GameplayEvents.MatchEnded and the map scene unloading.
    /// </summary>
    public sealed class MatchCollector : MonoBehaviour
    {
        private sealed class Match
        {
            public Scene scene;
            public string map, mode, role;
            public float start;
            public bool started, ended;
            public int characters, awardBefore;
            public double frameTime;
            public int frames;
            public float worstSecondFps = float.MaxValue, secondTime;
            public int secondFrames;
        }

        private static MatchCollector _instance;
        private static Match _match;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; _match = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[Telemetry] Match");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            _instance = go.AddComponent<MatchCollector>();
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            SceneManager.sceneUnloaded -= OnUnloaded;
            SceneManager.sceneUnloaded += OnUnloaded;
            GameplayEvents.MatchEnded -= OnEnded;
            GameplayEvents.MatchEnded += OnEnded;
            Telemetry.Quitting -= OnQuitting;
            Telemetry.Quitting += OnQuitting;
            OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneUnloaded -= OnUnloaded;
            GameplayEvents.MatchEnded -= OnEnded;
            Telemetry.Quitting -= OnQuitting;
        }

        // ── Start ──────────────────────────────────────────────────────────────────────
        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.buildIndex <= LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX || TutorialCollector.IsTutorial(scene)) return;
            if (!LevelController.HasInstance || (_match != null && !_match.ended)) return;
            bool online = InstanceFinder.IsClientStarted || InstanceFinder.IsServerStarted;
            _match = new Match
            {
                scene = scene,
                map = MapName(scene.name),
                mode = online ? "multiplayer" : "singleplayer",
                role = !online ? "" : InstanceFinder.IsServerStarted ? "host" : "client",
                start = Time.realtimeSinceStartup,
                awardBefore = PlayerProgression.LastAward?.TotalAfter ?? -1,
            };
            var level = LevelController.Instance;
            level.charactersCustomStart?.AddListener(OnCharactersStart);
        }

        private static void OnCharactersStart()
        {
            if (_match == null || _match.started) return;
            _match.started = true;
            _match.start = Time.realtimeSinceStartup;   // the race, not the loading
            _match.characters = LevelController.HasInstance ? LevelController.Instance.InGameCharacters.Count : 0;
            var (character, skin) = LocalCharacter();
            Telemetry.Track(TelemetryKeys.MATCH_START,
                ("mode", _match.mode), ("role", _match.role), ("map", _match.map),
                ("character", character), ("skin", skin), ("characters", _match.characters));
        }

        // ── End ────────────────────────────────────────────────────────────────────────
        private static void OnEnded(bool won)
        {
            if (_match == null || _match.ended) return;
            _match.ended = true;
            var (character, skin) = LocalCharacter();
            var data = new Dictionary<string, object>
            {
                ["mode"] = _match.mode,
                ["role"] = _match.role,
                ["map"] = _match.map,
                ["character"] = character,
                ["skin"] = skin,
                ["won"] = won,
                ["duration_s"] = Mathf.RoundToInt(Time.realtimeSinceStartup - _match.start),
                ["characters"] = _match.characters,
            };
            AddPerformance(data);

            if (MatchResults.TryGetLocal(out var mine) && MatchResults.Latest != null)
            {
                // Multiplayer: the server's table.
                int humans = 0;
                foreach (var entry in MatchResults.Latest) if (entry.IsHuman) humans++;
                data["placement"] = mine.Placement;
                data["participants"] = MatchResults.Latest.Length;
                data["humans"] = humans;
                data["kills"] = mine.Kills;
                data["damage_dealt"] = mine.DamageDealt;
                data["eliminated_by_player"] = !string.IsNullOrEmpty(mine.EliminatedBy);
            }
            else if (LevelController.HasInstance && LevelController.Instance.LocalPlayer != null &&
                     LevelController.Instance.LocalPlayer.TryGetComponent<StatsController>(out var stats))
            {
                data["kills"] = stats.totalKills;
                data["damage_dealt"] = Mathf.RoundToInt(stats.totalDamageDealt);
                data["damage_taken"] = Mathf.RoundToInt(stats.totalDamageTaken);
                data["distance_m"] = Mathf.RoundToInt(stats.totalDistanceDriven);
            }

            var award = PlayerProgression.LastAward;
            if (award.HasValue && award.Value.TotalAfter != _match.awardBefore)
            {
                data["xp"] = award.Value.Xp;
                data["level"] = award.Value.LevelAfter;
            }

            Telemetry.Track(TelemetryKeys.MATCH_END, data);
            Telemetry.Flush();
        }

        private static void OnUnloaded(Scene scene)
        {
            if (_match == null || scene != _match.scene) return;
            if (!_match.ended) Abandon();
            _match = null;
        }

        private static void OnQuitting()
        {
            if (_match != null && !_match.ended) Abandon();
        }

        private static void Abandon()
        {
            _match.ended = true;
            var (character, skin) = LocalCharacter();
            var data = new Dictionary<string, object>
            {
                ["mode"] = _match.mode,
                ["role"] = _match.role,
                ["map"] = _match.map,
                ["character"] = character,
                ["skin"] = skin,
                ["started"] = _match.started,
                ["duration_s"] = Mathf.RoundToInt(Time.realtimeSinceStartup - _match.start),
            };
            AddPerformance(data);
            Telemetry.Track(TelemetryKeys.MATCH_ABANDONED, data);
            Telemetry.Flush();
        }

        // ── Frame rate during the race ─────────────────────────────────────────────────
        private void Update()
        {
            if (_match == null || !_match.started || _match.ended) return;
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f || dt > 1f) return;   // pauses and hitches from loading are not the game's frame rate
            _match.frameTime += dt;
            _match.frames++;
            _match.secondTime += dt;
            _match.secondFrames++;
            if (_match.secondTime >= 1f)
            {
                _match.worstSecondFps = Mathf.Min(_match.worstSecondFps, _match.secondFrames / _match.secondTime);
                _match.secondTime = 0f;
                _match.secondFrames = 0;
            }
        }

        private static void AddPerformance(Dictionary<string, object> data)
        {
            if (_match.frames < 30) return;
            data["fps_avg"] = Mathf.RoundToInt((float)(_match.frames / _match.frameTime));
            if (_match.worstSecondFps < float.MaxValue) data["fps_min"] = Mathf.RoundToInt(_match.worstSecondFps);
        }

        // ── Helpers ────────────────────────────────────────────────────────────────────
        /// <summary>"NetworkedGolfMap" and "GolfMap" are both "Golf".</summary>
        public static string MapName(string sceneName)
        {
            string name = sceneName ?? "";
            if (name.StartsWith("Networked")) name = name.Substring("Networked".Length);
            if (name.EndsWith("Map") && name.Length > 3) name = name.Substring(0, name.Length - 3);
            return name;
        }

        /// <summary>The local player's character and skin ("Pepe_0" → Pepe, 0).</summary>
        private static (string character, string skin) LocalCharacter()
        {
            string code = null;
            if (_match != null && _match.mode == "multiplayer")
            {
                var wrapper = FindFirstObjectByType<NetworkedLevelControllerWrapper>();
                if (wrapper != null) code = wrapper.LocalPlayerNameCode;
            }
            if (string.IsNullOrEmpty(code)) code = PlayerPrefs.GetString("Selected_Player", "");
            return SplitCode(code);
        }

        public static (string character, string skin) SplitCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return ("unknown", "");
            int cut = code.LastIndexOf('_');
            return cut > 0 ? (code.Substring(0, cut), code.Substring(cut + 1)) : (code, "");
        }
    }
}
