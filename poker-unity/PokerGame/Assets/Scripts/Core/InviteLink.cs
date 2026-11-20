using System;
using UnityEngine;
using static Poker.Core.Constants;

namespace Poker.Core
{
    /// <summary>
    /// Invite links of the form  https://your-site/poker/?room=ABCDE.
    ///
    /// Opening one stores the room code as "pending"; the Lobby joins that table as soon
    /// as the player is signed in and connected (after the login screen if needed).
    /// Sources: the WebGL page address at startup, and Application.deepLinkActivated
    /// for builds set up to receive deep links (e.g. Android intent filters).
    /// </summary>
    public static class InviteLink
    {
        public const string QueryKey = "room";

        /// <summary>A link was opened while the game was already running.</summary>
        public static event Action<string> Received;

        private static string pending;

        public static bool HasPending => pending != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            pending = null;
            Application.deepLinkActivated -= OnDeepLink;
            Application.deepLinkActivated += OnDeepLink;
            if (!string.IsNullOrEmpty(Application.absoluteURL)) pending = ParseCode(Application.absoluteURL);
        }

        private static void OnDeepLink(string url)
        {
            string code = ParseCode(url);
            if (code == null) return;
            pending = code;
            Received?.Invoke(code);
        }

        /// <summary>Returns the pending room code (or null) and clears it.</summary>
        public static string TakePending()
        {
            string code = pending;
            pending = null;
            if (code != null) ShareUtil.RemoveUrlParam(QueryKey); // a page reload must not join again
            return code;
        }

        /// <summary>The room code in a link's query string, or null if there is no valid one.</summary>
        public static string ParseCode(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            int q = url.IndexOf('?');
            if (q < 0) return null;

            string query = url.Substring(q + 1);
            int hash = query.IndexOf('#');
            if (hash >= 0) query = query.Substring(0, hash);

            foreach (string part in query.Split('&'))
            {
                int eq = part.IndexOf('=');
                if (eq < 0 || !part.Substring(0, eq).Equals(QueryKey, StringComparison.OrdinalIgnoreCase)) continue;
                string code = Uri.UnescapeDataString(part.Substring(eq + 1)).Trim().ToUpperInvariant();
                return IsValidCode(code) ? code : null;
            }
            return null;
        }

        /// <summary>Link that opens the game and joins this table, or null if there is no web address to share.</summary>
        public static string Build(string code)
        {
            string baseUrl = Application.platform == RuntimePlatform.WebGLPlayer
                ? Application.absoluteURL
                : ServerConfig.Load().inviteBaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl)) return null;

            baseUrl = baseUrl.Trim();
            int cut = baseUrl.IndexOfAny(new[] { '?', '#' });
            if (cut >= 0) baseUrl = baseUrl.Substring(0, cut);
            return $"{baseUrl}?{QueryKey}={Uri.EscapeDataString(code)}";
        }

        private static bool IsValidCode(string code)
        {
            if (code.Length != GameRules.TableCodeLength) return false;
            foreach (char c in code)
            {
                if (!char.IsLetterOrDigit(c) || c > 127) return false;
            }
            return true;
        }
    }
}
