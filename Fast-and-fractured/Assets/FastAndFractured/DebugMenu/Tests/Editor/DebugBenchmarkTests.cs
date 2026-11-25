using NUnit.Framework;

namespace FastAndFractured.Tests
{
    public sealed class DebugBenchmarkTests
    {
        private const float TOLERANCE = 0.001f;
        private const string CONSTRUCTION = "Assets/FastAndFractured/!Environment/Scenes/!ConstructionMap/ConstructionMap.unity";
        private const string GOLF = "Assets/FastAndFractured/!Environment/Scenes/GolfMap/GolfMap.unity";
        private const string TRAINING = "Assets/FastAndFractured/Training/Scenes/TrainingScene.unity";
        private const string NETWORKED = "Assets/Multiplayer/Scenes/NetworkedConstructionMap.unity";

        [Test]
        public void BenchmarkOnlyRunsWhenAsked()
        {
            Assert.IsFalse(DebugBenchmarkOptions.From(new[] { "game.exe" }).Requested);
            Assert.IsFalse(DebugBenchmarkOptions.From(null).Requested);
            Assert.IsTrue(DebugBenchmarkOptions.From(new[] { "game.exe", DebugBenchmarkOptions.COMMAND_LINE_FLAG }).Requested);
        }

        [Test]
        public void BenchmarkStartsWithItsDefaults()
        {
            DebugBenchmarkOptions options = DebugBenchmarkOptions.From(new[] { "game.exe", DebugBenchmarkOptions.COMMAND_LINE_FLAG });

            Assert.AreEqual(DebugBenchmarkOptions.DEFAULT_SECONDS, options.Seconds, TOLERANCE);
            Assert.AreEqual(DebugBenchmarkOptions.DEFAULT_MOOD, options.Mood);
            Assert.AreEqual(DebugBenchmarkOptions.DEFAULT_RIVALS, options.Rivals);
            Assert.IsEmpty(options.Maps);
        }

        [Test]
        public void BenchmarkReadsItsOptions()
        {
            DebugBenchmarkOptions options = DebugBenchmarkOptions.From(new[]
            {
                "game.exe", DebugBenchmarkOptions.COMMAND_LINE_FLAG, DebugBenchmarkOptions.MAPS_FLAG, "construction,Golf",
                DebugBenchmarkOptions.SECONDS_FLAG, "12.5", DebugBenchmarkOptions.MOOD_FLAG, "3", DebugBenchmarkOptions.RIVALS_FLAG, "2"
            });

            Assert.AreEqual(new[] { "construction", "Golf" }, options.Maps);
            Assert.AreEqual(12.5f, options.Seconds, TOLERANCE);
            Assert.AreEqual(3, options.Mood);
            Assert.AreEqual(2, options.Rivals);
        }

        [Test]
        public void ABrokenOptionKeepsItsDefault()
        {
            DebugBenchmarkOptions options = DebugBenchmarkOptions.From(new[]
            {
                "game.exe", DebugBenchmarkOptions.COMMAND_LINE_FLAG, DebugBenchmarkOptions.SECONDS_FLAG, "long",
                DebugBenchmarkOptions.RIVALS_FLAG, "-4", DebugBenchmarkOptions.MOOD_FLAG
            });

            Assert.AreEqual(DebugBenchmarkOptions.DEFAULT_SECONDS, options.Seconds, TOLERANCE);
            Assert.AreEqual(DebugBenchmarkOptions.DEFAULT_RIVALS, options.Rivals);
            Assert.AreEqual(DebugBenchmarkOptions.DEFAULT_MOOD, options.Mood);
        }

        [Test]
        public void WithNoMapsNamedEveryMatchMapIsMeasured()
        {
            DebugBenchmarkOptions options = DebugBenchmarkOptions.From(new[] { "game.exe", DebugBenchmarkOptions.COMMAND_LINE_FLAG });

            Assert.IsTrue(options.Wants(CONSTRUCTION));
            Assert.IsTrue(options.Wants(GOLF));
            Assert.IsFalse(options.Wants(TRAINING));
            Assert.IsFalse(options.Wants(NETWORKED));
        }

        [Test]
        public void NamedMapsAreTheOnlyOnesMeasured()
        {
            DebugBenchmarkOptions options = DebugBenchmarkOptions.From(new[]
                { "game.exe", DebugBenchmarkOptions.COMMAND_LINE_FLAG, DebugBenchmarkOptions.MAPS_FLAG, "golf,training" });

            Assert.IsFalse(options.Wants(CONSTRUCTION));
            Assert.IsTrue(options.Wants(GOLF));
            Assert.IsTrue(options.Wants(TRAINING));
            Assert.IsFalse(options.Wants(NETWORKED));
        }

