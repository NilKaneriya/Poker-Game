using UnityEngine;

namespace Poker.Core
{
    /// <summary>
    /// Server address and debug switches. The asset lives at Resources/ServerConfig
    /// so it can be changed per build without touching code or scenes.
    /// </summary>
    [CreateAssetMenu(menuName = "Poker/Server Config", fileName = "ServerConfig")]
    public class ServerConfig : ScriptableObject
    {
        [Tooltip("Base URL of poker-server, including protocol, e.g. http://localhost:3000. " +
                 "Leave empty in a WebGL build that poker-server hosts itself: the page's own address is used.")]
        public string serverUrl = "http://localhost:3000";

        [Tooltip("Web address of the WebGL build, used for invite links shared from non-WebGL builds " +
                 "(e.g. https://example.com/poker/). WebGL builds use their own page address. " +
                 "Leave empty to share the room code only.")]
        public string inviteBaseUrl = "";

        [Tooltip("Log every Socket.IO event to the console.")]
        public bool logNetworkEvents = true;

        private static ServerConfig loaded;

        public static ServerConfig Load()
        {
            if (loaded == null) loaded = Resources.Load<ServerConfig>("ServerConfig");
            if (loaded == null)
            {
                Debug.LogWarning("Resources/ServerConfig not found, using defaults.");
                loaded = CreateInstance<ServerConfig>();
            }
            return loaded;
        }

        /// <summary>Full URL for an HTTP path, e.g. Url("/auth/login").</summary>
        public string Url(string path) => ServerAddress.TrimEnd('/') + path;

        /// <summary>
        /// The server to talk to. Empty serverUrl in a WebGL build = the address the game was
        /// loaded from (game and server hosted together), so no https/http mismatch.
        /// </summary>
        public string ServerAddress
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(serverUrl) || Application.platform != RuntimePlatform.WebGLPlayer)
                    return serverUrl.Trim();
                var page = new System.Uri(Application.absoluteURL);
                return page.GetLeftPart(System.UriPartial.Authority);
            }
        }
    }
}
