using System.Collections.Generic;

namespace FastAndFractured
{
    public readonly struct DebugFrameStats
    {
        private const float MILLISECONDS_PER_SECOND = 1000f;
        private const float HIGH_PERCENTILE = 0.95f;
        private const float WORST_PERCENTILE = 0.99f;

        public readonly int Frames;
        public readonly float Mean;
        public readonly float Median;
        public readonly float P95;
        public readonly float P99;
        public readonly float Worst;

        public float Fps => Mean > 0f ? MILLISECONDS_PER_SECOND / Mean : 0f;

        private DebugFrameStats(int frames, float mean, float median, float p95, float p99, float worst)
        {
            Frames = frames;
            Mean = mean;
            Median = median;
            P95 = p95;
            P99 = p99;
            Worst = worst;
        }

        public static DebugFrameStats Of(IReadOnlyList<float> milliseconds)
        {
            if (milliseconds == null || milliseconds.Count == 0) return default;
            var sorted = new List<float>(milliseconds);
            sorted.Sort();
            double sum = 0d;
            foreach (float frame in sorted) sum += frame;
            int count = sorted.Count;
            return new DebugFrameStats(count, (float)(sum / count), At(sorted, 0.5f), At(sorted, HIGH_PERCENTILE), At(sorted, WORST_PERCENTILE), sorted[count - 1]);
        }

        private static float At(List<float> sorted, float percentile)
        {
            int index = (int)(sorted.Count * percentile);
            return sorted[index < sorted.Count ? index : sorted.Count - 1];
        }
    }
}
