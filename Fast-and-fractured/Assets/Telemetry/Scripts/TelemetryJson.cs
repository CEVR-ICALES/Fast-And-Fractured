using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace FastAndFractured.GameTelemetry
{
    /// <summary>The bodies sent to Firestore (REST) and GA4 (Measurement Protocol). Pure, unit tested.</summary>
    public static class TelemetryJson
    {
        public const int GA_EVENTS_PER_REQUEST = 25;
        private const int GA_MAX_PARAMS = 25;
        private const int GA_MAX_NAME = 40;
        private const int GA_MAX_STRING = 100;

        // ── Firestore ────────────────────────────────────────────────────────────────
        /// <summary>A value in Firestore's typed JSON ({"stringValue": ...}, {"mapValue": ...}...).</summary>
        public static JObject FirestoreValue(object value)
        {
            switch (value)
            {
                case null: return new JObject { ["nullValue"] = null };
                case string s: return new JObject { ["stringValue"] = s };
                case bool b: return new JObject { ["booleanValue"] = b };
                case int or long or short or byte or uint:
                    return new JObject { ["integerValue"] = Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture) };
                case float or double or decimal:
                    double d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    return double.IsNaN(d) || double.IsInfinity(d) ? new JObject { ["nullValue"] = null } : new JObject { ["doubleValue"] = d };
                case DateTime time: return new JObject { ["timestampValue"] = time.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) };
                case IDictionary<string, object> map: return new JObject { ["mapValue"] = new JObject { ["fields"] = FirestoreFields(map) } };
                case IEnumerable list:
                    var values = new JArray();
                    foreach (var item in list) values.Add(FirestoreValue(item));
                    return new JObject { ["arrayValue"] = new JObject { ["values"] = values } };
                default: return new JObject { ["stringValue"] = value.ToString() };
            }
        }

        public static JObject FirestoreFields(IDictionary<string, object> map)
        {
            var fields = new JObject();
            if (map != null)
                foreach (var pair in map) fields[pair.Key] = FirestoreValue(pair.Value);
            return fields;
        }

        /// <summary>The events/{id} document (fields allowed by the security rules).</summary>
        public static JObject FirestoreEventsDocument(string uid, string steamHash, string version, bool dev, IList<TelemetryEvent> events, DateTime sentAt)
        {
            var list = new List<object>();
            foreach (var e in events)
                list.Add(new Dictionary<string, object>
                {
                    ["key"] = e.key,
                    ["time"] = DateTimeOffset.FromUnixTimeMilliseconds(e.time).UtcDateTime,
                    ["data"] = e.data ?? new Dictionary<string, object>(),
                });
            return new JObject
            {
                ["fields"] = FirestoreFields(new Dictionary<string, object>
                {
                    ["uid"] = uid,
                    ["steamHash"] = steamHash,
                    ["version"] = version,
                    ["dev"] = dev,
                    ["sentAt"] = sentAt,
                    ["events"] = list,
                }),
            };
        }

        // ── GA4 Measurement Protocol ─────────────────────────────────────────────────
        /// <summary>
        /// Request bodies for /mp/collect, at most 25 events each. GA4 only takes flat string or
        /// number parameters: booleans become 0/1, maps and lists are left out.
        /// </summary>
        public static List<JObject> Ga4Requests(string clientId, string userId, long sessionId, string version, bool dev, IList<TelemetryEvent> events)
        {
            var requests = new List<JObject>();
            for (int start = 0; start < events.Count; start += GA_EVENTS_PER_REQUEST)
            {
                var list = new JArray();
                for (int i = start; i < Math.Min(events.Count, start + GA_EVENTS_PER_REQUEST); i++)
                    list.Add(Ga4Event(events[i], sessionId, dev));
                var body = new JObject
                {
                    ["client_id"] = clientId,
                    ["user_properties"] = new JObject
                    {
                        ["game_version"] = new JObject { ["value"] = version },
                        ["dev_build"] = new JObject { ["value"] = dev ? "yes" : "no" },
                    },
                    ["events"] = list,
                };
                if (!string.IsNullOrEmpty(userId)) body["user_id"] = userId;
                requests.Add(body);
            }
            return requests;
        }

        private static JObject Ga4Event(TelemetryEvent e, long sessionId, bool dev)
        {
            var parameters = new JObject
            {
                ["session_id"] = sessionId,
                ["engagement_time_msec"] = 100,   // without it GA4 does not count the user as active
            };
            if (dev) parameters["debug_mode"] = 1;   // shows up in GA4's DebugView
            if (e.data != null)
                foreach (var pair in e.data)
                {
                    if (parameters.Count >= GA_MAX_PARAMS) break;
                    var value = Ga4Value(pair.Value);
                    if (value != null) parameters[Clip(pair.Key, GA_MAX_NAME)] = value;
                }
            return new JObject
            {
                ["name"] = Clip(e.key, GA_MAX_NAME),
                ["params"] = parameters,
                ["timestamp_micros"] = e.time * 1000L,
            };
        }

        private static JToken Ga4Value(object value) => value switch
        {
            null => null,
            string s => Clip(s, GA_MAX_STRING),
            bool b => b ? 1 : 0,
            int or long or short or byte or uint => Convert.ToInt64(value),
            float or double or decimal => Math.Round(Convert.ToDouble(value, CultureInfo.InvariantCulture), 3),
            IDictionary or IEnumerable => null,
            _ => Clip(value.ToString(), GA_MAX_STRING),
        };

        private static string Clip(string text, int max) => text == null || text.Length <= max ? text : text.Substring(0, max);
    }
}
