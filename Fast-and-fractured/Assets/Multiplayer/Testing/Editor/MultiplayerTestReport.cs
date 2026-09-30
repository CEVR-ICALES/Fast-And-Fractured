using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Testing.Editor
{
    /// <summary>
    /// Runs the multiplayer EditMode tests and writes a plain-text summary to
    /// &lt;project&gt;/Logs/MultiplayerTests/last.txt (pass/fail counts, then every failure with its
    /// message), so a run can be read without the Test Runner window.
    /// </summary>
    public static class MultiplayerTestReport
    {
        private const string Namespace = "FastAndFractured.Multiplayer.Tests";

        public static string ReportPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "MultiplayerTests", "last.txt"));

        [MenuItem("Tools/Fast And Fractured/Tests/Run Multiplayer EditMode Tests")]
        public static void RunAll() => Run(new[] { Namespace });

        /// <summary>Runs the tests whose full names start with any of <paramref name="groups"/>.</summary>
        public static string Run(string[] groups)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, "running\n");
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks(api));
            var filter = new Filter { testMode = TestMode.EditMode, groupNames = ToGroupPatterns(groups) };
            api.Execute(new ExecutionSettings(filter));
            return "started; report: " + ReportPath;
        }

        private static string[] ToGroupPatterns(string[] groups)
        {
            var patterns = new string[groups.Length];
            for (int i = 0; i < groups.Length; i++)
                patterns[i] = "^" + System.Text.RegularExpressions.Regex.Escape(groups[i]);
            return patterns;
        }

        private sealed class Callbacks : ICallbacks
        {
            private readonly TestRunnerApi _api;
            public Callbacks(TestRunnerApi api) { _api = api; }

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"{result.ResultState}: {result.PassCount} passed, {result.FailCount} failed, " +
                              $"{result.SkipCount} skipped, {result.InconclusiveCount} inconclusive, {result.Duration:F1}s");
                AppendFailures(result, sb);
                File.WriteAllText(ReportPath, sb.ToString());
                _api.UnregisterCallbacks(this);
                Debug.Log("[MultiplayerTestReport] " + sb.ToString().Split('\n')[0]);
            }

            private static void AppendFailures(ITestResultAdaptor result, StringBuilder sb)
            {
                if (!result.HasChildren)
                {
                    if (result.TestStatus == TestStatus.Failed)
                        sb.AppendLine($"FAILED {result.Test.FullName}\n    {result.Message?.Trim().Replace("\n", "\n    ")}");
                    return;
                }
                foreach (var child in result.Children)
                    AppendFailures(child, sb);
            }
        }
    }
}
