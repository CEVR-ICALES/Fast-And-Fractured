using System;
using System.Collections.Generic;
using System.Linq;
using FastAndFractured.GameTelemetry;
using NUnit.Framework;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>Telemetry: what is sent to Firestore and GA4, the player id, map and character names.</summary>
    public class TelemetryTests
    {
        private static TelemetryEvent Event(string key, Dictionary<string, object> data, long time = 1_700_000_000_000) =>
            new TelemetryEvent { key = key, time = time, data = data };

        [Test]
        public void Firestore_TypesEachValue()
        {
            Assert.AreEqual("hola", (string)TelemetryJson.FirestoreValue("hola")["stringValue"]);
            Assert.AreEqual(true, (bool)TelemetryJson.FirestoreValue(true)["booleanValue"]);
            Assert.AreEqual("42", (string)TelemetryJson.FirestoreValue(42)["integerValue"]);
            Assert.AreEqual(1.5, (double)TelemetryJson.FirestoreValue(1.5f)["doubleValue"]);
            Assert.IsTrue(TelemetryJson.FirestoreValue(null).ContainsKey("nullValue"));
            Assert.IsTrue(TelemetryJson.FirestoreValue(float.NaN).ContainsKey("nullValue"));
            var list = TelemetryJson.FirestoreValue(new List<string> { "a", "b" });
            Assert.AreEqual(2, list["arrayValue"]["values"].Count());
            var map = TelemetryJson.FirestoreValue(new Dictionary<string, object> { ["level"] = 3 });
            Assert.AreEqual("3", (string)map["mapValue"]["fields"]["level"]["integerValue"]);
        }

        [Test]
        public void Firestore_EventsDocument_HasOnlyTheFieldsTheRulesAllow()
        {
            var doc = TelemetryJson.FirestoreEventsDocument("uid1", "hash", "1.69", true,
                new[] { Event("match_end", new Dictionary<string, object> { ["won"] = true }) }, DateTime.UtcNow);
            var fields = (Newtonsoft.Json.Linq.JObject)doc["fields"];
            CollectionAssert.AreEquivalent(new[] { "uid", "steamHash", "version", "dev", "sentAt", "events" },
                fields.Properties().Select(p => p.Name).ToList());
            Assert.AreEqual("uid1", (string)fields["uid"]["stringValue"]);
            var first = fields["events"]["arrayValue"]["values"][0]["mapValue"]["fields"];
            Assert.AreEqual("match_end", (string)first["key"]["stringValue"]);
            Assert.AreEqual(true, (bool)first["data"]["mapValue"]["fields"]["won"]["booleanValue"]);
        }

        [Test]
        public void Ga4_SplitsInRequestsOf25_AndFlattensParams()
        {
            var events = new List<TelemetryEvent>();
            for (int i = 0; i < 30; i++)
                events.Add(Event("match_end", new Dictionary<string, object>
                {
                    ["won"] = true, ["kills"] = 2, ["map"] = "Golf", ["nested"] = new Dictionary<string, object> { ["x"] = 1 },
                }));
            var requests = TelemetryJson.Ga4Requests("client", "steamhash", 123, "1.69", false, events);
            Assert.AreEqual(2, requests.Count);
            Assert.AreEqual(25, requests[0]["events"].Count());
            Assert.AreEqual(5, requests[1]["events"].Count());
            Assert.AreEqual("steamhash", (string)requests[0]["user_id"]);
            var p = requests[0]["events"][0]["params"];
            Assert.AreEqual(1, (int)p["won"]);
            Assert.AreEqual(2, (int)p["kills"]);
            Assert.AreEqual("Golf", (string)p["map"]);
            Assert.IsNull(p["nested"]);
            Assert.AreEqual(123, (long)p["session_id"]);
            Assert.IsNull(p["debug_mode"]);
            Assert.AreEqual(1_700_000_000_000_000L, (long)requests[0]["events"][0]["timestamp_micros"]);
        }

        [Test]
        public void Ga4_DevBuild_IsMarkedForDebugView()
        {
            var requests = TelemetryJson.Ga4Requests("client", null, 1, "1.69", true, new[] { Event("session_start", null) });
            Assert.AreEqual(1, (int)requests[0]["events"][0]["params"]["debug_mode"]);
            Assert.IsNull(requests[0]["user_id"]);
        }

        [Test]
        public void SteamHash_IsStableAndNotTheId()
        {
            string a = TelemetryIdentity.HashSteamId(76561198000000000UL);
            Assert.AreEqual(a, TelemetryIdentity.HashSteamId(76561198000000000UL));
            Assert.AreNotEqual(a, TelemetryIdentity.HashSteamId(76561198000000001UL));
            Assert.AreEqual(32, a.Length);
            StringAssert.DoesNotContain("76561198000000000", a);
        }

        [TestCase("NetworkedGolfMap", "Golf")]
        [TestCase("GolfMap", "Golf")]
        [TestCase("NetworkedMountainVillageMap", "MountainVillage")]
        [TestCase("Release", "Release")]
        public void MapName(string scene, string map) => Assert.AreEqual(map, MatchCollector.MapName(scene));

        [Test]
        public void CharacterCode_Splits()
        {
            Assert.AreEqual(("MariaAntonia", "2"), MatchCollector.SplitCode("MariaAntonia_2"));
            Assert.AreEqual(("unknown", ""), MatchCollector.SplitCode(""));
        }
    }
}
