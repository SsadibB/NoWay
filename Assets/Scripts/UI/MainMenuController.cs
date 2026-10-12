using System.Collections;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TrickyPictures
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] Button profileButton;
        [SerializeField] TMP_Text profileButtonName;
        [SerializeField] Image profileButtonAvatar;
        [SerializeField] Button settingsButton;

        [Header("Main Menu")]
        [SerializeField] Button playGloballyButton;
        [SerializeField] Button createRoomButton;
        [SerializeField] Button joinRoomButton;
        [SerializeField] Button howToPlayButton;

        [Header("Profile")]
        [SerializeField] GameObject profilePanel;
        [SerializeField] Button profileCloseButton;
        [SerializeField] Image avatarPreview;
        [SerializeField] Button[] avatarButtons;
        [SerializeField] GameObject[] avatarSelectedMarks;
        [SerializeField] TMP_Text displayNameLabel;
        [SerializeField] TMP_InputField displayNameInput;
        [SerializeField] Button saveProfileButton;

        [Header("Create Room")]
        [SerializeField] GameObject createRoomPanel;
        [SerializeField] TMP_Dropdown roundCountDropdown;
        [SerializeField] TMP_Dropdown roundDurationDropdown;
        [SerializeField] Toggle roomVisibilityToggle;
        [SerializeField] Button createButton;
        [SerializeField] Button createBackButton;

        [Header("Join Room")]
        [SerializeField] GameObject joinRoomPanel;
        [SerializeField] TMP_InputField roomCodeInput;
        [SerializeField] TMP_Text joinErrorText;
        [SerializeField] Button joinButton;
        [SerializeField] Button joinBackButton;

        [Header("Matchmaking")]
        [SerializeField] GameObject matchmakingPanel;
        [SerializeField] TMP_Text searchingText;
        [SerializeField] TMP_Text searchHintText;
        [SerializeField] Image[] searchingDots;
        [SerializeField] Button cancelSearchButton;

        [Header("Settings")]
        [SerializeField] GameObject settingsPanel;
        [SerializeField] Button settingsCloseButton;
        [SerializeField] Button settingsDoneButton;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;
        [SerializeField] Toggle fullscreenToggle;

        [Header("How To Play")]
        [SerializeField] GameObject howToPlayPanel;
        [SerializeField] Button howToPlayCloseButton;

        static readonly Color ErrorColor = new Color32(231, 76, 60, 255);

        AvatarLook selectedLook;
        AvatarPartPicker partPicker;
        string defaultNameLabel;
        Color defaultNameLabelColor;
        Coroutine matchmakingRoutine;
        CancellationTokenSource connectCts;
        bool searchCancelled;

        GameObject[] Panels => new[] { profilePanel, createRoomPanel, joinRoomPanel, matchmakingPanel, settingsPanel, howToPlayPanel };

        void Awake()
        {
            Bind(profileButton, OpenProfile);
            Bind(settingsButton, OpenSettings);

            Bind(playGloballyButton, StartMatchmaking);
            Bind(createRoomButton, () => Open(createRoomPanel));
            Bind(joinRoomButton, OpenJoinRoom);
            Bind(howToPlayButton, () => Open(howToPlayPanel));

            Bind(profileCloseButton, CloseAllPanels);
            Bind(saveProfileButton, SaveProfile);
            for (int i = 0; i < avatarButtons.Length; i++)
            {
                int index = i;
                Bind(avatarButtons[i], () => SelectAvatar(index));
            }

            Bind(createButton, CreateRoom);
            Bind(createBackButton, CloseAllPanels);

            Bind(joinButton, JoinRoom);
            Bind(joinBackButton, CloseAllPanels);
            if (roomCodeInput)
            {
                roomCodeInput.characterLimit = AppConfig.RoomCodeLength;
                roomCodeInput.onValidateInput = (text, index, c) => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '\0';
                roomCodeInput.onSubmit.AddListener(_ => JoinRoom());
            }

            Bind(cancelSearchButton, CancelMatchmaking);

            Bind(settingsCloseButton, CloseAllPanels);
            Bind(settingsDoneButton, CloseAllPanels);
            if (musicSlider) musicSlider.onValueChanged.AddListener(v => AudioService.MusicVolume = v);
            if (sfxSlider) sfxSlider.onValueChanged.AddListener(v => AudioService.SfxVolume = v);
            if (fullscreenToggle) fullscreenToggle.onValueChanged.AddListener(v => Screen.fullScreen = v);

            Bind(howToPlayCloseButton, CloseAllPanels);

            if (displayNameInput)
            {
                displayNameInput.characterLimit = AppConfig.MaxDisplayNameLength;
                displayNameInput.onSubmit.AddListener(_ => SaveProfile());
            }
            if (displayNameLabel)
            {
                defaultNameLabel = displayNameLabel.text;
                defaultNameLabelColor = displayNameLabel.color;
            }
        }

        void Start()
        {
            AudioService.ApplySavedSettings();
            CloseAllPanels();
            EnsurePartPicker();
            RefreshHeader();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (matchmakingPanel && matchmakingPanel.activeSelf) CancelMatchmaking();
                else CloseAllPanels();
            }
        }

        static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button) button.onClick.AddListener(action);
        }

        void Open(GameObject panel)
        {
            CloseAllPanels();
            if (panel) panel.SetActive(true);
        }

        void CloseAllPanels()
        {
            foreach (var p in Panels)
                if (p) p.SetActive(false);
        }

        void RefreshHeader()
        {
            var profile = ProfileService.Current;
            if (profileButtonName) profileButtonName.text = profile.DisplayName;
            if (profileButtonAvatar) AvatarPortrait.Paint(profileButtonAvatar, profile.AvatarId);
        }

        // ---------------- Profile ----------------

        void OpenProfile()
        {
            Open(profilePanel);
            EnsurePartPicker();
            var profile = ProfileService.Current;
            if (displayNameInput) displayNameInput.text = profile.DisplayName;
            ShowNameError(null);
            selectedLook = AvatarLook.Parse(profile.AvatarId);
            if (partPicker) partPicker.SetLook(selectedLook);
            if (avatarPreview) AvatarPortrait.Paint(avatarPreview, selectedLook.ToId());
        }

        void EnsurePartPicker()
        {
            if (partPicker || !avatarPreview) return;
            if (avatarButtons != null && avatarButtons.Length > 0 && avatarButtons[0])
                avatarButtons[0].transform.parent.gameObject.SetActive(false);
            var pickerObject = new GameObject("AvatarPartPicker");
            pickerObject.transform.SetParent(avatarPreview.transform.parent, false);
            partPicker = pickerObject.AddComponent<AvatarPartPicker>();
            var font = displayNameLabel ? displayNameLabel.font : TMP_Settings.defaultFontAsset;
            partPicker.Build(avatarPreview.transform.parent, font);
            partPicker.Changed += look =>
            {
                selectedLook = look;
                if (avatarPreview) AvatarPortrait.Paint(avatarPreview, look.ToId());
            };
        }

        void SelectAvatar(int index)
        {
            selectedLook = AvatarLook.Presets[Mathf.Clamp(index, 0, AvatarLook.Presets.Length - 1)];
            if (partPicker) partPicker.SetLook(selectedLook);
            if (avatarPreview) AvatarPortrait.Paint(avatarPreview, selectedLook.ToId());
        }

        void SaveProfile()
        {
            var name = ProfileService.SanitizeName(displayNameInput ? displayNameInput.text : "", out var error);
            if (error != null)
            {
                ShowNameError(error);
                return;
            }
            ProfileService.Save(name, selectedLook);
            RefreshHeader();
            CloseAllPanels();
        }

        void ShowNameError(string error)
        {
            if (!displayNameLabel) return;
            displayNameLabel.text = error ?? defaultNameLabel;
            displayNameLabel.color = error != null ? ErrorColor : defaultNameLabelColor;
        }

        // ---------------- Rooms ----------------

        void CreateRoom()
        {
            int rounds = Pick(AppConfig.RoundOptions, roundCountDropdown, AppConfig.DefaultRounds);
            int duration = Pick(AppConfig.DurationOptions, roundDurationDropdown, (int)AppConfig.DefaultRoundDuration);
            bool isPrivate = !roomVisibilityToggle || roomVisibilityToggle.isOn;
            var code = RoomCodeService.Generate();
            MatchLaunchSettings.SetPrivateRoom(rounds, duration, code, isPrivate);
            BeginConnect("Creating room " + code + "...", "Opening a Photon Fusion room",
                token => FusionSession.Ensure().CreateRoom(code, isPrivate, token),
                result =>
                {
                    MatchLaunchSettings.SetPrivateRoom(rounds, duration, string.IsNullOrEmpty(result.RoomName) ? code : result.RoomName, isPrivate);
                    SceneTransitionService.Load(AppConfig.GameScene);
                });
        }

        static int Pick(int[] options, TMP_Dropdown dropdown, int fallback)
        {
            if (!dropdown || dropdown.value < 0 || dropdown.value >= options.Length) return fallback;
            return options[dropdown.value];
        }

        void OpenJoinRoom()
        {
            Open(joinRoomPanel);
            if (roomCodeInput)
            {
                roomCodeInput.text = "";
                roomCodeInput.ActivateInputField();
            }
            if (joinErrorText) joinErrorText.text = "";
        }

        void JoinRoom()
        {
            var code = RoomCodeService.Normalize(roomCodeInput ? roomCodeInput.text : "");
            if (!RoomCodeService.IsValidFormat(code))
            {
                if (joinErrorText) joinErrorText.text = "Enter a valid six-character code";
                return;
            }
            if (joinErrorText) joinErrorText.text = "";
            BeginConnect("Joining " + code + "...", "Looking up that room on Photon Fusion",
                token => FusionSession.Ensure().JoinRoom(code, token),
                result =>
                {
                    MatchLaunchSettings.SetPrivateRoom(AppConfig.DefaultRounds, AppConfig.DefaultRoundDuration, code, true);
                    SceneTransitionService.Load(AppConfig.GameScene);
                },
                error =>
                {
                    Open(joinRoomPanel);
                    if (joinErrorText) joinErrorText.text = error;
                });
        }

        // ---------------- Matchmaking ----------------

        void StartMatchmaking()
        {
            BeginConnect("Searching for players...", "Finding an open global room",
                token => FusionSession.Ensure().ConnectGlobal(token),
                result =>
                {
                    MatchLaunchSettings.SetGlobal(result.RoomName);
                    SceneTransitionService.Load(AppConfig.GameScene);
                });
        }

        void BeginConnect(string title, string hint, System.Func<CancellationToken, System.Threading.Tasks.Task<FusionSession.ConnectResult>> connect, System.Action<FusionSession.ConnectResult> onSuccess, System.Action<string> onError = null)
        {
            searchCancelled = false;
            connectCts?.Cancel();
            connectCts?.Dispose();
            connectCts = new CancellationTokenSource();
            Open(matchmakingPanel);
            if (matchmakingRoutine != null) StopCoroutine(matchmakingRoutine);
            matchmakingRoutine = StartCoroutine(ConnectRoutine(title, hint, connect, onSuccess, onError));
        }

        void CancelMatchmaking()
        {
            searchCancelled = true;
            connectCts?.Cancel();
            if (matchmakingRoutine != null) StopCoroutine(matchmakingRoutine);
            matchmakingRoutine = null;
            FusionSession.Instance?.BeginShutdown();
            CloseAllPanels();
        }

        IEnumerator ConnectRoutine(string title, string hint, System.Func<CancellationToken, System.Threading.Tasks.Task<FusionSession.ConnectResult>> connect, System.Action<FusionSession.ConnectResult> onSuccess, System.Action<string> onError)
        {
            SetSearchText(title, hint);
            var task = connect(connectCts.Token);
            while (!task.IsCompleted)
            {
                if (searchCancelled) yield break;
                TickDots();
                yield return null;
            }
            matchmakingRoutine = null;
            if (searchCancelled) yield break;
            if (task.IsFaulted)
            {
                var message = task.Exception.GetBaseException().Message;
                if (onError != null) onError(message);
                else SetSearchText("Couldn't connect", message);
                yield break;
            }
            var result = task.Result;
            if (!result.Ok)
            {
                if (onError != null) onError(result.Error);
                else SetSearchText("Couldn't connect", result.Error);
                yield break;
            }
            onSuccess(result);
        }

        void SetSearchText(string title, string hint)
        {
            if (searchingText) searchingText.text = title;
            if (searchHintText) searchHintText.text = hint;
        }

        void TickDots()
        {
            if (searchingDots == null) return;
            float t = Time.unscaledTime;
            for (int i = 0; i < searchingDots.Length; i++)
            {
                if (!searchingDots[i]) continue;
                var c = searchingDots[i].color;
                c.a = 0.25f + 0.75f * Mathf.Abs(Mathf.Sin(t * 3f - i * 0.6f));
                searchingDots[i].color = c;
            }
        }

        // ---------------- Settings ----------------

        void OpenSettings()
        {
            Open(settingsPanel);
            if (musicSlider) musicSlider.SetValueWithoutNotify(AudioService.MusicVolume);
            if (sfxSlider) sfxSlider.SetValueWithoutNotify(AudioService.SfxVolume);
            if (fullscreenToggle)
            {
                fullscreenToggle.gameObject.SetActive(!Application.isMobilePlatform);
                fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
            }
        }
    }
}
