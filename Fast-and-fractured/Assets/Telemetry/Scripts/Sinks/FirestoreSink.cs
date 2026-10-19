using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace FastAndFractured.GameTelemetry
{
    /// <summary>
    /// Firestore over REST (no Firebase SDK on Windows): one anonymous sign-in, refresh token in PlayerPrefs, and the
    /// documents the security rules allow.
    /// </summary>
    internal sealed class FirestoreSink
    {
        private const string REFRESH_TOKEN_KEY = "Telemetry_RefreshToken";
        private const int TIMEOUT = 15;
        private const int DEFAULT_TOKEN_SECONDS = 3600;
        private const int MIN_TOKEN_SECONDS = 60;
        private const int TOKEN_EXPIRY_MARGIN_SECONDS = 120;
        private const long HTTP_UNAUTHORIZED = 401;

        public static bool Configured => !string.IsNullOrEmpty(TelemetryConfig.FirebaseApiKey);

        private static string Documents =>
            $"https://firestore.googleapis.com/v1/projects/{TelemetryConfig.FIREBASE_PROJECT_ID}/databases/(default)/documents";

        private string _idToken, _uid;
        private DateTime _tokenExpires;
        private bool _signingIn;

        public IEnumerator SendEvents(IList<TelemetryEvent> events, string steamHash, string version, bool dev, Action<bool> done)
        {
            bool signedIn = false;
            yield return SignIn(ok => signedIn = ok);
            if (!signedIn) { done(false); yield break; }

            var body = TelemetryJson.FirestoreEventsDocument(_uid, steamHash, version, dev, events, DateTime.UtcNow);
            bool sent = false;
            yield return Request("POST", $"{Documents}/events", body, ok => sent = ok);
            done(sent);
        }

        /// <summary>players/{uid}: replaced as a whole with the current profile.</summary>
        public IEnumerator SendPlayer(IDictionary<string, object> fields, Action<bool> done)
        {
            bool signedIn = false;
            yield return SignIn(ok => signedIn = ok);
            if (!signedIn) { done(false); yield break; }

            var body = new JObject { ["fields"] = TelemetryJson.FirestoreFields(fields) };
            bool sent = false;
            yield return Request("PATCH", $"{Documents}/players/{_uid}", body, ok => sent = ok);
            done(sent);
        }

        private IEnumerator SignIn(Action<bool> done)
        {
            // One sign-in at a time: two at once would create two anonymous players.
            while (_signingIn) yield return null;
            if (_idToken != null && DateTime.UtcNow < _tokenExpires) { done(true); yield break; }
            _signingIn = true;
            try
            {
                bool ok = false;
                yield return SignInNow(result => ok = result);
                done(ok);
            }
            finally { _signingIn = false; }
        }

        private IEnumerator SignInNow(Action<bool> done)
        {

            string refresh = PlayerPrefs.GetString(REFRESH_TOKEN_KEY, string.Empty);
            JObject reply = null;
            if (!string.IsNullOrEmpty(refresh))
            {
                // securetoken answers in snake_case: id_token, refresh_token, user_id, expires_in.
                yield return Post($"https://securetoken.googleapis.com/v1/token?key={TelemetryConfig.FirebaseApiKey}",
                    new JObject { ["grant_type"] = "refresh_token", ["refresh_token"] = refresh }, r => reply = r);
                if (reply != null && Store((string)reply["id_token"], (string)reply["refresh_token"], (string)reply["user_id"], (string)reply["expires_in"]))
                { done(true); yield break; }
            }

            // First time (or the old account is gone): a new anonymous account.
            reply = null;
            yield return Post($"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={TelemetryConfig.FirebaseApiKey}",
                new JObject { ["returnSecureToken"] = true }, r => reply = r);
            done(reply != null && Store((string)reply["idToken"], (string)reply["refreshToken"], (string)reply["localId"], (string)reply["expiresIn"]));
        }

        private bool Store(string idToken, string refreshToken, string uid, string expiresIn)
        {
            if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(uid)) return false;
            _idToken = idToken;
            _uid = uid;
            int seconds = int.TryParse(expiresIn, out var s) ? s : DEFAULT_TOKEN_SECONDS;
            _tokenExpires = DateTime.UtcNow.AddSeconds(Mathf.Max(MIN_TOKEN_SECONDS, seconds - TOKEN_EXPIRY_MARGIN_SECONDS));
            if (!string.IsNullOrEmpty(refreshToken))
            {
                PlayerPrefs.SetString(REFRESH_TOKEN_KEY, refreshToken);
                PlayerPrefs.Save();
            }
            return true;
        }

        private static IEnumerator Post(string url, JObject body, Action<JObject> done)
        {
            using var request = Build("POST", url, body, null);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success) { done(null); yield break; }
            try { done(JObject.Parse(request.downloadHandler.text)); }
            catch { done(null); }
        }

        private IEnumerator Request(string method, string url, JObject body, Action<bool> done)
        {
            using var request = Build(method, url, body, _idToken);
            yield return request.SendWebRequest();
            bool ok = request.result == UnityWebRequest.Result.Success;
            if (!ok && request.responseCode == HTTP_UNAUTHORIZED) _idToken = null;   // expired early: sign in again next time
            if (!ok && Debug.isDebugBuild)
                Debug.LogWarning($"[Telemetry] Firestore {method} failed: {request.responseCode} {request.error}");
            done(ok);
        }

        private static UnityWebRequest Build(string method, string url, JObject body, string bearer)
        {
            var request = new UnityWebRequest(url, method)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None))),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TIMEOUT,
            };
            request.SetRequestHeader("Content-Type", "application/json");
            if (bearer != null) request.SetRequestHeader("Authorization", "Bearer " + bearer);
            return request;
        }
    }
}
