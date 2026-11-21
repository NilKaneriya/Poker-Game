using System.IO;
using Poker.Core;
using Poker.EditorTools.Audio;
using Poker.UI;
using Poker.UI.Game;
using Poker.UI.Lobby;
using Poker.UI.Splash;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Poker.EditorTools.UIFactory;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.EditorTools
{
    /// <summary>
    /// Tools > Poker > Build Scenes: (re)creates the Splash, Lobby and Game scenes,
    /// the table-list row prefab and the ServerConfig asset, and adds the scenes to
    /// Build Settings. Run it once, then edit the scenes by hand as usual.
    /// Running it again overwrites manual changes to those three scenes.
    /// </summary>
    public static class PokerSceneBuilder
    {
        private const string ScenesFolder = "Assets/Scene";
        private const string PrefabsFolder = "Assets/Prefabs";
        private const string ConfigPath = "Assets/Resources/ServerConfig.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string TmpFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string TmpEssentialsPackage = "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage";

        [MenuItem("Tools/Poker/Build Scenes")]
        public static void BuildFromMenu()
        {
            bool ok = EditorUtility.DisplayDialog("Build poker scenes",
                "This creates (or overwrites) Splash, Lobby and Game in Assets/Scene.", "Build", "Cancel");
            if (!ok) return;

            if (!HasTmpEssentials())
            {
                // TextMeshPro needs its default font/settings first. The import finishes
                // after this method returns, so continue from the "completed" callback.
                AssetDatabase.importPackageCompleted += OnTmpImported;
                AssetDatabase.ImportPackage(Path.GetFullPath(TmpEssentialsPackage), false);
                return;
            }
            BuildAll();
        }

        /// <summary>
        /// Command line entry point. If TMP Essential Resources are not in the project yet,
        /// import them in a separate run first (Unity applies -importPackage after -executeMethod):
        ///   1) Unity -batchmode -projectPath . -importPackage "&lt;TMP Essential Resources.unitypackage&gt;" -quit
        ///   2) Unity -batchmode -projectPath . -executeMethod Poker.EditorTools.PokerSceneBuilder.BuildFromCommandLine -quit
        /// </summary>
        public static void BuildFromCommandLine()
        {
            if (!HasTmpEssentials())
            {
                Debug.LogError("TMP Essential Resources are missing. Run with -importPackage (see comment above).");
                return;
            }
            BuildAll();
        }

        private static void OnTmpImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImported;
            EditorApplication.delayCall += BuildAll;
        }

        private static bool HasTmpEssentials() => AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null;

        private static void BuildAll()
        {
            UIFactory.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
            if (UIFactory.Font == null)
            {
                Debug.LogError("TextMeshPro default font not found. Import TMP Essential Resources and run again.");
                return;
            }

            Directory.CreateDirectory(ScenesFolder);
            Directory.CreateDirectory(PrefabsFolder);
            UISprites.Generate();
            CreateServerConfig();
            PlaceholderSoundGenerator.Generate(); // fills Resources/SoundLibrary (keeps clips you assigned)

            var rowPrefab = BuildTableListItemPrefab();
            string splash = BuildSplash();
            string lobby = BuildLobby(rowPrefab);
            string game = BuildGame();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(splash, true),
                new EditorBuildSettingsScene(lobby, true),
                new EditorBuildSettingsScene(game, true),
            };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(splash);
            Debug.Log("Poker scenes built: Splash, Lobby, Game (added to Build Settings).");
        }

        private static void CreateServerConfig()
        {
            if (AssetDatabase.LoadAssetAtPath<ServerConfig>(ConfigPath) != null) return; // keep the user's URL
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ServerConfig>(), ConfigPath);
        }

        // =====================================================================
        // Shared scene parts
        // =====================================================================

        private static Transform NewScene(out Scene scene)
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Theme.Background;
            camera.orthographic = true;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))
            {
                layer = LayerMask.NameToLayer("UI"),
            };
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Transform canvas = canvasGo.transform;
            Img(canvas, "Background", null, Theme.Background).rectTransform.Fill();
            var light = Img(canvas, "Spotlight", UISprites.Glow, Theme.Blue.WithAlpha(0.18f));
            light.rectTransform.At(0, 60, 2200, 1500);
            return canvas;
        }

        private static string Save(Scene scene, string name)
        {
            string path = $"{ScenesFolder}/{name}.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        private static T AddController<T>(string name) where T : MonoBehaviour =>
            new GameObject(name).AddComponent<T>();

        /// <summary>The scene's music track (crossfades from the previous scene).</summary>
        private static void AddSceneMusic(SoundId track)
        {
            var music = AddController<SceneMusic>("SceneMusic");
            WireEnum(music, "track", track);
        }

        /// <summary>"Music" and "Sound" on/off buttons, right-aligned at (x, y) from the anchor.</summary>
        private static void BuildAudioToggles(Transform parent, Vector2 anchor, float x, float y)
        {
            var settings = new[] { SettingToggleButton.Setting.Sound, SettingToggleButton.Setting.Music };
            for (int i = 0; i < settings.Length; i++)
                BuildSettingToggle(parent, settings[i], anchor, x - i * 112, y, 104, 48);
        }

        private static Button BuildSettingToggle(Transform parent, SettingToggleButton.Setting setting, Vector2 anchor,
            float x, float y, float w, float h)
        {
            var button = MakeButton(parent, setting + "Toggle", "", Theme.Dark.WithAlpha(0.85f), Theme.Gold, 20, 16f);
            button.GetComponent<RectTransform>().Pin(anchor, x, y, w, h);
            var toggle = button.gameObject.AddComponent<SettingToggleButton>();
            WireEnum(toggle, "setting", setting);
            Wire(toggle, "label", LabelOf(button));
            Wire(toggle, "group", Group(button));
            return button;
        }

        /// <summary>"Time for a break?" overlay after a long session (responsible play).</summary>
        private static void BuildBreakReminder(Transform canvas)
        {
            var root = Node("BreakReminder", canvas).Fill();
            var group = Group(root, 0f);
            Img(root, "Dim", null, Color.black.WithAlpha(0.6f), raycast: true).rectTransform.Fill();
            var card = Panel(root, "Card", Theme.Panel, 30f);
            card.rectTransform.At(0, 0, 760, 380);
            Panel(card.transform, "HeaderBar", Theme.Green, 7f).rectTransform.Pin(new Vector2(0.5f, 1f), 0, -16, 260, 12);
            MakeText(card.transform, "Title", WellbeingTexts.Title, 44, Theme.Text, bold: true).rectTransform.At(0, 110, 680, 60);
            var body = MakeText(card.transform, "Body", WellbeingTexts.Body, 26, Theme.Muted);
            body.rectTransform.At(0, 20, 660, 110);
            var keep = MakeButton(card.transform, "KeepPlayingButton", WellbeingTexts.KeepPlaying, Theme.Green, Theme.Dark, 26);
            keep.GetComponent<RectTransform>().At(-150, -115, 260, 68);
            var off = MakeButton(card.transform, "TurnOffButton", WellbeingTexts.TurnOff, Theme.PanelLight, Theme.Text, 24);
            off.GetComponent<RectTransform>().At(150, -115, 260, 68);

            var panel = root.gameObject.AddComponent<BreakReminderPanel>();
            Wire(panel, "group", group);
            Wire(panel, "card", card.rectTransform);
            Wire(panel, "bodyText", body);
            Wire(panel, "keepPlayingButton", keep);
            Wire(panel, "turnOffButton", off);
            root.gameObject.SetActive(false);
        }

        /// <summary>Hidden message pill; anchor (0.5,1) = top centre, (0.5,0) = bottom centre.</summary>
        private static ToastView BuildToast(Transform canvas, Vector2 anchor, float y)
        {
            var bg = Panel(canvas, "Toast", Theme.Dark.WithAlpha(0.95f), 30f);
            bg.rectTransform.Pin(anchor, 0, y, 640, 64);
            Group(bg, 0f); // invisible until ToastView.Show
            var text = MakeText(bg.transform, "Message", "Message", 24, Theme.Text);
            text.rectTransform.Fill(24, 24, 0, 0);
            var toast = bg.gameObject.AddComponent<ToastView>();
            Wire(toast, "messageText", text);
            return toast;
        }

        private static ConnectionStatusView BuildConnectionStatus(Transform parent, Vector2 anchor, float x, float y, GameObject lostBanner)
        {
            var root = Panel(parent, "Connection", Theme.Dark.WithAlpha(0.7f), 22f);
            root.rectTransform.Pin(anchor, x, y, 190, 44);
            var dot = Img(root.transform, "Dot", UISprites.Circle, Theme.Green);
            dot.rectTransform.Pin(new Vector2(0f, 0.5f), 18, 0, 14, 14);
            var label = MakeText(root.transform, "Label", "Online", 20, Theme.Text, TextAlignmentOptions.Left);
            label.rectTransform.Fill(44, 10, 0, 0);

            var view = root.gameObject.AddComponent<ConnectionStatusView>();
            Wire(view, "dot", dot);
            Wire(view, "label", label);
            if (lostBanner != null) Wire(view, "lostBanner", lostBanner);
            return view;
        }

        private static GameObject BuildLostBanner(Transform canvas, float y, string message)
        {
            var banner = Img(canvas, "ConnectionLostBanner", null, Theme.Red.WithAlpha(0.95f));
            banner.rectTransform.anchorMin = new Vector2(0f, 1f);
            banner.rectTransform.anchorMax = new Vector2(1f, 1f);
            banner.rectTransform.pivot = new Vector2(0.5f, 1f);
            banner.rectTransform.anchoredPosition = new Vector2(0, y);
            banner.rectTransform.sizeDelta = new Vector2(0, 56);
            MakeText(banner.transform, "Message", message, 24, Color.white, bold: true).rectTransform.Fill();
            banner.gameObject.SetActive(false);
            return banner.gameObject;
        }

        /// <summary>A playing card (face, back, highlight) with its CardView.</summary>
        private static CardView BuildCard(Transform parent, string name, float w, float h)
        {
            var root = Node(name, parent).At(0, 0, w, h);
            var group = Group(root);

            var highlight = Panel(root, "Highlight", Theme.Gold, 14f);
            highlight.rectTransform.Fill(-6, -6, -6, -6);
            highlight.gameObject.SetActive(false);

            var shadow = Panel(root, "Shadow", Color.black.WithAlpha(0.35f), 12f);
            shadow.rectTransform.Fill(0, -3, 3, -6);

            // Back: blue with an inner frame and a suit emblem.
            var back = Panel(root, "Back", Theme.CardBack, 12f);
            back.rectTransform.Fill();
            Panel(back.transform, "Frame", Color.white.WithAlpha(0.18f), 8f).rectTransform.Fill(6, 6, 6, 6);
            Panel(back.transform, "Inner", Theme.CardBack, 6f).rectTransform.Fill(9, 9, 9, 9);
            MakeText(back.transform, "Emblem", "♠", h * 0.38f, Theme.Gold.WithAlpha(0.8f)).rectTransform.Fill();

            // Face: white with rank + small suit in the corner and a big suit in the middle.
            var face = Panel(root, "Face", Color.white, 12f);
            face.rectTransform.Fill();
            var rank = MakeText(face.transform, "Rank", "A", h * 0.24f, Theme.Dark, TextAlignmentOptions.TopLeft, bold: true);
            rank.rectTransform.Fill(w * 0.09f, 0, h * 0.04f, 0);
            rank.enableWordWrapping = false;
            var corner = MakeText(face.transform, "CornerSuit", "♠", h * 0.17f, Theme.Dark, TextAlignmentOptions.TopLeft);
            corner.rectTransform.Fill(w * 0.1f, 0, h * 0.27f, 0);
            var center = MakeText(face.transform, "CenterSuit", "♠", h * 0.44f, Theme.Dark);
            center.rectTransform.Fill(0, 0, h * 0.22f, 0);
            face.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<CardView>();
            Wire(view, "face", face.gameObject);
            Wire(view, "back", back.gameObject);
            Wire(view, "rankText", rank);
            Wire(view, "cornerSuitText", corner);
            Wire(view, "centerSuitText", center);
            Wire(view, "highlight", highlight.gameObject);
            Wire(view, "canvasGroup", group);
            root.gameObject.SetActive(false); // empty until dealt
            return view;
        }

        private static TextMeshProUGUI SuitRow(Transform parent, float y, float size)
        {
            string red = ColorUtility.ToHtmlStringRGB(Theme.Red), white = ColorUtility.ToHtmlStringRGB(Theme.Text);
            var row = MakeText(parent, "Suits",
                $"<color=#{white}>♠</color>  <color=#{red}>♥</color>  <color=#{white}>♣</color>  <color=#{red}>♦</color>",
                size, Theme.Text);
            row.rectTransform.At(0, y, 600, size * 1.3f);
            return row;
        }

        // =====================================================================
        // Splash
        // =====================================================================

        private static string BuildSplash()
        {
            Transform canvas = NewScene(out Scene scene);

            // Big faded suits in the background for a bit of personality.
            string[] suits = { "♠", "♥", "♣", "♦" };
            Vector2[] spots = { new Vector2(-760, 300), new Vector2(760, 320), new Vector2(-700, -330), new Vector2(720, -300) };
            for (int i = 0; i < suits.Length; i++)
            {
                var deco = MakeText(canvas, "Deco" + i, suits[i], 320, Theme.Text.WithAlpha(0.04f));
                deco.rectTransform.At(spots[i].x, spots[i].y, 400, 400);
                deco.rectTransform.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? 14 : -12);
            }

            var logo = Node("Logo", canvas).At(0, 300, 1200, 300);
            SuitRow(logo, 105, 56);
            var title = MakeText(logo, "Title", "TEXAS HOLD'EM", 116, Theme.Gold, bold: true);
            title.rectTransform.At(0, 10, 1200, 140);
            title.characterSpacing = 4;
            MakeText(logo, "Tagline", "Multiplayer poker with friends  •  virtual chips only", 28, Theme.Muted)
                .rectTransform.At(0, -80, 1000, 40);

            // Loading state
            var loading = Node("Loading", canvas).At(0, -150, 700, 260);
            var loadingGroup = Group(loading);
            var track = Panel(loading, "ProgressTrack", Theme.Input, 9f);
            track.rectTransform.At(0, 40, 640, 18);
            var fill = Panel(track.transform, "Fill", Theme.Gold, 9f);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0.05f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var status = MakeText(loading, "Status", "Connecting to server...", 26, Theme.Muted);
            status.rectTransform.At(0, -10, 700, 40);
            var retry = MakeButton(loading, "RetryButton", "Try again", Theme.Gold, Theme.Dark, 26);
            retry.GetComponent<RectTransform>().At(0, -90, 240, 64);
            retry.gameObject.SetActive(false);

            // Login form
            var login = Panel(canvas, "LoginForm", Theme.Panel, 26f);
            login.rectTransform.At(0, -175, 620, 600);
            var loginGroup = Group(login, 0f);
            MakeText(login.transform, "Header", "Sign in to play", 40, Theme.Text, bold: true).rectTransform.At(0, 240, 560, 56);
            MakeText(login.transform, "UsernameLabel", "Username", 22, Theme.Muted, TextAlignmentOptions.Left).rectTransform.At(0, 178, 520, 30);
            var username = MakeInput(login.transform, "UsernameInput", "3-16 letters, numbers or _");
            username.GetComponent<RectTransform>().At(0, 135, 520, 66);
            username.characterLimit = 16;
            MakeText(login.transform, "PasswordLabel", "Password", 22, Theme.Muted, TextAlignmentOptions.Left).rectTransform.At(0, 73, 520, 30);
            var password = MakeInput(login.transform, "PasswordInput", "At least 6 characters");
            password.GetComponent<RectTransform>().At(0, 30, 520, 66);
            password.contentType = TMP_InputField.ContentType.Password;
            password.characterLimit = 64;
            var remember = MakeToggle(login.transform, "RememberMe", "Remember me on this device", true);
            remember.GetComponent<RectTransform>().At(0, -42, 520, 44);
            var loginButton = MakeButton(login.transform, "LoginButton", "Log in", Theme.Gold, Theme.Dark, 30);
            loginButton.GetComponent<RectTransform>().At(0, -122, 520, 74);
            var registerButton = MakeButton(login.transform, "RegisterButton", "Create account", Theme.PanelLight, Theme.Text, 26);
            registerButton.GetComponent<RectTransform>().At(0, -205, 520, 62);
            var error = MakeText(login.transform, "Error", "", 22, Theme.Red);
            error.rectTransform.At(0, -265, 560, 34);
            login.gameObject.SetActive(false);

            AddSceneMusic(SoundId.MusicLobby);
            var controller = AddController<SplashController>("SplashController");
            Wire(controller, "loadingGroup", loadingGroup);
            Wire(controller, "progressFill", fill.rectTransform);
            Wire(controller, "statusText", status);
            Wire(controller, "retryButton", retry);
            Wire(controller, "logo", logo);
            Wire(controller, "loginGroup", loginGroup);
            Wire(controller, "usernameInput", username);
            Wire(controller, "passwordInput", password);
            Wire(controller, "rememberToggle", remember);
            Wire(controller, "loginButton", loginButton);
            Wire(controller, "registerButton", registerButton);
            Wire(controller, "loginErrorText", error);

            return Save(scene, SceneNames.Splash);
        }

        // =====================================================================
        // Lobby
        // =====================================================================

        private static TableListItemView BuildTableListItemPrefab()
        {
            var row = Panel(null, "TableListItem", Theme.PanelLight, 18f);
            row.rectTransform.sizeDelta = new Vector2(1000, 100);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;

            var name = MakeText(row.transform, "Name", "Table name", 30, Theme.Text, TextAlignmentOptions.Left, bold: true);
            name.rectTransform.Pin(new Vector2(0f, 0.5f), 28, 16, 600, 38);
            var details = MakeText(row.transform, "Details", "Code ABCDE  •  Blinds 10/20", 20, Theme.Muted, TextAlignmentOptions.Left);
            details.rectTransform.Pin(new Vector2(0f, 0.5f), 28, -22, 640, 30);
            var players = MakeText(row.transform, "Players", "1/6", 30, Theme.Gold, TextAlignmentOptions.Right, bold: true);
            players.rectTransform.Pin(new Vector2(1f, 0.5f), -200, 8, 120, 38);
            MakeText(row.transform, "PlayersLabel", "players", 18, Theme.Muted, TextAlignmentOptions.Right)
                .rectTransform.Pin(new Vector2(1f, 0.5f), -200, -24, 120, 26);
            var join = MakeButton(row.transform, "JoinButton", "Join", Theme.Green, Theme.Dark, 26);
            join.GetComponent<RectTransform>().Pin(new Vector2(1f, 0.5f), -24, 0, 150, 60);

            var view = row.gameObject.AddComponent<TableListItemView>();
            Wire(view, "nameText", name);
            Wire(view, "detailsText", details);
            Wire(view, "playersText", players);
            Wire(view, "joinButton", join);
            Wire(view, "joinLabel", LabelOf(join));

            string path = $"{PrefabsFolder}/TableListItem.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(row.gameObject, path);
            Object.DestroyImmediate(row.gameObject);
            return prefab.GetComponent<TableListItemView>();
        }

        private static string BuildLobby(TableListItemView rowPrefab)
        {
            Transform canvas = NewScene(out Scene scene);

            // Top bar
            var top = Img(canvas, "TopBar", null, Theme.Dark.WithAlpha(0.85f));
            top.rectTransform.anchorMin = new Vector2(0f, 1f);
            top.rectTransform.anchorMax = new Vector2(1f, 1f);
            top.rectTransform.pivot = new Vector2(0.5f, 1f);
            top.rectTransform.sizeDelta = new Vector2(0, 110);
            var title = MakeText(top.transform, "Title", "TEXAS HOLD'EM", 46, Theme.Gold, TextAlignmentOptions.Left, bold: true);
            title.rectTransform.Pin(new Vector2(0f, 0.5f), 48, 0, 600, 70);
            var logout = MakeButton(top.transform, "LogoutButton", "Log out", Theme.PanelLight, Theme.Text, 22);
            logout.GetComponent<RectTransform>().Pin(new Vector2(1f, 0.5f), -40, 0, 150, 56);
            var welcome = MakeText(top.transform, "Welcome", "Hi, player", 28, Theme.Text, TextAlignmentOptions.Right);
            welcome.rectTransform.Pin(new Vector2(1f, 0.5f), -215, 0, 420, 50);
            var banner = BuildLostBanner(canvas, -110, "Connection lost. Reconnecting...");
            BuildConnectionStatus(top.transform, new Vector2(1f, 0.5f), -660, 0, banner);
            BuildAudioToggles(top.transform, new Vector2(1f, 0.5f), -880, 0);

            // Left column: create, join, how it works
            var create = Panel(canvas, "CreateCard", Theme.Panel, 24f);
            create.rectTransform.At(-590, 225, 640, 380);
            MakeText(create.transform, "Title", "Create a table", 34, Theme.Text, TextAlignmentOptions.Left, bold: true).rectTransform.At(0, 150, 560, 46);

            // Who you play with
            var modeRow = Node("ModeGroup", create.transform).At(0, 84, 560, 62);
            var botsButton = MakeButton(modeRow, "BotsButton", "Play vs bots", Theme.PanelLight, Theme.Text, 24);
            botsButton.GetComponent<RectTransform>().At(-142, 0, 274, 60);
            var friendsButton = MakeButton(modeRow, "FriendsButton", "Play with friends", Theme.PanelLight, Theme.Text, 24);
            friendsButton.GetComponent<RectTransform>().At(142, 0, 274, 60);
            var modeGroup = modeRow.gameObject.AddComponent<OptionButtonGroup>();
            WireArray(modeGroup, "options", new Object[] { botsButton, friendsButton });

            // Table size
            MakeText(create.transform, "PlayersLabel", "Players", 24, Theme.Muted, TextAlignmentOptions.Left, bold: true)
                .rectTransform.At(-215, 10, 130, 40);
            var seatRow = Node("SeatGroup", create.transform).At(70, 10, 420, 56);
            int seatChoices = GameRules.MaxSeats - GameRules.MinSeats + 1;
            var seatButtons = new Object[seatChoices];
            for (int i = 0; i < seatChoices; i++)
            {
                var b = MakeButton(seatRow, $"Seats{GameRules.MinSeats + i}", (GameRules.MinSeats + i).ToString(), Theme.PanelLight, Theme.Text, 26, 14f);
                b.GetComponent<RectTransform>().At((i - (seatChoices - 1) / 2f) * 84, 0, 72, 52);
                seatButtons[i] = b;
            }
            var seatGroup = seatRow.gameObject.AddComponent<OptionButtonGroup>();
            WireArray(seatGroup, "options", seatButtons);

            var modeHint = MakeText(create.transform, "ModeHint", "", 21, Theme.Muted, TextAlignmentOptions.Left);
            modeHint.rectTransform.At(0, -54, 560, 56);

            var tableName = MakeInput(create.transform, "TableNameInput", "Table name (optional)");
            tableName.GetComponent<RectTransform>().At(-95, -132, 370, 70);
            tableName.characterLimit = 24;
            var createButton = MakeButton(create.transform, "CreateButton", "Create", Theme.Gold, Theme.Dark, 28);
            createButton.GetComponent<RectTransform>().At(195, -132, 170, 70);

            var join = Panel(canvas, "JoinCard", Theme.Panel, 24f);
            join.rectTransform.At(-590, -105, 640, 220);
            MakeText(join.transform, "Title", "Join with a code", 34, Theme.Text, TextAlignmentOptions.Left, bold: true).rectTransform.At(0, 66, 560, 46);
            MakeText(join.transform, "Hint", "Got a 5-letter room code or invite from a friend?", 22, Theme.Muted, TextAlignmentOptions.Left).rectTransform.At(0, 24, 560, 32);
            var code = MakeInput(join.transform, "CodeInput", "ABCDE", 34);
            code.GetComponent<RectTransform>().At(-95, -48, 370, 70);
            code.textComponent.fontStyle = FontStyles.Bold;
            code.textComponent.characterSpacing = 12;
            code.characterLimit = 5;
            var joinButton = MakeButton(join.transform, "JoinButton", "Join", Theme.Green, Theme.Dark, 28);
            joinButton.GetComponent<RectTransform>().At(195, -48, 170, 70);

            // Your stats (the one retention feature: facts only, no goals or streaks)
            var statsCard = Panel(canvas, "StatsCard", Theme.Panel.WithAlpha(0.6f), 24f);
            statsCard.rectTransform.At(-590, -337, 640, 176);
            MakeText(statsCard.transform, "Title", LobbyTexts.StatsTitle, 26, Theme.Gold, TextAlignmentOptions.Left, bold: true)
                .rectTransform.At(0, 56, 560, 36);
            string[] statLabels = { LobbyTexts.HandsPlayed, LobbyTexts.HandsWon, LobbyTexts.BiggestPot, LobbyTexts.BestHand };
            var statValues = new TextMeshProUGUI[statLabels.Length];
            for (int i = 0; i < statLabels.Length; i++)
            {
                float x = -225 + i * 150;
                statValues[i] = MakeText(statsCard.transform, "Value" + i, LobbyTexts.NoValue, 28, Theme.Text, bold: true);
                statValues[i].rectTransform.At(x, -8, 146, 40);
                statValues[i].enableAutoSizing = true;
                statValues[i].fontSizeMin = 16;
                statValues[i].fontSizeMax = 28;
                MakeText(statsCard.transform, "Label" + i, statLabels[i], 18, Theme.Muted).rectTransform.At(x, -48, 146, 28);
            }
            var statsView = statsCard.gameObject.AddComponent<PlayerStatsView>();
            Wire(statsView, "handsPlayedText", statValues[0]);
            Wire(statsView, "handsWonText", statValues[1]);
            Wire(statsView, "biggestPotText", statValues[2]);
            Wire(statsView, "bestHandText", statValues[3]);

            // Footer: break reminder switch + free-to-play note
            BuildSettingToggle(canvas, SettingToggleButton.Setting.BreakReminders, new Vector2(0f, 0f), 28, 22, 300, 46);
            MakeText(canvas, "FreeToPlay", LobbyTexts.FreeToPlay, 20, Theme.Muted, TextAlignmentOptions.Left)
                .rectTransform.Pin(new Vector2(0f, 0f), 350, 22, 620, 46);

            // Right: open tables
            var tables = Panel(canvas, "TablesPanel", Theme.Panel, 24f);
            tables.rectTransform.At(330, -40, 1100, 820);
            MakeText(tables.transform, "Title", "Open tables", 34, Theme.Text, TextAlignmentOptions.Left, bold: true)
                .rectTransform.Pin(new Vector2(0f, 1f), 32, -26, 500, 50);
            var refresh = MakeButton(tables.transform, "RefreshButton", "Refresh", Theme.PanelLight, Theme.Text, 22);
            refresh.GetComponent<RectTransform>().Pin(new Vector2(1f, 1f), -28, -24, 160, 54);
            var content = ScrollList(tables.transform, "TableList", out ScrollRect scroll);
            scroll.GetComponent<RectTransform>().Fill(28, 28, 100, 28);
            var empty = MakeText(tables.transform, "EmptyState", "No open tables yet.\nCreate one and share the code!", 28, Theme.Muted);
            empty.rectTransform.At(0, -20, 800, 120);

            var toast = BuildToast(canvas, new Vector2(0.5f, 0f), 24);
            BuildBreakReminder(canvas);

            AddSceneMusic(SoundId.MusicLobby);
            var controller = AddController<LobbyController>("LobbyController");
            Wire(controller, "welcomeText", welcome);
            Wire(controller, "logoutButton", logout);
            Wire(controller, "modeGroup", modeGroup);
            Wire(controller, "seatGroup", seatGroup);
            Wire(controller, "modeHintText", modeHint);
            Wire(controller, "tableNameInput", tableName);
            Wire(controller, "createButton", createButton);
            Wire(controller, "codeInput", code);
            Wire(controller, "joinButton", joinButton);
            Wire(controller, "refreshButton", refresh);
            Wire(controller, "listContent", content);
            Wire(controller, "listItemPrefab", rowPrefab);
            Wire(controller, "emptyState", empty.gameObject);
            Wire(controller, "statsView", statsView);
            Wire(controller, "toast", toast);

            return Save(scene, SceneNames.Lobby);
        }

        // =====================================================================
        // Game
        // =====================================================================

        // Screen positions of the 6 seats, clockwise from the bottom (index 0 = you).
        private static readonly Vector2[] SeatPositions =
        {
            new Vector2(0, -350), new Vector2(-640, -200), new Vector2(-640, 215),
            new Vector2(0, 345), new Vector2(640, 215), new Vector2(640, -200),
        };

        // Where each seat's bet chips sit, relative to the seat (toward the table centre).
        private static readonly Vector2[] BetOffsets =
        {
            new Vector2(0, 150), new Vector2(230, 40), new Vector2(230, -60),
            new Vector2(0, -150), new Vector2(-230, -60), new Vector2(-230, 40),
        };

        private static string BuildGame()
        {
            Transform canvas = NewScene(out Scene scene);

            var table = Img(canvas, "Table", UISprites.Table, Color.white);
            table.rectTransform.At(0, 0, 1500, 760);
            var tableLogo = MakeText(table.transform, "TableLogo", "TEXAS HOLD'EM", 40, Color.white.WithAlpha(0.08f), bold: true);
            tableLogo.rectTransform.At(0, -150, 800, 60);
            tableLogo.characterSpacing = 10;

            // Board, pot, stage, waiting message
            var board = Node("Board", canvas).At(0, 20, 700, 170);
            var boardCards = new CardView[5];
            for (int i = 0; i < 5; i++)
            {
                boardCards[i] = BuildCard(board, "BoardCard" + i, 112, 158);
                boardCards[i].GetComponent<RectTransform>().anchoredPosition = new Vector2((i - 2) * 126, 0);
            }

            var pot = Panel(canvas, "Pot", Theme.Dark.WithAlpha(0.75f), 26f);
            pot.rectTransform.At(0, 150, 260, 52);
            var potChip = Img(pot.transform, "Chip", UISprites.Circle, Theme.Gold);
            potChip.rectTransform.Pin(new Vector2(0f, 0.5f), 14, 0, 30, 30);
            Img(potChip.transform, "Inner", UISprites.Ring, Theme.Dark.WithAlpha(0.5f)).rectTransform.Fill(4, 4, 4, 4);
            var potText = MakeText(pot.transform, "Amount", "Pot 0", 28, Theme.Text, bold: true);
            potText.rectTransform.Fill(50, 14, 0, 0);

            var stage = MakeText(canvas, "Stage", "FLOP", 22, Color.white.WithAlpha(0.6f), bold: true);
            stage.rectTransform.At(0, -86, 400, 30);
            stage.characterSpacing = 8;

            var waiting = MakeText(canvas, "Waiting", "Waiting for players...", 34, Theme.Text);
            waiting.rectTransform.At(0, 20, 900, 140);

            // Seats
            var seatsRoot = Node("Seats", canvas).Fill();
            var seats = new SeatView[SeatPositions.Length];
            for (int i = 0; i < seats.Length; i++)
            {
                seats[i] = BuildSeat(seatsRoot, i);
                Wire(seats[i], "dealOrigin", board); // hole cards are dealt from the middle of the table
            }

            // Flying chips (bets, pot collection, pot to winner) draw above seats and pot.
            var chipLayer = Node("ChipLayer", canvas).Fill();
            var chipTemplate = Img(chipLayer, "ChipTemplate", UISprites.Circle, Color.white);
            chipTemplate.rectTransform.At(0, 0, 30, 30);
            Img(chipTemplate.transform, "Edge", UISprites.Ring, Color.white.WithAlpha(0.8f)).rectTransform.Fill(3, 3, 3, 3);
            chipTemplate.gameObject.SetActive(false);
            var chipAnimator = chipLayer.gameObject.AddComponent<ChipAnimator>();
            Wire(chipAnimator, "chipTemplate", chipTemplate.rectTransform);

            // "THE FLOP" etc., under the board for a moment when a street starts.
            var stageBannerBg = Panel(canvas, "StageBanner", Theme.Dark.WithAlpha(0.85f), 30f);
            stageBannerBg.rectTransform.At(0, -160, 460, 74);
            var stageBannerGroup = Group(stageBannerBg, 0f);
            var stageBannerText = MakeText(stageBannerBg.transform, "Label", "THE FLOP", 44, Theme.Gold, bold: true);
            stageBannerText.rectTransform.Fill(10, 10, 0, 0);
            stageBannerText.characterSpacing = 10;
            var stageBanner = stageBannerBg.gameObject.AddComponent<StageBannerView>();
            Wire(stageBanner, "group", stageBannerGroup);
            Wire(stageBanner, "label", stageBannerText);

            var waitingRoom = BuildWaitingRoom(canvas);

            // Header: table info + code (left), connection + leave (right)
            var header = Panel(canvas, "TableInfo", Theme.Dark.WithAlpha(0.8f), 22f);
            header.rectTransform.Pin(new Vector2(0f, 1f), 28, -24, 600, 112);
            var tableName = MakeText(header.transform, "TableName", "Table", 32, Theme.Text, TextAlignmentOptions.Left, bold: true);
            tableName.rectTransform.Pin(new Vector2(0f, 1f), 24, -14, 340, 44);
            var blinds = MakeText(header.transform, "Blinds", "Blinds 10/20", 22, Theme.Muted, TextAlignmentOptions.Left);
            blinds.rectTransform.Pin(new Vector2(0f, 0f), 24, 16, 340, 32);
            var copy = MakeButton(header.transform, "CopyCodeButton", "", Theme.Gold, Theme.Dark, 30, 16f);
            copy.GetComponent<RectTransform>().Pin(new Vector2(1f, 0.5f), -16, 0, 210, 80);
            Object.DestroyImmediate(LabelOf(copy).gameObject);
            MakeText(copy.transform, "Hint", "CODE • TAP TO COPY", 14, Theme.Dark.WithAlpha(0.75f), bold: true).rectTransform.At(0, 22, 200, 20);
            var codeText = MakeText(copy.transform, "Code", "ABCDE", 36, Theme.Dark, bold: true);
            codeText.rectTransform.At(0, -10, 200, 44);
            codeText.characterSpacing = 10;

            var leave = MakeButton(canvas, "LeaveButton", "Leave", Theme.Red, Color.white, 24);
            leave.GetComponent<RectTransform>().Pin(new Vector2(1f, 1f), -28, -28, 150, 58);
            var banner = BuildLostBanner(canvas, 0, GameTexts.ConnectionLost);
            BuildConnectionStatus(canvas, new Vector2(1f, 1f), -196, -35, banner);
            BuildAudioToggles(canvas, new Vector2(1f, 1f), -28, -98);

            var actionBar = BuildActionBar(canvas);
            var toast = BuildToast(canvas, new Vector2(0.5f, 1f), -12);
            BuildBreakReminder(canvas);
            var result = BuildResultPanel(canvas);

            // Hint: hand rankings chart + rules (bottom-left corner is free at every table size).
            var rankingsButton = MakeButton(canvas, "HandRankingsButton", "♠  Hand rankings", Theme.Dark.WithAlpha(0.85f), Theme.Gold, 24);
            rankingsButton.GetComponent<RectTransform>().Pin(new Vector2(0f, 0f), 28, 28, 250, 60);
            var rankings = BuildHandRankingsPanel(canvas);

            AddSceneMusic(SoundId.MusicTable);
            var controller = AddController<GameController>("GameController");
            Wire(controller, "timerAlert", controller.gameObject.AddComponent<TurnTimerAlert>());
            Wire(controller, "tableNameText", tableName);
            Wire(controller, "codeText", codeText);
            Wire(controller, "copyCodeButton", copy);
            Wire(controller, "blindsText", blinds);
            Wire(controller, "leaveButton", leave);

            // Seats, board and the animator that plays changes between snapshots
            var seatsView = seatsRoot.gameObject.AddComponent<SeatsView>();
            WireArray(seatsView, "seats", seats);
            var boardView = board.gameObject.AddComponent<BoardView>();
            WireArray(boardView, "boardCards", boardCards);
            Wire(boardView, "potRoot", pot.gameObject);
            Wire(boardView, "potText", potText);
            Wire(boardView, "stageText", stage);
            Wire(boardView, "waitingText", waiting);
            var animator = chipLayer.gameObject.AddComponent<TableChangeAnimator>();
            Wire(animator, "chips", chipAnimator);
            Wire(animator, "banner", stageBanner);
            Wire(animator, "seats", seatsView);
            Wire(animator, "board", boardView);

            Wire(controller, "seats", seatsView);
            Wire(controller, "board", boardView);
            Wire(controller, "animator", animator);
            Wire(controller, "waitingRoom", waitingRoom);
            Wire(controller, "actionBar", actionBar);
            Wire(controller, "resultPanel", result);
            Wire(controller, "handRankingsPanel", rankings);
            Wire(controller, "handRankingsButton", rankingsButton);
            Wire(controller, "toast", toast);

            return Save(scene, SceneNames.Game);
        }

        private static SeatView BuildSeat(Transform parent, int index)
        {
            bool isMe = index == 0;
            var root = Node("Seat" + index, parent).At(SeatPositions[index].x, SeatPositions[index].y, 300, 230);
            if (isMe) root.localScale = Vector3.one * 1.1f;

            // Empty seat placeholder
            var empty = Panel(root, "Empty", Theme.Dark.WithAlpha(0.35f), 24f);
            empty.rectTransform.At(0, -18, 230, 80);
            MakeText(empty.transform, "Label", "Empty seat", 22, Theme.Muted.WithAlpha(0.8f)).rectTransform.Fill();

            var occupied = Node("Occupied", root).Fill();
            var occupiedGroup = Group(occupied);

            var glow = Img(occupied, "TurnGlow", UISprites.Glow, Theme.Gold.WithAlpha(0.55f));
            glow.rectTransform.At(0, -10, 420, 260);
            glow.gameObject.SetActive(false);

            // Hole cards, slightly fanned
            var cardsRoot = Node("Cards", occupied).At(0, 62, 160, 110);
            var cards = new CardView[2];
            for (int i = 0; i < 2; i++)
            {
                cards[i] = BuildCard(cardsRoot, "Card" + i, 74, 104);
                var rt = cards[i].GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(i == 0 ? -30 : 30, i == 0 ? 0 : -2);
                rt.localRotation = Quaternion.Euler(0, 0, i == 0 ? 7 : -7);
            }

            // Name plate with avatar and timer ring
            var body = Panel(occupied, "Body", Theme.Panel.WithAlpha(0.95f), 48f);
            body.rectTransform.At(0, -22, 276, 96);
            var ring = Img(body.transform, "TimerRing", UISprites.Ring, Theme.Green);
            ring.rectTransform.Pin(new Vector2(0f, 0.5f), -8, 0, 98, 98);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = false;
            ring.gameObject.SetActive(false);
            var avatar = Img(body.transform, "Avatar", UISprites.Circle, Theme.Blue);
            avatar.rectTransform.Pin(new Vector2(0f, 0.5f), 5, 0, 76, 76);
            var initial = MakeText(avatar.transform, "Initial", "A", 36, Color.white, bold: true);
            initial.rectTransform.Fill();
            var name = MakeText(body.transform, "Name", "Player", 26, Theme.Text, TextAlignmentOptions.Left, bold: true);
            name.rectTransform.Pin(new Vector2(0f, 0.5f), 94, 16, 170, 34);
            var chips = MakeText(body.transform, "Chips", "1,000", 24, Theme.Gold, TextAlignmentOptions.Left, bold: true);
            chips.rectTransform.Pin(new Vector2(0f, 0.5f), 94, -18, 170, 32);

            var offline = Panel(occupied, "OfflineBadge", Theme.Red, 14f);
            offline.rectTransform.At(92, 34, 104, 28);
            MakeText(offline.transform, "Label", "OFFLINE", 16, Color.white, bold: true).rectTransform.Fill();
            offline.gameObject.SetActive(false);

            var dealer = Img(occupied, "DealerButton", UISprites.Circle, Color.white);
            dealer.rectTransform.At(128, 30, 44, 44);
            MakeText(dealer.transform, "D", "D", 24, Theme.Dark, bold: true).rectTransform.Fill();
            dealer.gameObject.SetActive(false);

            // Your seat: current hand ("One Pair", "Flush"...) right under the chips,
            // with your last action below it.
            GameObject handRank = null;
            TextMeshProUGUI handRankText = null;
            if (isMe)
            {
                var rankPill = Panel(occupied, "HandRank", Theme.Dark.WithAlpha(0.92f), 17f);
                rankPill.rectTransform.At(0, -90, 220, 34);
                Img(rankPill.transform, "Accent", UISprites.Circle, Theme.Gold).rectTransform.Pin(new Vector2(0f, 0.5f), 12, 0, 12, 12);
                handRankText = MakeText(rankPill.transform, "Label", "One Pair", 20, Theme.Gold, bold: true);
                handRankText.rectTransform.Fill(28, 12, 0, 0);
                handRank = rankPill.gameObject;
                handRank.SetActive(false);
            }

            var action = Panel(occupied, "ActionBadge", Theme.Dark.WithAlpha(0.9f), 16f);
            action.rectTransform.At(0, isMe ? -128 : -88, 140, 34);
            var actionText = MakeText(action.transform, "Label", "CHECK", 18, Theme.Gold, bold: true);
            actionText.rectTransform.Fill();
            action.gameObject.SetActive(false);

            var bet = Node("Bet", occupied).At(BetOffsets[index].x, BetOffsets[index].y, 140, 40);
            var betChip = Img(bet, "Chip", UISprites.Circle, Theme.Red);
            betChip.rectTransform.Pin(new Vector2(0f, 0.5f), 0, 0, 34, 34);
            Img(betChip.transform, "Edge", UISprites.Ring, Color.white.WithAlpha(0.8f)).rectTransform.Fill(3, 3, 3, 3);
            var betPill = Panel(bet, "Pill", Theme.Dark.WithAlpha(0.8f), 16f);
            betPill.rectTransform.Pin(new Vector2(0f, 0.5f), 40, 0, 96, 32);
            var betText = MakeText(betPill.transform, "Amount", "0", 20, Theme.Text, bold: true);
            betText.rectTransform.Fill();
            bet.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<SeatView>();
            Wire(view, "occupiedRoot", occupied.gameObject);
            Wire(view, "emptyRoot", empty.gameObject);
            Wire(view, "occupiedGroup", occupiedGroup);
            Wire(view, "nameText", name);
            Wire(view, "chipsText", chips);
            Wire(view, "initialText", initial);
            Wire(view, "avatarBackground", avatar);
            Wire(view, "offlineBadge", offline.gameObject);
            Wire(view, "dealerButton", dealer.gameObject);
            WireArray(view, "cards", cards);
            Wire(view, "actionBadge", action.gameObject);
            Wire(view, "actionText", actionText);
            Wire(view, "betRoot", bet.gameObject);
            Wire(view, "betText", betText);
            Wire(view, "turnGlow", glow.gameObject);
            Wire(view, "timerRing", ring);
            Wire(view, "pulseTarget", body.transform);
            if (handRank != null)
            {
                Wire(view, "handRankRoot", handRank);
                Wire(view, "handRankText", handRankText);
            }
            occupied.gameObject.SetActive(false);
            return view;
        }

        private static ActionBar BuildActionBar(Transform canvas)
        {
            var bar = Panel(canvas, "ActionBar", Theme.Dark.WithAlpha(0.92f), 26f);
            bar.rectTransform.Pin(new Vector2(1f, 0f), -28, 28, 680, 210);
            var group = Group(bar);

            var raiseRow = Node("RaiseRow", bar.transform).At(0, 52, 640, 64);
            var halfPot = MakeButton(raiseRow, "HalfPotButton", "½ Pot", Theme.PanelLight, Theme.Text, 22);
            halfPot.GetComponent<RectTransform>().At(-265, 0, 100, 52);
            var potButton = MakeButton(raiseRow, "PotButton", "Pot", Theme.PanelLight, Theme.Text, 22);
            potButton.GetComponent<RectTransform>().At(-155, 0, 100, 52);
            var slider = MakeSlider(raiseRow, "RaiseSlider");
            slider.GetComponent<RectTransform>().At(50, 0, 280, 40);
            var amount = MakeInput(raiseRow, "RaiseInput", "Amount", 24);
            amount.GetComponent<RectTransform>().At(260, 0, 120, 52);
            amount.contentType = TMP_InputField.ContentType.IntegerNumber;
            amount.characterLimit = 7;

            var fold = MakeButton(bar.transform, "FoldButton", "Fold", Theme.Red, Color.white, 28);
            fold.GetComponent<RectTransform>().At(-243, -48, 150, 84);
            var checkCall = MakeButton(bar.transform, "CheckCallButton", "Check", Theme.Blue, Color.white, 28);
            checkCall.GetComponent<RectTransform>().At(-81, -48, 150, 84);
            var raise = MakeButton(bar.transform, "RaiseButton", "Raise", Theme.Green, Theme.Dark, 26);
            raise.GetComponent<RectTransform>().At(81, -48, 150, 84);
            var allIn = MakeButton(bar.transform, "AllInButton", "All-in", Theme.Orange, Theme.Dark, 28);
            allIn.GetComponent<RectTransform>().At(243, -48, 150, 84);

            var view = bar.gameObject.AddComponent<ActionBar>();
            Wire(view, "group", group);
            Wire(view, "foldButton", fold);
            Wire(view, "checkCallButton", checkCall);
            Wire(view, "checkCallLabel", LabelOf(checkCall));
            Wire(view, "raiseButton", raise);
            Wire(view, "raiseLabel", LabelOf(raise));
            Wire(view, "allInButton", allIn);
            Wire(view, "allInLabel", LabelOf(allIn));
            Wire(view, "raiseRow", raiseRow.gameObject);
            Wire(view, "raiseSlider", slider);
            Wire(view, "raiseInput", amount);
            Wire(view, "halfPotButton", halfPot);
            Wire(view, "potButton", potButton);
            return view;
        }

        /// <summary>
        /// Centre panel shown while a play-with-friends table fills up. The seats around
        /// it stay visible, so players see friends sit down as they join.
        /// </summary>
        private static WaitingRoomPanel BuildWaitingRoom(Transform canvas)
        {
            var root = Node("WaitingRoom", canvas).At(0, 0, 860, 440);
            var group = Group(root, 0f);
            var glow = Img(root, "Glow", UISprites.Glow, Theme.Gold.WithAlpha(0.18f));
            glow.rectTransform.At(0, 0, 1300, 800);
            Panel(root, "Background", Theme.Dark.WithAlpha(0.93f), 30f).rectTransform.Fill();
            Panel(root, "HeaderBar", Theme.Gold, 7f).rectTransform.Pin(new Vector2(0.5f, 1f), 0, -16, 260, 12);

            var title = MakeText(root, "Title", "WAITING FOR PLAYERS", 32, Theme.Gold, bold: true);
            title.rectTransform.At(0, 168, 780, 44);
            title.characterSpacing = 4;
            var status = MakeText(root, "Status", "1 of 4 players at the table", 26, Theme.Text);
            status.rectTransform.At(0, 124, 780, 36);

            var codeBox = Panel(root, "CodeBox", Theme.Input, 18f);
            codeBox.rectTransform.At(0, 48, 340, 96);
            MakeText(codeBox.transform, "Label", "ROOM CODE", 16, Theme.Muted, bold: true).rectTransform.At(0, 30, 300, 20);
            var code = MakeText(codeBox.transform, "Code", "ABCDE", 54, Theme.Gold, bold: true);
            code.rectTransform.At(0, -10, 320, 62);
            code.characterSpacing = 14;

            // Buttons share the row and stay centred when "Copy link" is hidden.
            var row = Node("Buttons", root).At(0, -46, 800, 62);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            Button RowButton(string name, string label, Color bg, Color fg)
            {
                var b = MakeButton(row, name, label, bg, fg, 24);
                var le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 230;
                le.preferredHeight = 62;
                return b;
            }
            var copyCode = RowButton("CopyCodeButton", "Copy code", Theme.PanelLight, Theme.Text);
            var copyLink = RowButton("CopyLinkButton", "Copy invite link", Theme.PanelLight, Theme.Text);
            var share = RowButton("ShareButton", "Share invite", Theme.Blue, Color.white);

            var footer = MakeText(root, "Footer", "Starts when 3 more players join.", 22, Theme.Muted);
            footer.rectTransform.At(0, -116, 780, 34);
            var start = MakeButton(root, "StartButton", "Start now", Theme.Green, Theme.Dark, 28);
            start.GetComponent<RectTransform>().At(0, -174, 300, 64);

            var view = root.gameObject.AddComponent<WaitingRoomPanel>();
            Wire(view, "group", group);
            Wire(view, "card", root);
            Wire(view, "statusText", status);
            Wire(view, "codeText", code);
            Wire(view, "footerText", footer);
            Wire(view, "copyCodeButton", copyCode);
            Wire(view, "copyLinkButton", copyLink);
            Wire(view, "shareButton", share);
            Wire(view, "startButton", start);
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>Overlay with the 10 poker hands (best first, example cards) and the basic rules.</summary>
        private static HandRankingsPanel BuildHandRankingsPanel(Transform canvas)
        {
            var root = Node("HandRankingsPanel", canvas).Fill();
            var group = Group(root, 0f);
            var dim = Img(root, "Dim", null, Color.black.WithAlpha(0.7f), raycast: true);
            dim.rectTransform.Fill();
            var backdrop = dim.gameObject.AddComponent<Button>(); // click outside the card to close
            backdrop.transition = Selectable.Transition.None;

            var card = Panel(root, "Card", Theme.Panel, 30f);
            card.rectTransform.At(0, 0, 1560, 960);
            card.raycastTarget = true; // clicks on the card must not reach the backdrop
            Panel(card.transform, "HeaderBar", Theme.Gold, 7f).rectTransform.Pin(new Vector2(0.5f, 1f), 0, -16, 320, 12);

            MakeText(card.transform, "Title", "Hand rankings", 44, Theme.Gold, TextAlignmentOptions.Left, bold: true)
                .rectTransform.Pin(new Vector2(0f, 1f), 44, -34, 700, 56);
            MakeText(card.transform, "Subtitle", "Strongest at the top. Gold cards make the hand; the faded ones are kickers.", 22, Theme.Muted, TextAlignmentOptions.Left)
                .rectTransform.Pin(new Vector2(0f, 1f), 44, -90, 1000, 32);
            var close = MakeButton(card.transform, "CloseButton", "X", Theme.PanelLight, Theme.Text, 30, 30f);
            close.GetComponent<RectTransform>().Pin(new Vector2(1f, 1f), -28, -28, 64, 64);
            var currentHand = MakeText(card.transform, "CurrentHand", "Your hand shows here once cards are dealt", 28, Theme.Text, TextAlignmentOptions.Right);
            currentHand.rectTransform.Pin(new Vector2(1f, 1f), -110, -38, 640, 44);

            // Left: one row per hand
            var list = Node("Rankings", card.transform).At(-300, -60, 900, 790);
            var cards = new System.Collections.Generic.List<Object>();
            var rowImages = new System.Collections.Generic.List<Object>();
            var yourHandTags = new System.Collections.Generic.List<Object>();
            const float rowHeight = 79f;
            for (int i = 0; i < HandChart.Entries.Length; i++)
            {
                HandChartEntry hand = HandChart.Entries[i];
                var row = Panel(list, "Row" + (i + 1), Theme.PanelLight.WithAlpha(i % 2 == 0 ? 0.45f : 0.2f), 16f);
                row.rectTransform.Pin(new Vector2(0.5f, 1f), 0, -i * rowHeight, 900, rowHeight - 7);

                var badge = Img(row.transform, "Rank", UISprites.Circle, i < 3 ? Theme.Gold : Theme.Muted);
                badge.rectTransform.Pin(new Vector2(0f, 0.5f), 14, 0, 46, 46);
                MakeText(badge.transform, "Number", (i + 1).ToString(), 24, Theme.Dark, bold: true).rectTransform.Fill();

                MakeText(row.transform, "Name", HandRanks.Names[hand.Rank], 27, Theme.Text, TextAlignmentOptions.Left, bold: true)
                    .rectTransform.Pin(new Vector2(0f, 0.5f), 76, 13, 385, 34);
                MakeText(row.transform, "Description", hand.Description, 18, Theme.Muted, TextAlignmentOptions.Left)
                    .rectTransform.Pin(new Vector2(0f, 0.5f), 76, -17, 390, 28);

                // Shown only on the row matching the player's current hand.
                var tag = Panel(row.transform, "YourHandTag", Theme.Gold, 15f);
                tag.rectTransform.Pin(new Vector2(1f, 0.5f), -306, 0, 124, 32);
                MakeText(tag.transform, "Label", "YOUR HAND", 16, Theme.Dark, bold: true).rectTransform.Fill();
                tag.gameObject.SetActive(false);
                rowImages.Add(row);
                yourHandTags.Add(tag.gameObject);

                var cardRow = Node("Cards", row.transform).Pin(new Vector2(1f, 0.5f), -16, 0, 280, 66);
                for (int c = 0; c < hand.Cards.Length; c++)
                {
                    var view = BuildCard(cardRow, "Card" + c, 46, 64);
                    view.GetComponent<RectTransform>().anchoredPosition = new Vector2((c - 2) * 54, 0);
                    cards.Add(view);
                }
            }

            // Right: how to play
            var rules = Panel(card.transform, "Rules", Theme.Input, 22f);
            rules.rectTransform.At(478, -60, 540, 790);
            MakeText(rules.transform, "Title", "How to play", 30, Theme.Gold, TextAlignmentOptions.Left, bold: true)
                .rectTransform.Pin(new Vector2(0f, 1f), 30, -24, 480, 42);
            var body = MakeText(rules.transform, "Body", HandChart.HowToPlay, 21, Theme.Text, TextAlignmentOptions.TopLeft);
            body.rectTransform.Fill(30, 30, 84, 24);
            body.lineSpacing = 8;
            body.paragraphSpacing = 6;
            body.overflowMode = TextOverflowModes.Overflow;
            body.enableAutoSizing = true; // shrink a little if the text ever outgrows the box
            body.fontSizeMin = 16;
            body.fontSizeMax = 21;

            var panel = root.gameObject.AddComponent<HandRankingsPanel>();
            Wire(panel, "group", group);
            Wire(panel, "card", card.rectTransform);
            Wire(panel, "closeButton", close);
            Wire(panel, "backdropButton", backdrop);
            WireArray(panel, "exampleCards", cards.ToArray());
            Wire(panel, "currentHandText", currentHand);
            WireArray(panel, "rowBackgrounds", rowImages.ToArray());
            WireArray(panel, "yourHandTags", yourHandTags.ToArray());
            root.gameObject.SetActive(false);
            return panel;
        }

        private static ResultPanel BuildResultPanel(Transform canvas)
        {
            var root = Node("ResultPanel", canvas).Fill();
            var group = Group(root, 0f);
            Img(root, "Dim", null, Color.black.WithAlpha(0.65f), raycast: true).rectTransform.Fill();

            var card = Node("Card", root).At(0, 0, 800, 520);
            var glow = Img(card, "Glow", UISprites.Glow, Theme.Gold.WithAlpha(0.25f));
            glow.rectTransform.At(0, 0, 1300, 900);
            var bg = Panel(card, "Background", Theme.Panel, 30f);
            bg.rectTransform.Fill();
            var header = Panel(card, "HeaderBar", Theme.Gold, 7f);
            header.rectTransform.Pin(new Vector2(0.5f, 1f), 0, -18, 320, 14);

            // Content is pinned to the top and buttons to the bottom, so the card can
            // shrink (ResultPanel) when there are no winning cards to show.
            var top = new Vector2(0.5f, 1f);
            var title = MakeText(card, "Title", "You win!", 66, Theme.Gold, bold: true);
            title.rectTransform.Pin(top, 0, -50, 740, 84);
            var subtitle = MakeText(card, "Subtitle", "with Full House", 28, Theme.Text);
            subtitle.rectTransform.Pin(top, 0, -132, 720, 70);
            var amount = MakeText(card, "Amount", "+640", 50, Theme.Gold, bold: true);
            amount.rectTransform.Pin(top, 0, -200, 600, 60);

            var cardsRow = Node("BestCards", card).Pin(top, 0, -270, 520, 130);
            var best = new CardView[5];
            for (int i = 0; i < 5; i++)
            {
                best[i] = BuildCard(cardsRow, "Card" + i, 84, 118);
                best[i].GetComponent<RectTransform>().anchoredPosition = new Vector2((i - 2) * 96, 0);
            }

            var continueButton = MakeButton(card, "ContinueButton", "Continue", Theme.Gold, Theme.Dark, 28);
            var bottom = new Vector2(0.5f, 0f);
            continueButton.GetComponent<RectTransform>().Pin(bottom, 0, 36, 280, 72);
            var rebuy = MakeButton(card, "RebuyButton", "Restart with 1,000", Theme.Green, Theme.Dark, 26);
            rebuy.GetComponent<RectTransform>().Pin(bottom, -140, 36, 340, 72);
            var lobby = MakeButton(card, "LobbyButton", "Back to lobby", Theme.PanelLight, Theme.Text, 26);
            lobby.GetComponent<RectTransform>().Pin(bottom, 195, 36, 260, 72);

            var view = root.gameObject.AddComponent<ResultPanel>();
            Wire(view, "group", group);
            Wire(view, "card", card);
            Wire(view, "headerBar", header);
            Wire(view, "titleText", title);
            Wire(view, "subtitleText", subtitle);
            Wire(view, "amountText", amount);
            WireArray(view, "bestCards", best);
            Wire(view, "continueButton", continueButton);
            Wire(view, "rebuyButton", rebuy);
            Wire(view, "lobbyButton", lobby);
            root.gameObject.SetActive(false);
            return view;
        }
    }
}
