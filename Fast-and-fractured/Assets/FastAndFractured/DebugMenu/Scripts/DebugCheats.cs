using System;
using System.Collections.Generic;
using UnityEngine;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured
{
    public static class DebugCheats
    {
        private static readonly HashSet<GameObject> _killed = new HashSet<GameObject>();

        public static Func<bool> IsNetworkSession { get; set; }

        public static bool InNetworkSession => IsNetworkSession != null && IsNetworkSession();

        public static bool Allowed(bool hasMatch, bool inNetworkSession) => hasMatch && !inNetworkSession;

        public static StatsController LocalStats(LevelController level) =>
            level != null && level.LocalPlayer != null ? level.LocalPlayer.GetComponent<StatsController>() : null;

        public static int KillRivals(LevelController level)
        {
            if (level == null) return 0;
            _killed.RemoveWhere(character => character == null);
            int killed = 0;
            foreach (GameObject character in new List<GameObject>(level.InGameCharacters))
            {
                if (!IsRivalToKill(character, level.LocalPlayer, _killed.Contains(character))) continue;
                if (!character.TryGetComponent(out StatsController stats)) continue;
                _killed.Add(character);
                stats.DebugGodMode = false;
                stats.DebugDie();
                killed++;
            }
            return killed;
        }

        public static bool IsRivalToKill(GameObject character, GameObject localPlayer, bool alreadyKilled) =>
            character != null && character != localPlayer && !alreadyKilled && character.activeInHierarchy;

        public static List<GameObject> Rivals(LevelController level)
        {
            var rivals = new List<GameObject>();
            if (level == null) return rivals;
            foreach (GameObject character in level.InGameCharacters)
                if (character != null && character != level.LocalPlayer) rivals.Add(character);
            return rivals;
        }

        public static void PauseRivals(LevelController level, bool paused)
        {
            foreach (GameObject rival in Rivals(level))
                foreach (IPausable pausable in rival.transform.root.GetComponentsInChildren<IPausable>(true))
                {
                    if (paused) pausable.OnPause();
                    else pausable.OnResume();
                }
        }

        public static void DisarmRivals(LevelController level, bool disarmed)
        {
            foreach (GameObject rival in Rivals(level))
            {
                Transform root = rival.transform.root;
                foreach (ShootingHandle handle in root.GetComponentsInChildren<ShootingHandle>(true)) handle.DebugCannotShoot = disarmed;
            }
        }

        public static void ShieldRivals(LevelController level, bool shielded)
        {
            foreach (GameObject rival in Rivals(level))
                if (rival.TryGetComponent(out StatsController stats)) stats.DebugGodMode = shielded;
        }

        public static int RemoveRivals(LevelController level)
        {
            int removed = 0;
            foreach (GameObject rival in Rivals(level))
            {
                GameObject root = rival.transform.root.gameObject;
                if (!root.activeSelf) continue;
                root.SetActive(false);
                removed++;
            }
            return removed;
        }

        public static void FreePlayerWeapons(LevelController level, bool noOverheat, bool noCooldown)
        {
            GameObject player = level != null ? level.LocalPlayer : null;
            if (player == null) return;
            Transform root = player.transform.root;
            foreach (NormalShootHandle handle in root.GetComponentsInChildren<NormalShootHandle>(true)) handle.DebugNoOverheat = noOverheat;
            foreach (BaseUniqueAbility ability in root.GetComponentsInChildren<BaseUniqueAbility>(true)) ability.DebugNoCooldown = noCooldown;
        }

        public static bool BringPlayerTo(LevelController level, Vector3 position, float yaw)
        {
            GameObject player = level != null ? level.LocalPlayer : null;
            if (player == null) return false;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Rigidbody body = player.GetComponentInParent<Rigidbody>();
            if (body == null)
            {
                player.transform.SetPositionAndRotation(position, rotation);
                return true;
            }
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            body.transform.SetPositionAndRotation(position, rotation);
            return true;
        }
    }
}
