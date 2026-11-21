using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine.Networking;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.Network
{
    /// <summary>
    /// Minimal JSON-over-HTTP helper for the login endpoints. Run with StartCoroutine.
    /// </summary>
    public static class ApiClient
    {

        public static IEnumerator Get<T>(string url, string token, Action<T> onSuccess, Action<ApiFailure> onFailure) =>
            Send("GET", url, null, token, onSuccess, onFailure);

        public static IEnumerator Post<T>(string url, object body, string token, Action<T> onSuccess, Action<ApiFailure> onFailure) =>
            Send("POST", url, body, token, onSuccess, onFailure);

        public static IEnumerator Put<T>(string url, object body, string token, Action<T> onSuccess, Action<ApiFailure> onFailure) =>
            Send("PUT", url, body, token, onSuccess, onFailure);

        private static IEnumerator Send<T>(string method, string url, object body, string token,
            Action<T> onSuccess, Action<ApiFailure> onFailure)
        {
            using var request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Timeouts.HttpSeconds;

            if (body != null)
            {
                byte[] json = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body));
                request.uploadHandler = new UploadHandlerRaw(json);
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(JsonConvert.DeserializeObject<T>(request.downloadHandler.text));
            }
            else
            {
                onFailure?.Invoke(new ApiFailure { Status = request.responseCode, Message = ReadError(request) });
            }
        }

        /// <summary>Prefer the server's { error } message; fall back to a generic one.</summary>
        private static string ReadError(UnityWebRequest request)
        {
            if (request.responseCode == 0) return "Can't reach the server. Check your connection.";
            try
            {
                var body = JsonConvert.DeserializeObject<ErrorBody>(request.downloadHandler.text);
                if (!string.IsNullOrEmpty(body?.error)) return body.error;
            }
            catch (JsonException) { /* not JSON, use the fallback below */ }
            return $"Server error ({request.responseCode})";
        }
    }
}
