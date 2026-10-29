using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace FastAndFractured.GameTelemetry
{
    /// <summary>
    /// GA4 through the Measurement Protocol (plain HTTPS, works on Windows): the dashboards,
    /// funnels and retention. The endpoint always answers 2xx, so "sent" means "delivered".
    /// </summary>
    internal sealed class AnalyticsSink
    {
        private const int TIMEOUT = 15;

        public static bool Configured => !string.IsNullOrEmpty(TelemetryConfig.GaApiSecret);

        public IEnumerator Send(IList<TelemetryEvent> events, string userId, long sessionId, string version, bool dev, Action<bool> done)
        {
            if (!Configured) { done(true); yield break; }   // nothing to send to yet
            string url = $"https://www.google-analytics.com/mp/collect?measurement_id={TelemetryConfig.GA_MEASUREMENT_ID}&api_secret={TelemetryConfig.GaApiSecret}";
            foreach (var body in TelemetryJson.Ga4Requests(TelemetryIdentity.ClientId, userId, sessionId, version, dev, events))
            {
                using var request = new UnityWebRequest(url, "POST")
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None))),
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = TIMEOUT,
                };
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) { done(false); yield break; }
            }
            done(true);
        }
    }
}
