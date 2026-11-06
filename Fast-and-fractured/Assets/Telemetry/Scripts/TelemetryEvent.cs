using System;
using System.Collections.Generic;

namespace FastAndFractured.GameTelemetry
{
    /// <summary>One thing that happened: its key, when (Unix ms) and its data.</summary>
    [Serializable]
    public sealed class TelemetryEvent
    {
        public string key;
        public long time;
        public Dictionary<string, object> data = new Dictionary<string, object>();
    }

    /// <summary>Events sent together; each destination is retried until it took them.</summary>
    [Serializable]
    internal sealed class TelemetryBatch
    {
        public List<TelemetryEvent> events = new List<TelemetryEvent>();
        public bool sentToFirestore;
        public bool sentToAnalytics;
        public bool Done => sentToFirestore && sentToAnalytics;
    }
}
