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
        [Tooltip("Base URL of poker-server, including protocol. e.g. http://localhost:3000")]
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
        public string Url(string path) => serverUrl.TrimEnd('/') + path;
    }
}
