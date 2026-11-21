using Poker.Network;
using UnityEngine;

namespace Poker.Core
{
    /// <summary>
    /// Creates the long-lived services object (login + network) the first time any
    /// scene needs it. It survives scene loads, so every scene can be played
    /// directly from the Editor and the connection is never duplicated.
    /// </summary>
    public static class AppServices
    {
        public static void EnsureExists()
        {
            if (NetworkManager.Instance != null) return;

            var go = new GameObject("AppServices");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<AuthManager>();
            go.AddComponent<NetworkManager>(); // also adds SocketIOCommunicator (RequireComponent)
        }
    }
}
