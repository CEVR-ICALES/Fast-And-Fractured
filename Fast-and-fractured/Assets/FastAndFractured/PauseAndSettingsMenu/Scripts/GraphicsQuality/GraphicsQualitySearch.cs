using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public sealed class GraphicsQualitySearch
    {
        private const int LOW_VRAM_MB = 3000;
        private const int MID_VRAM_MB = 5000;
        private const int HIGH_VRAM_MB = 7000;
        private const int LOW_RAM_MB = 8000;
        private const int INTEGRATED_START = 1;
        private const int LOW_VRAM_START = 1;
        private const int MID_VRAM_START = 2;
        private const int HIGH_VRAM_START = 3;
        private const int TOP_VRAM_START = 4;
        private const int LOW_RAM_MAX_START = 2;

        private static readonly string[] INTEGRATED_GPU_MARKERS =
        {
            "radeon(tm) graphics", "radeon graphics", "vega", "680m", "660m", "760m", "780m", "890m", "880m",
            "uhd graphics", "hd graphics", "iris",
        };

        private readonly float _budgetMs;
        private int _bestPassing = -1;
        private int _lowestFailing;

        public int Current { get; private set; }
        public bool IsDone { get; private set; }
        public int Result => Mathf.Max(0, _bestPassing);

        public GraphicsQualitySearch(int levelCount, int startLevel, float budgetMs)
        {
            _budgetMs = budgetMs;
            _lowestFailing = Mathf.Max(1, levelCount);
            Current = GraphicsQuality.ClampLevel(startLevel, levelCount);
        }

        public void Report(float frameMs)
        {
            if (IsDone)
                return;

            if (frameMs <= _budgetMs)
            {
                _bestPassing = Mathf.Max(_bestPassing, Current);
                if (Current + 1 >= _lowestFailing)
                    IsDone = true;
                else
                    Current++;
                return;
            }

            _lowestFailing = Mathf.Min(_lowestFailing, Current);
            if (_bestPassing >= 0 || Current == 0)
                IsDone = true;
            else
                Current--;
        }

        public static int StartLevel(int levelCount, int vramMb, int ramMb, bool integratedGpu)
        {
            int start;
            if (integratedGpu) start = INTEGRATED_START;
            else if (vramMb < LOW_VRAM_MB) start = LOW_VRAM_START;
            else if (vramMb < MID_VRAM_MB) start = MID_VRAM_START;
            else if (vramMb < HIGH_VRAM_MB) start = HIGH_VRAM_START;
            else start = TOP_VRAM_START;

            if (ramMb > 0 && ramMb < LOW_RAM_MB)
                start = Mathf.Min(start, LOW_RAM_MAX_START);
            return GraphicsQuality.ClampLevel(start, levelCount);
        }

        public static bool LooksIntegrated(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
                return false;
            string name = deviceName.ToLowerInvariant();
            if (name.Contains("intel") && name.Contains("arc"))
                return false;
            foreach (string marker in INTEGRATED_GPU_MARKERS)
                if (name.Contains(marker))
                    return true;
            return false;
        }

        public static float Percentile(List<float> samples, float percentile)
        {
            if (samples == null || samples.Count == 0)
                return 0f;
            var sorted = new List<float>(samples);
            sorted.Sort();
            int index = Mathf.Clamp(Mathf.CeilToInt(percentile * sorted.Count) - 1, 0, sorted.Count - 1);
            return sorted[index];
        }
    }
}
