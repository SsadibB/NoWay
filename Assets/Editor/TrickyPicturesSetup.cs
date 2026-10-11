using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickyPictures.EditorTools
{
    /// <summary>
    /// Adds the gameplay components to the hand-editable scene objects and assigns their references by name.
    /// Never changes positions, sizes or anchors, so it is safe to re-run after layout edits.
    /// </summary>
    public static class TrickyPicturesSetup
    {
        const string ScenesFolder = "Assets/Scenes/";
        const string PuzzleFolder = "Assets/Puzzles";
        const string DatabasePath = PuzzleFolder + "/PuzzleDatabase.asset";

        static int warnings;

        [MenuItem("Tricky Pictures/Wire Scenes && Puzzles")]
        public static void WireAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previousScene = SceneManager.GetActiveScene().path;
            warnings = 0;

            EnsurePuzzles();
            WireScene("Bootstrap", WireBootstrap);
            WireScene("MainMenu", WireMainMenu);
            // Opening a scene unloads unreferenced assets, so the database is loaded after the scene opens.
            WireScene("Game", scene => WireGame(scene, AssetDatabase.LoadAssetAtPath<PuzzleDatabase>(DatabasePath)));

            if (!string.IsNullOrEmpty(previousScene)) EditorSceneManager.OpenScene(previousScene);
            Debug.Log(warnings == 0
                ? "[Tricky Pictures] All scenes wired successfully."
                : $"[Tricky Pictures] Scenes wired with {warnings} warning(s); see messages above.");
        }

        static void WireScene(string sceneName, System.Action<Scene> wire)
        {
            var scene = EditorSceneManager.OpenScene(ScenesFolder + sceneName + ".unity");
            wire(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ------------------------------------------------------------------ Bootstrap

        static void WireBootstrap(Scene scene)
        {
            var app = Root(scene, "AppRoot");
            var loading = Root(scene, "LoadingCanvas");
            if (!app || !loading) return;

            var bootstrap = Add<GameBootstrap>(Find(app, "GameBootstrap"));
            Add<ProfileService>(Find(app, "ProfileService"));
            Add<AudioService>(Find(app, "AudioService"));
            Add<SceneTransitionService>(Find(app, "SceneTransitionService"));
            new Wire(bootstrap)
                .Ref("loadingFill", Get<Image>(loading, "LoadingIndicator/Fill"))
                .Ref("statusText", Get<TMP_Text>(loading, "StatusText"))
                .Apply();
        }

        // ------------------------------------------------------------------ Main menu

        static void WireMainMenu(Scene scene)
        {
            var c = Root(scene, "MainCanvas");
            if (!c) return;
            var menu = Add<MainMenuController>(c);
            const string profile = "ProfilePanel/Window/";
            const string create = "CreateRoomPanel/Window/";
            const string join = "JoinRoomPanel/Window/";
            const string match = "MatchmakingPanel/Window/";
            const string settings = "SettingsPanel/Window/";

            var avatarButtons = new Object[8];
            var avatarMarks = new Object[8];
            for (int i = 0; i < 8; i++)
            {
                avatarButtons[i] = Get<Button>(c, $"{profile}AvatarGrid/Avatar_{i + 1}");
                avatarMarks[i] = Obj(c, $"{profile}AvatarGrid/Avatar_{i + 1}/SelectedMark");
            }

            new Wire(menu)
                .Ref("profileButton", Get<Button>(c, "Header/ProfileButton"))
                .Ref("profileButtonName", Get<TMP_Text>(c, "Header/ProfileButton/DisplayNameText"))
                .Ref("profileButtonAvatar", Get<Image>(c, "Header/ProfileButton/AvatarIcon"))
                .Ref("settingsButton", Get<Button>(c, "Header/SettingsButton"))
                .Ref("playGloballyButton", Get<Button>(c, "MainMenuPanel/PlayGloballyButton"))
                .Ref("createRoomButton", Get<Button>(c, "MainMenuPanel/CreateRoomButton"))
                .Ref("joinRoomButton", Get<Button>(c, "MainMenuPanel/JoinRoomButton"))
                .Ref("howToPlayButton", Get<Button>(c, "MainMenuPanel/HowToPlayButton"))
                .Ref("profilePanel", Obj(c, "ProfilePanel"))
                .Ref("profileCloseButton", Get<Button>(c, profile + "CloseButton"))
                .Ref("avatarPreview", Get<Image>(c, profile + "AvatarPreview"))
                .Array("avatarButtons", avatarButtons)
                .Array("avatarSelectedMarks", avatarMarks)
                .Ref("displayNameLabel", Get<TMP_Text>(c, profile + "DisplayNameLabel"))
                .Ref("displayNameInput", Get<TMP_InputField>(c, profile + "DisplayNameInput"))
                .Ref("saveProfileButton", Get<Button>(c, profile + "SaveProfileButton"))
                .Ref("createRoomPanel", Obj(c, "CreateRoomPanel"))
                .Ref("roundCountDropdown", Get<TMP_Dropdown>(c, create + "RoundCountSelector/Dropdown"))
                .Ref("roundDurationDropdown", Get<TMP_Dropdown>(c, create + "RoundDurationSelector/Dropdown"))
                .Ref("roomVisibilityToggle", Get<Toggle>(c, create + "RoomVisibilityToggle"))
                .Ref("createButton", Get<Button>(c, create + "CreateButton"))
                .Ref("createBackButton", Get<Button>(c, create + "BackButton"))
                .Ref("joinRoomPanel", Obj(c, "JoinRoomPanel"))
                .Ref("roomCodeInput", Get<TMP_InputField>(c, join + "RoomCodeInput"))
                .Ref("joinErrorText", Get<TMP_Text>(c, join + "ErrorText"))
                .Ref("joinButton", Get<Button>(c, join + "JoinButton"))
                .Ref("joinBackButton", Get<Button>(c, join + "BackButton"))
                .Ref("matchmakingPanel", Obj(c, "MatchmakingPanel"))
                .Ref("searchingText", Get<TMP_Text>(c, match + "SearchingText"))
                .Ref("searchHintText", Get<TMP_Text>(c, match + "SearchHintText"))
                .Array("searchingDots", Get<Image>(c, match + "SearchingIndicator/Dot1"), Get<Image>(c, match + "SearchingIndicator/Dot2"), Get<Image>(c, match + "SearchingIndicator/Dot3"))
                .Ref("cancelSearchButton", Get<Button>(c, match + "CancelSearchButton"))
                .Ref("settingsPanel", Obj(c, "SettingsPanel"))
                .Ref("settingsCloseButton", Get<Button>(c, settings + "CloseButton"))
                .Ref("settingsDoneButton", Get<Button>(c, settings + "DoneButton"))
                .Ref("musicSlider", Get<Slider>(c, settings + "MusicVolume/Slider"))
                .Ref("sfxSlider", Get<Slider>(c, settings + "SfxVolume/Slider"))
                .Ref("fullscreenToggle", Get<Toggle>(c, settings + "FullscreenToggle"))
                .Ref("howToPlayPanel", Obj(c, "HowToPlayPanel"))
                .Ref("howToPlayCloseButton", Get<Button>(c, "HowToPlayPanel/Window/CloseButton"))
                .Apply();
        }

        // ------------------------------------------------------------------ Game

        static void WireGame(Scene scene, PuzzleDatabase database)
        {
            var c = Root(scene, "GameCanvas");
            if (!c) return;

            var controllerRoot = Root(scene, "MatchController", warnIfMissing: false);
            if (!controllerRoot)
            {
                var go = new GameObject("MatchController");
                SceneManager.MoveGameObjectToScene(go, scene);
                controllerRoot = go.transform;
            }
            var roundManager = Add<RoundManager>(controllerRoot);
            if (!database) Warn("PuzzleDatabase asset not found at " + DatabasePath);
            new Wire(roundManager).Ref("puzzleDatabase", database).Apply();

            var puzzleText = EnsurePuzzleText(c);

            const string lobby = "LobbyPanel/Window/";
            const string result = "RoundResultPanel/Window/";
            const string final = "FinalLeaderboardPanel/Window/";
            const string pause = "PausePanel/Window/";
            const string chat = "ChatPanel/ChatScrollView/Viewport/Content/";

            var ui = Add<GameUIController>(c);
            new Wire(ui)
                .Ref("roundManager", roundManager)
                .Ref("roomCodeLabel", Get<TMP_Text>(c, "TopBar/RoomCodeLabel"))
                .Ref("roundCounter", Get<TMP_Text>(c, "TopBar/RoundCounter"))
                .Ref("countdownTimer", Get<TMP_Text>(c, "TopBar/CountdownTimer"))
                .Ref("connectionStatusText", Get<TMP_Text>(c, "TopBar/ConnectionStatus/StatusText"))
                .Ref("connectionStatusDot", Get<Image>(c, "TopBar/ConnectionStatus/StatusDot"))
                .Ref("pauseButton", Get<Button>(c, "TopBar/PauseButton"))
                .Ref("playerListHeader", Get<TMP_Text>(c, "PlayerListPanel/Header"))
                .Ref("playerCardTemplate", Card(c, "PlayerListPanel/PlayerListScrollView/Viewport/Content/PlayerCardTemplate", "Score"))
                .Ref("puzzleImage", Get<Image>(c, "PuzzlePanel/PuzzleImage"))
                .Ref("puzzleText", puzzleText)
                .Ref("roundStatusText", Get<TMP_Text>(c, "PuzzlePanel/RoundStatusText"))
                .Ref("timeProgressFill", Get<Image>(c, "PuzzlePanel/TimeProgressBar/Fill"))
                .Ref("chatScrollRect", Get<ScrollRect>(c, "ChatPanel/ChatScrollView"))
                .Ref("chatMessageTemplate", Message(c, chat + "ChatMessageTemplate"))
                .Ref("correctGuessTemplate", Message(c, chat + "CorrectGuessTemplate"))
                .Ref("systemMessageTemplate", Message(c, chat + "SystemMessageTemplate"))
                .Ref("chatModeToggle", Get<Toggle>(c, "ChatPanel/ChatModeSelector/ChatModeToggle"))
                .Ref("guessModeToggle", Get<Toggle>(c, "ChatPanel/ChatModeSelector/GuessModeToggle"))
                .Ref("chatInput", Get<TMP_InputField>(c, "ChatPanel/ChatInput"))
                .Ref("sendButton", Get<Button>(c, "ChatPanel/SendButton"))
                .Ref("lobbyPanel", Obj(c, "LobbyPanel"))
                .Ref("lobbyRoomCodeText", Get<TMP_Text>(c, lobby + "RoomCodeBox/RoomCodeText"))
                .Ref("copyCodeButton", Get<Button>(c, lobby + "RoomCodeBox/CopyCodeButton"))
                .Ref("shareButton", Get<Button>(c, lobby + "RoomCodeBox/ShareButton"))
                .Ref("lobbyPlayerCountText", Get<TMP_Text>(c, lobby + "PlayerCountText"))
                .Ref("lobbySlotTemplate", Card(c, lobby + "LobbyPlayerGrid/LobbyPlayerSlotTemplate", "ReadyStatus"))
                .Ref("matchSettingsRoot", Obj(c, lobby + "MatchSettings"))
                .Ref("roundsDropdown", Get<TMP_Dropdown>(c, lobby + "MatchSettings/RoundsDropdown"))
                .Ref("durationDropdown", Get<TMP_Dropdown>(c, lobby + "MatchSettings/DurationDropdown"))
                .Ref("lobbyLeaveButton", Get<Button>(c, lobby + "LeaveRoomButton"))
                .Ref("readyButton", Get<Button>(c, lobby + "ReadyButton"))
                .Ref("startMatchButton", Get<Button>(c, lobby + "StartMatchButton"))
                .Ref("roundResultPanel", Obj(c, "RoundResultPanel"))
                .Ref("roundResultTitle", Get<TMP_Text>(c, result + "Title"))
                .Ref("answerText", Get<TMP_Text>(c, result + "AnswerText"))
                .Ref("roundScoreRowTemplate", Card(c, result + "RoundScoreList/Viewport/Content/RoundScoreRowTemplate", "Value"))
                .Ref("nextRoundText", Get<TMP_Text>(c, result + "NextRoundText"))
                .Ref("finalLeaderboardPanel", Obj(c, "FinalLeaderboardPanel"))
                .Array("podiumPlaces", Card(c, final + "Podium/FirstPlace", "Score"), Card(c, final + "Podium/SecondPlace", "Score"), Card(c, final + "Podium/ThirdPlace", "Score"))
                .Ref("rankingRowTemplate", Card(c, final + "RankingsScrollView/Viewport/Content/RankingRowTemplate", "Value"))
                .Ref("playAgainButton", Get<Button>(c, final + "PlayAgainButton"))
                .Ref("returnToLobbyButton", Get<Button>(c, final + "ReturnToLobbyButton"))
                .Ref("finalLeaveButton", Get<Button>(c, final + "LeaveRoomButton"))
                .Ref("pausePanel", Obj(c, "PausePanel"))
                .Ref("resumeButton", Get<Button>(c, pause + "ResumeButton"))
                .Ref("pauseSettingsButton", Get<Button>(c, pause + "SettingsButton"))
                .Ref("leaveMatchButton", Get<Button>(c, pause + "LeaveMatchButton"))
                .Ref("toast", Obj(c, "NotificationLayer/ToastTemplate"))
                .Ref("toastText", Get<TMP_Text>(c, "NotificationLayer/ToastTemplate/MessageText"))
                .Apply();
        }

        // The rebus text lives on the old placeholder label inside PuzzleImage.
        static TMP_Text EnsurePuzzleText(Transform canvas)
        {
            var image = Find(canvas, "PuzzlePanel/PuzzleImage");
            if (!image) return null;
            var existing = image.Find("PuzzleText");
            if (existing) return existing.GetComponent<TMP_Text>();

            var label = image.Find("PlaceholderLabel");
            if (!label)
            {
                Warn("Missing PuzzlePanel/PuzzleImage/PuzzleText");
                return null;
            }
            label.name = "PuzzleText";
            var text = label.GetComponent<TMP_Text>();
            text.text = "?";
            text.color = new Color32(24, 28, 56, 255);
            text.fontStyle = FontStyles.Bold;
            text.enableAutoSizing = true;
            text.fontSizeMin = 24;
            text.fontSizeMax = 110;
            text.lineSpacing = -10;
            text.margin = new Vector4(30, 20, 30, 20);
            return text;
        }

        static PlayerCardView Card(Transform root, string path, string valueChild)
        {
            var t = Find(root, path);
            if (!t) return null;
            var view = Add<PlayerCardView>(t);
            new Wire(view)
                .Ref("rankText", Child<TMP_Text>(t, "Rank"))
                .Ref("avatar", Child<Image>(t, "Avatar"))
                .Ref("nameText", Child<TMP_Text>(t, "PlayerName"))
                .Ref("valueText", Child<TMP_Text>(t, valueChild))
                .Ref("hostBadge", ChildObj(t, "HostBadge"))
                .Ref("guessedIcon", ChildObj(t, "GuessedIcon"))
                .Apply();
            return view;
        }

        static ChatMessageView Message(Transform root, string path)
        {
            var t = Find(root, path);
            if (!t) return null;
            var view = Add<ChatMessageView>(t);
            new Wire(view)
                .Ref("senderName", Child<TMP_Text>(t, "SenderName"))
                .Ref("avatar", Child<Image>(t, "Avatar"))
                .Ref("messageText", Child<TMP_Text>(t, "MessageText"))
                .Apply();
            return view;
        }

        // ------------------------------------------------------------------ Puzzles

        struct PuzzleSeed
        {
            public string Id, Answer, Rebus;
            public string[] Alternatives;
            public PuzzleDifficulty Difficulty;

            public PuzzleSeed(string id, string answer, PuzzleDifficulty difficulty, string rebus, params string[] alternatives)
            {
                Id = id;
                Answer = answer;
                Difficulty = difficulty;
                Rebus = rebus;
                Alternatives = alternatives;
            }
        }

        const string Nbsp = "\u00A0";

        static readonly PuzzleSeed[] Seeds =
        {
            new PuzzleSeed("downtown", "Downtown", PuzzleDifficulty.Easy, "T\nO\nW\nN", "down town"),
            new PuzzleSeed("breakfast", "Breakfast", PuzzleDifficulty.Easy, "<rotate=-14>FA</rotate>    <voffset=-0.35em><rotate=16>ST</rotate></voffset>", "break fast"),
            new PuzzleSeed("stepfather", "Stepfather", PuzzleDifficulty.Medium,
                "FATHER\n<mspace=0.6em>" +
                Pad("         __") + "\n" + Pad("      __|") + "\n" + Pad("   __|") + "\n" + Pad("__|") + "</mspace>", "step father"),
            new PuzzleSeed("forget-it", "Forget it", PuzzleDifficulty.Medium, "GET IT\nGET IT\nGET IT\nGET IT", "forgetit"),
            new PuzzleSeed("try-to-understand", "Try to understand", PuzzleDifficulty.Hard, "<u>STAND</u>\nTRY 2", "try 2 understand", "try to under stand"),
            new PuzzleSeed("mind-over-matter", "Mind over matter", PuzzleDifficulty.Medium, "<u>MIND</u>\nMATTER"),
            new PuzzleSeed("head-over-heels", "Head over heels", PuzzleDifficulty.Medium, "<u>HEAD</u>\nHEELS"),
            new PuzzleSeed("once-upon-a-time", "Once upon a time", PuzzleDifficulty.Hard, "<u>ONCE</u>\nA TIME"),
            new PuzzleSeed("man-overboard", "Man overboard", PuzzleDifficulty.Medium, "<u>MAN</u>\nBOARD", "man over board"),
            new PuzzleSeed("big-deal", "Big deal", PuzzleDifficulty.Easy, "<size=170%>DEAL</size>", "a big deal"),
            new PuzzleSeed("reading-between-the-lines", "Reading between the lines", PuzzleDifficulty.Hard, "__________\nREADING\n__________"),
            new PuzzleSeed("backwards", "Backwards", PuzzleDifficulty.Easy, "SDRAWKCAB", "backward"),
            new PuzzleSeed("split-second", "Split second", PuzzleDifficulty.Easy, "SEC          OND", "a split second"),
            new PuzzleSeed("upside-down", "Upside down", PuzzleDifficulty.Medium, "<rotate=180>NWOD</rotate>", "upsidedown"),
        };

        // Pads to equal width with non-breaking spaces so centred monospace lines stay aligned.
        static string Pad(string line) => (line + new string(' ', Mathf.Max(0, 11 - line.Length))).Replace(" ", Nbsp);

        static void EnsurePuzzles()
        {
            if (!AssetDatabase.IsValidFolder(PuzzleFolder)) AssetDatabase.CreateFolder("Assets", "Puzzles");

            var database = AssetDatabase.LoadAssetAtPath<PuzzleDatabase>(DatabasePath);
            if (!database)
            {
                database = ScriptableObject.CreateInstance<PuzzleDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            for (int i = 0; i < Seeds.Length; i++)
            {
                var seed = Seeds[i];
                var path = $"{PuzzleFolder}/Puzzle_{i + 1:00}_{seed.Answer.Replace(" ", "")}.asset";
                var puzzle = AssetDatabase.LoadAssetAtPath<PuzzleDefinition>(path);
                if (puzzle) continue; // never overwrite edited puzzles

                puzzle = ScriptableObject.CreateInstance<PuzzleDefinition>();
                puzzle.PuzzleId = seed.Id;
                puzzle.CanonicalAnswer = seed.Answer;
                puzzle.AcceptedAnswers = seed.Alternatives;
                puzzle.RebusText = seed.Rebus;
                puzzle.Difficulty = seed.Difficulty;
                AssetDatabase.CreateAsset(puzzle, path);
                if (!database.Puzzles.Contains(puzzle)) database.Puzzles.Add(puzzle);
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ Helpers

        static void Warn(string message)
        {
            warnings++;
            Debug.LogWarning("[Tricky Pictures] " + message);
        }

        static Transform Root(Scene scene, string name, bool warnIfMissing = true)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go.transform;
            if (warnIfMissing) Warn($"Scene '{scene.name}' has no root object '{name}'");
            return null;
        }

        static Transform Find(Transform root, string path)
        {
            if (!root) return null;
            var t = root.Find(path);
            if (!t) Warn($"Missing object '{root.name}/{path}'");
            return t;
        }

        static T Get<T>(Transform root, string path) where T : Component
        {
            var t = Find(root, path);
            if (!t) return null;
            var c = t.GetComponent<T>();
            if (!c) Warn($"'{root.name}/{path}' has no {typeof(T).Name}");
            return c;
        }

        static GameObject Obj(Transform root, string path)
        {
            var t = Find(root, path);
            return t ? t.gameObject : null;
        }

        static T Child<T>(Transform parent, string name) where T : Component
        {
            var t = parent.Find(name);
            return t ? t.GetComponent<T>() : null;
        }

        static GameObject ChildObj(Transform parent, string name)
        {
            var t = parent.Find(name);
            return t ? t.gameObject : null;
        }

        static T Add<T>(Transform t) where T : Component
        {
            if (!t) return null;
            var c = t.GetComponent<T>();
            return c ? c : t.gameObject.AddComponent<T>();
        }

        class Wire
        {
            readonly SerializedObject so;

            public Wire(Object target)
            {
                so = target ? new SerializedObject(target) : null;
            }

            public Wire Ref(string field, Object value)
            {
                if (so == null) return this;
                var p = so.FindProperty(field);
                if (p == null) Warn($"{so.targetObject.GetType().Name} has no field '{field}'");
                else p.objectReferenceValue = value;
                return this;
            }

            public Wire Array(string field, params Object[] values)
            {
                if (so == null) return this;
                var p = so.FindProperty(field);
                if (p == null)
                {
                    Warn($"{so.targetObject.GetType().Name} has no field '{field}'");
                    return this;
                }
                p.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                return this;
            }

            public void Apply() => so?.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
