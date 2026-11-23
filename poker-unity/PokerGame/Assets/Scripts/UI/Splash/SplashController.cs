using System.Collections;
using DG.Tweening;
using Poker.Core;
using Poker.Network;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Splash
{
    /// <summary>
    /// First scene. Steps:
    ///   1. Check the server is reachable.
    ///   2. If a "Remember me" token is saved, verify it and skip the login form.
    ///   3. Otherwise show login / create account.
    ///   4. Open the real-time connection, then go to the Lobby.
    /// </summary>
    public class SplashController : MonoBehaviour
    {
        [Header("Loading")]
        [SerializeField] private CanvasGroup loadingGroup;
        [SerializeField] private RectTransform progressFill; // width driven by anchorMax.x
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button retryButton;
        [SerializeField] private RectTransform logo;

        [Header("Login form")]
        [SerializeField] private CanvasGroup loginGroup;
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Toggle rememberToggle;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button registerButton;
        [SerializeField] private TMP_Text loginErrorText;


        private AuthManager Auth => AuthManager.Instance;
        private NetworkManager Net => NetworkManager.Instance;
        private ServerConfig config;
        private bool sessionStarted;

        private void Awake()
        {
            AppServices.EnsureExists();
            config = ServerConfig.Load();
        }

        private void Start()
        {
            retryButton.onClick.AddListener(() => StartCoroutine(Boot()));
            loginButton.onClick.AddListener(() => SubmitLogin(register: false));
            registerButton.onClick.AddListener(() => SubmitLogin(register: true));
            passwordInput.onSubmit.AddListener(_ => SubmitLogin(register: false));

            Net.SessionStarted += OnSessionStarted;
            Net.AuthRejected += OnAuthRejected;

            if (logo != null) logo.DOAnchorPosY(logo.anchoredPosition.y + 12f, 1.4f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);

            StartCoroutine(Boot());
        }

        private void OnDestroy()
        {
            // NetworkManager outlives this scene, so always unsubscribe.
            if (Net == null) return;
            Net.SessionStarted -= OnSessionStarted;
            Net.AuthRejected -= OnAuthRejected;
            if (logo != null) logo.DOKill();
        }

        // ---------- Flow ----------

        private IEnumerator Boot()
        {
            ShowLoading();
            retryButton.gameObject.SetActive(false);

            SetProgress(0.15f, "Connecting to server...");
            bool serverUp = false;
            yield return ApiClient.Get<object>(config.Url("/health"), null, _ => serverUp = true, _ => { });
            if (!serverUp)
            {
                SetProgress(0.15f, "Can't reach the server. Check your connection and try again.");
                retryButton.gameObject.SetActive(true);
                yield break;
            }

            if (Auth.IsLoggedIn)
            {
                // Already logged in this session (e.g. came back here from another scene).
                yield return EnterLobby();
                yield break;
            }

            if (Auth.HasRememberedLogin)
            {
                SetProgress(0.45f, "Signing you in...");
                bool? resumed = null;
                Auth.ResumeRememberedLogin(() => resumed = true, _ => resumed = false);
                yield return new WaitUntil(() => resumed.HasValue);
                if (resumed == true)
                {
                    yield return EnterLobby();
                    yield break;
                }
            }

            ShowLoginForm();
        }

        private void SubmitLogin(bool register)
        {
            string username = usernameInput.text.Trim();
            string password = passwordInput.text;
            if (username.Length == 0 || password.Length == 0)
            {
                ShowLoginError("Enter a username and password.");
                return;
            }

            SetFormInteractable(false);
            loginErrorText.text = "";

            void OnSuccess()
            {
                passwordInput.text = ""; // don't keep the password around
                StartCoroutine(EnterLobby());
            }

            void OnFailure(ApiFailure failure)
            {
                SetFormInteractable(true);
                ShowLoginError(failure.Message);
            }

            if (register) Auth.Register(username, password, rememberToggle.isOn, OnSuccess, OnFailure);
            else Auth.Login(username, password, rememberToggle.isOn, OnSuccess, OnFailure);
        }

        /// <summary>Open the socket and wait for the server to accept our token.</summary>
        private IEnumerator EnterLobby()
        {
            ShowLoading();
            SetProgress(0.75f, $"Welcome, {Auth.Username}!");

            sessionStarted = false;
            if (Net.IsConnected) sessionStarted = true;
            else Net.Connect();

            float waited = 0f;
            while (!sessionStarted && waited < Timeouts.SocketConnectSeconds)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (!sessionStarted)
            {
                SetProgress(0.75f, "The server didn't answer. Try again.");
                retryButton.gameObject.SetActive(true);
                yield break;
            }

            SetProgress(1f, "Let's play!");
            yield return new WaitForSeconds(0.35f); // let the bar finish filling
            SceneManager.LoadScene(SceneNames.Lobby);
        }

        private void OnSessionStarted(SessionInfo _) => sessionStarted = true;

        private void OnAuthRejected()
        {
            Auth.ClearSession();
            ShowLoginForm();
            ShowLoginError(SceneNotice.Take() ?? "Your session expired. Please sign in again.");
        }

        // ---------- View helpers ----------

        private void ShowLoading()
        {
            UIAnim.FadeOut(loginGroup);
            UIAnim.FadeIn(loadingGroup);
        }

        private void ShowLoginForm()
        {
            UIAnim.FadeOut(loadingGroup);
            UIAnim.FadeIn(loginGroup);
            UIAnim.PopIn(loginGroup.transform, 0.35f);
            SetFormInteractable(true);
            loginErrorText.text = "";
        }

        private void ShowLoginError(string message)
        {
            loginErrorText.text = message;
            UIAnim.Punch(loginErrorText.transform, 0.1f);
        }

        private void SetFormInteractable(bool value)
        {
            loginButton.interactable = registerButton.interactable = value;
            usernameInput.interactable = passwordInput.interactable = value;
        }

        private void SetProgress(float value, string status)
        {
            statusText.text = status;
            progressFill.DOKill();
            var target = new Vector2(Mathf.Clamp01(value), 1f);
            if (Application.isPlaying) progressFill.DOAnchorMax(target, 0.4f).SetEase(Ease.OutCubic);
            else progressFill.anchorMax = target;
        }
    }
}