        [Test]
        public void FrameStatsDescribeTheWholeRecording()
        {
            var frames = new float[100];
            for (int i = 0; i < frames.Length; i++) frames[i] = 10f;
            frames[3] = 30f;
            frames[50] = 110f;

            DebugFrameStats stats = DebugFrameStats.Of(frames);

            Assert.AreEqual(100, stats.Frames);
            Assert.AreEqual(11.2f, stats.Mean, TOLERANCE);
            Assert.AreEqual(10f, stats.Median, TOLERANCE);
            Assert.AreEqual(10f, stats.P95, TOLERANCE);
            Assert.AreEqual(110f, stats.P99, TOLERANCE);
            Assert.AreEqual(110f, stats.Worst, TOLERANCE);
            Assert.AreEqual(1000f / 11.2f, stats.Fps, TOLERANCE);
        }

        [Test]
        public void FrameStatsDoNotChangeTheRecording()
        {
            float[] frames = { 30f, 10f, 20f };

            DebugFrameStats.Of(frames);

            Assert.AreEqual(new[] { 30f, 10f, 20f }, frames);
        }

        [Test]
        public void AnEmptyRecordingHasNoSpeed()
        {
            DebugFrameStats stats = DebugFrameStats.Of(new float[0]);

            Assert.AreEqual(0, stats.Frames);
            Assert.AreEqual(0f, stats.Fps, TOLERANCE);
            Assert.AreEqual(0f, DebugFrameStats.Of(null).Worst, TOLERANCE);
        }

        [Test]
        public void TheReportIsNamedAfterThePipelineAndTheVersion()
        {
            string name = DebugBenchmark.ReportName("UniversalRenderPipelineAsset", "1.72.1", new System.DateTime(2026, 11, 22, 21, 5, 9));

            Assert.AreEqual("benchmark_UniversalRenderPipelineAsset_1.72.1_20261122_210509.txt", name);
        }

        [Test]
        public void AReportLineCarriesTheSceneAndItsNumbers()
        {
            DebugFrameStats stats = DebugFrameStats.Of(new[] { 10f, 10f, 10f, 10f });

            string line = DebugBenchmark.ReportLine("GolfMap", "MidDay", stats, "main=3.10");

            StringAssert.StartsWith("scene=GolfMap\tmood=MidDay\tfps=100.0\tframes=4\tmean=10.00\t", line);
            StringAssert.EndsWith("\tmain=3.10", line);
        }
        [Test]
        public void MatchesAreFiledByPipelineAndVersion()
        {
            Assert.AreEqual("matches_HDRenderPipelineAsset_1.72.1.txt", DebugMatchRecorder.FileName("HDRenderPipelineAsset", "1.72.1"));
        }

        [TestCase(true, true, 1f, true)]
        [TestCase(true, true, 0f, false)]
        [TestCase(true, false, 1f, false)]
        [TestCase(false, false, 1f, false)]
        public void OnlyAMatchThatIsRunningIsRecorded(bool hasLevel, bool started, float timeScale, bool expected)
        {
            Assert.AreEqual(expected, DebugMatchRecorder.ShouldRecord(hasLevel, started, timeScale));
        }

        [Test]
        public void AMatchOfAFewSecondsIsNotKept()
        {
            var shortMatch = new float[100];
            var realMatch = new float[600];
            for (int i = 0; i < shortMatch.Length; i++) shortMatch[i] = 10f;
            for (int i = 0; i < realMatch.Length; i++) realMatch[i] = 10f;

            Assert.IsFalse(DebugMatchRecorder.WorthKeeping(DebugFrameStats.Of(shortMatch)));
            Assert.IsTrue(DebugMatchRecorder.WorthKeeping(DebugFrameStats.Of(realMatch)));
            Assert.IsFalse(DebugMatchRecorder.WorthKeeping(default));
        }

        [Test]
        public void AMatchLineSaysHowLongItWasAndWhetherItHadTheFocus()
        {
            string extra = DebugMatchRecorder.Extra(new System.DateTime(2026, 11, 22, 21, 40, 5), 183.4f, 97.6f, 9.5, 2.25, 4.126, "Ultra", 1360, 768);

            Assert.AreEqual("seconds=183\tfocused=98%\tmain=9.50\trender=2.25\tgpu=4.13\tquality=Ultra\tscreen=1360x768\ttime=2026-11-22 21:40:05", extra);
        }
    }
}
