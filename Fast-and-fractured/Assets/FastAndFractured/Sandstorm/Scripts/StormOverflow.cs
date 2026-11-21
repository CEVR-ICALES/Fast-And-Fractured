using UnityEngine;

namespace FastAndFractured
{
    public static class StormOverflow
    {
        public const float FULL_BAR = 1f;
        public const float EXPLODES_AT = 10f;
        public const float SECONDS_FROM_FULL_BAR = 5f;
        private const float TOLERANCE = 0.0001f;

        public static bool Explodes(float damage01) => damage01 >= EXPLODES_AT - TOLERANCE;

        public static float Grow(float damage01, float seconds)
        {
            float from = Mathf.Clamp(damage01, FULL_BAR, EXPLODES_AT);
            float grown = from * Mathf.Pow(EXPLODES_AT / FULL_BAR, Mathf.Max(0f, seconds) / SECONDS_FROM_FULL_BAR);
            return Mathf.Min(EXPLODES_AT, grown);
        }

        public static float SecondsToExplode(float damage01)
        {
            float from = Mathf.Clamp(damage01, FULL_BAR, EXPLODES_AT);
            return SECONDS_FROM_FULL_BAR * Mathf.Log(EXPLODES_AT / from) / Mathf.Log(EXPLODES_AT / FULL_BAR);
        }
    }
}
