using System.Collections;
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

        int selectedAvatar;
        string defaultNameLabel;
        Color defaultNameLabelColor;
        Coroutine matchmakingRoutine;

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
            if (profileButtonAvatar) profileButtonAvatar.color = AvatarPalette.Get(profile.AvatarId);
        }

        // ---------------- Profile ----------------

        void OpenProfile()
        {
            Open(profilePanel);
            var profile = ProfileService.Current;
            if (displayNameInput) displayNameInput.text = profile.DisplayName;
            ShowNameError(null);
            SelectAvatar(ProfileService.AvatarIndex(profile.AvatarId));
        }

        void SelectAvatar(int index)
        {
            selectedAvatar = index;
            if (avatarPreview) avatarPreview.color = AvatarPalette.Get(index);
            for (int i = 0; i < avatarSelectedMarks.Length; i++)
                if (avatarSelectedMarks[i]) avatarSelectedMarks[i].SetActive(i == index);
        }

        void SaveProfile()
        {
            var name = ProfileService.SanitizeName(displayNameInput ? displayNameInput.text : "", out var error);
            if (error != null)
            {
                ShowNameError(error);
                return;
            }
            ProfileService.Save(name, selectedAvatar);
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
            MatchLaunchSettings.SetPrivateRoom(rounds, duration, RoomCodeService.Generate(), isPrivate);
            SceneTransitionService.Load(AppConfig.GameScene);
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
            string message = RoomCodeService.IsValidFormat(code)
                ? "Room not found. Joining a friend's room needs online multiplayer (Photon Fusion), which isn't set up yet."
                : "Enter a valid six-character code";
            if (joinErrorText) joinErrorText.text = message;
        }

        // ---------------- Matchmaking ----------------

        void StartMatchmaking()
        {
            Open(matchmakingPanel);
            if (matchmakingRoutine != null) StopCoroutine(matchmakingRoutine);
            matchmakingRoutine = StartCoroutine(MatchmakingRoutine());
        }

        void CancelMatchmaking()
        {
            if (matchmakingRoutine != null) StopCoroutine(matchmakingRoutine);
            matchmakingRoutine = null;
            CloseAllPanels();
        }

        IEnumerator MatchmakingRoutine()
        {
            SetSearchText("Searching for players...", "Looking for a public room with free space");
            yield return AnimateDots(2.5f);

            SetSearchText("Online matchmaking isn't available yet",
                "Photon Fusion isn't installed, so a solo practice match will start instead.");
            yield return AnimateDots(3f);

            matchmakingRoutine = null;
            MatchLaunchSettings.SetPractice();
            SceneTransitionService.Load(AppConfig.GameScene);
        }

        void SetSearchText(string title, string hint)
        {
            if (searchingText) searchingText.text = title;
            if (searchHintText) searchHintText.text = hint;
        }

        IEnumerator AnimateDots(float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < searchingDots.Length; i++)
                {
                    if (!searchingDots[i]) continue;
                    var c = searchingDots[i].color;
                    c.a = 0.25f + 0.75f * Mathf.Abs(Mathf.Sin(t * 3f - i * 0.6f));
                    searchingDots[i].color = c;
                }
                yield return null;
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
