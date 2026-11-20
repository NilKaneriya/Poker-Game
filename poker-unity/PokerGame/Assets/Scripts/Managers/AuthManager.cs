using System;
using Poker.Core;
using UnityEngine;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.Network
{
    /// <summary>
    /// Holds the logged-in player and their login token.
    /// With "Remember me" the token is saved in PlayerPrefs, so the Splash scene
    /// can log in automatically next time. Passwords are never stored.
    /// </summary>
    public class AuthManager : MonoBehaviour
    {

        public static AuthManager Instance { get; private set; }

        public string Token { get; private set; }
        public string PlayerId { get; private set; }
        public string Username { get; private set; }

        public bool IsLoggedIn => !string.IsNullOrEmpty(Token);
        public bool HasRememberedLogin => PlayerPrefs.HasKey(PrefsKeys.AuthToken);

        private ServerConfig config;

        private void Awake()
        {
            Instance = this;
            config = ServerConfig.Load();
        }

        public void Login(string username, string password, bool remember, Action onSuccess, Action<ApiFailure> onFailure) =>
            Authenticate(ApiRoutes.Login, username, password, remember, onSuccess, onFailure);

        public void Register(string username, string password, bool remember, Action onSuccess, Action<ApiFailure> onFailure) =>
            Authenticate(ApiRoutes.Register, username, password, remember, onSuccess, onFailure);

        private void Authenticate(string path, string username, string password, bool remember,
            Action onSuccess, Action<ApiFailure> onFailure)
        {
            var body = new AuthRequest { username = username, password = password, remember = remember };
            StartCoroutine(ApiClient.Post<AuthResponse>(config.Url(path), body, null, response =>
            {
                SetSession(response.token, response.playerId, response.username);
                if (remember) SaveToken(response.token);
                else ForgetSavedToken();
                onSuccess?.Invoke();
            }, onFailure));
        }

        /// <summary>Auto-login: check the saved token with the server.</summary>
        public void ResumeRememberedLogin(Action onSuccess, Action<ApiFailure> onFailure)
        {
            string saved = PlayerPrefs.GetString(PrefsKeys.AuthToken, "");
            StartCoroutine(ApiClient.Get<UserInfo>(config.Url(ApiRoutes.Me), saved, user =>
            {
                SetSession(saved, user.playerId, user.username);
                onSuccess?.Invoke();
            }, failure =>
            {
                // Expired or revoked token: forget it. Network errors keep it for the next try.
                if (!failure.IsNetworkError) ForgetSavedToken();
                onFailure?.Invoke(failure);
            }));
        }

        public void Logout()
        {
            if (IsLoggedIn)
            {
                // Tell the server to revoke the token; we don't need to wait for the answer.
                StartCoroutine(ApiClient.Post<object>(config.Url(ApiRoutes.Logout), null, Token, null, null));
            }
            ClearSession();
        }

        /// <summary>Forget the current login (e.g. the server rejected our token).</summary>
        public void ClearSession()
        {
            Token = PlayerId = Username = null;
            ForgetSavedToken();
        }

        private void SetSession(string token, string playerId, string username)
        {
            Token = token;
            PlayerId = playerId;
            Username = username;
        }

        private static void SaveToken(string token)
        {
            PlayerPrefs.SetString(PrefsKeys.AuthToken, token);
            PlayerPrefs.Save();
        }

        private static void ForgetSavedToken()
        {
            PlayerPrefs.DeleteKey(PrefsKeys.AuthToken);
            PlayerPrefs.Save();
        }
    }
}
