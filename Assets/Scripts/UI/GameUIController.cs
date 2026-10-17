using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TrickyPictures
{
    public class GameUIController : MonoBehaviour
    {
        [Header("Logic")]
        [SerializeField] RoundManager roundManager;

        [Header("Top Bar")]
        [SerializeField] TMP_Text roomCodeLabel;
        [SerializeField] TMP_Text roundCounter;
        [SerializeField] TMP_Text countdownTimer;
        [SerializeField] TMP_Text connectionStatusText;
        [SerializeField] Image connectionStatusDot;
        [SerializeField] Button pauseButton;

        [Header("Player List")]
        [SerializeField] TMP_Text playerListHeader;
        [SerializeField] PlayerCardView playerCardTemplate;

        [Header("Puzzle")]
        [SerializeField] Image puzzleImage;
        [SerializeField] TMP_Text puzzleText;
        [SerializeField] TMP_Text roundStatusText;
        [SerializeField] Image timeProgressFill;

        [Header("Chat")]
        [SerializeField] ScrollRect chatScrollRect;
        [SerializeField] ChatMessageView chatMessageTemplate;
        [SerializeField] ChatMessageView correctGuessTemplate;
        [SerializeField] ChatMessageView systemMessageTemplate;
        [SerializeField] Toggle chatModeToggle;
        [SerializeField] Toggle guessModeToggle;
        [SerializeField] TMP_InputField chatInput;
        [SerializeField] Button sendButton;

        [Header("Lobby")]
        [SerializeField] GameObject lobbyPanel;
        [SerializeField] TMP_Text lobbyRoomCodeText;
        [SerializeField] Button copyCodeButton;
        [SerializeField] Button shareButton;
        [SerializeField] TMP_Text lobbyPlayerCountText;
        [SerializeField] PlayerCardView lobbySlotTemplate;
        [SerializeField] GameObject matchSettingsRoot;
        [SerializeField] TMP_Dropdown roundsDropdown;
        [SerializeField] TMP_Dropdown durationDropdown;
        [SerializeField] Button lobbyLeaveButton;
        [SerializeField] Button readyButton;
        [SerializeField] Button startMatchButton;

        [Header("Round Result")]
        [SerializeField] GameObject roundResultPanel;
        [SerializeField] TMP_Text roundResultTitle;
        [SerializeField] TMP_Text answerText;
        [SerializeField] PlayerCardView roundScoreRowTemplate;
        [SerializeField] TMP_Text nextRoundText;

        [Header("Final Leaderboard")]
        [SerializeField] GameObject finalLeaderboardPanel;
        [Tooltip("First, second and third place, in that order.")]
        [SerializeField] PlayerCardView[] podiumPlaces;
        [SerializeField] PlayerCardView rankingRowTemplate;
        [SerializeField] Button playAgainButton;
        [SerializeField] Button returnToLobbyButton;
        [SerializeField] Button finalLeaveButton;

        [Header("Pause")]
        [SerializeField] GameObject pausePanel;
        [SerializeField] Button resumeButton;
        [SerializeField] Button pauseSettingsButton;
        [SerializeField] Button leaveMatchButton;

        [Header("Notifications")]
        [SerializeField] GameObject toast;
        [SerializeField] TMP_Text toastText;
        [SerializeField] float toastDuration = 2.5f;

        const int MaxChatMessages = 100;
        static readonly Color OfflineColor = new Color32(230, 126, 34, 255);
        static readonly Color OnlineColor = new Color32(46, 204, 113, 255);
        static readonly Color TimerColor = new Color32(255, 201, 60, 255);
        static readonly Color TimerWarningColor = new Color32(231, 76, 60, 255);
        const float TimerWarningSeconds = 5f;

        readonly List<GameObject> playerCards = new List<GameObject>();
        readonly List<GameObject> lobbySlots = new List<GameObject>();
        readonly List<GameObject> roundRows = new List<GameObject>();
        readonly List<GameObject> rankingRows = new List<GameObject>();
        readonly Queue<GameObject> chatMessages = new Queue<GameObject>();
        readonly RateLimiter chatLimiter = new RateLimiter(AppConfig.ChatRateLimitCount, AppConfig.ChatRateLimitWindow);

        MatchPlayer localPlayer;
        OnlineMatch online;
        bool showingLobby;
        Coroutine toastRoutine;

        bool IsOnline => MatchLaunchSettings.IsOnline;

        void Awake()
        {
            foreach (var template in new Component[]
                     {
                         playerCardTemplate, chatMessageTemplate, correctGuessTemplate, systemMessageTemplate,
                         lobbySlotTemplate, roundScoreRowTemplate, rankingRowTemplate,
                     })
                if (template) template.gameObject.SetActive(false);
            if (toast) toast.SetActive(false);

            Bind(pauseButton, TogglePause);
            Bind(sendButton, SubmitChatInput);
            if (chatInput)
            {
                chatInput.characterLimit = AppConfig.MaxMessageLength;
                chatInput.onSubmit.AddListener(_ => SubmitChatInput());
            }
            var modeSelector = chatModeToggle ? chatModeToggle.transform.parent
                : guessModeToggle ? guessModeToggle.transform.parent : null;
            if (modeSelector) modeSelector.gameObject.SetActive(false);

            Bind(copyCodeButton, () => CopyRoomCode("Room code copied!"));
            Bind(shareButton, () => CopyRoomCode("Room code copied - send it to your friends!"));
            Bind(readyButton, ToggleReady);
            Bind(startMatchButton, StartMatchFromLobby);
            Bind(lobbyLeaveButton, LeaveToMenu);

            Bind(playAgainButton, PlayAgain);
            Bind(returnToLobbyButton, ShowLobby);
            Bind(finalLeaveButton, LeaveToMenu);

            Bind(resumeButton, TogglePause);
            Bind(pauseSettingsButton, () => ShowToast("Audio settings are in the main menu."));
            Bind(leaveMatchButton, LeaveToMenu);
        }

        void Start()
        {
            if (!roundManager)
            {
                Debug.LogError("[GameUIController] RoundManager is not assigned.", this);
                enabled = false;
                return;
            }

            roundManager.Configure(MatchLaunchSettings.Rounds, MatchLaunchSettings.RoundDuration);
            roundManager.RoundStarted += OnRoundStarted;
            roundManager.CorrectGuess += OnCorrectGuess;
            roundManager.IncorrectGuess += OnIncorrectGuess;
            roundManager.RoundEnded += OnRoundEnded;
            roundManager.MatchEnded += OnMatchEnded;
            roundManager.ScoresChanged += RefreshPlayerList;
            roundManager.RosterChanged += RefreshLobby;

            var session = FusionSession.Instance;
            bool connected = IsOnline && session && session.IsOnline;
            if (IsOnline && !connected)
            {
                AddSystemMessage("The online session dropped. Returning to the menu.");
                LeaveToMenu();
                return;
            }

            if (connectionStatusText)
            {
                connectionStatusText.text = connected ? (session.IsHost ? "Host" : "Online") : "Offline";
                connectionStatusText.color = connected ? OnlineColor : OfflineColor;
            }
            if (connectionStatusDot) connectionStatusDot.color = connected ? OnlineColor : OfflineColor;
            if (roomCodeLabel) roomCodeLabel.text = RoomLabel();

            if (chatInput && chatInput.placeholder is TMP_Text placeholder)
                placeholder.text = "Chat or guess the word...";
            HideOverlays();

            if (connected)
            {
                online = gameObject.AddComponent<OnlineMatch>();
                online.Initialize(roundManager, this);
                ShowLobby();
            }
            else
            {
                var profile = ProfileService.Current;
                localPlayer = new MatchPlayer(profile.PlayerId, profile.DisplayName, profile.AvatarId, isHost: true);
                roundManager.AddPlayer(localPlayer);
                RefreshPlayerList();
                AddSystemMessage("Solo practice. Use Play Globally or a room code for online matches.");
                BeginMatch();
            }
        }

        void OnDestroy()
        {
            if (!roundManager) return;
            roundManager.RoundStarted -= OnRoundStarted;
            roundManager.CorrectGuess -= OnCorrectGuess;
            roundManager.IncorrectGuess -= OnIncorrectGuess;
            roundManager.RoundEnded -= OnRoundEnded;
            roundManager.MatchEnded -= OnMatchEnded;
            roundManager.ScoresChanged -= RefreshPlayerList;
            roundManager.RosterChanged -= RefreshLobby;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) TogglePause();

            if (roundManager.Phase == MatchPhase.Playing)
            {
                float remaining = roundManager.RemainingTime;
                if (countdownTimer)
                {
                    int seconds = Mathf.CeilToInt(remaining);
                    countdownTimer.text = $"{seconds / 60}:{seconds % 60:00}";
                    countdownTimer.color = remaining <= TimerWarningSeconds ? TimerWarningColor : TimerColor;
                }
                if (timeProgressFill) timeProgressFill.fillAmount = remaining / roundManager.RoundDuration;
            }
            else if (roundManager.Phase == MatchPhase.RoundResult && nextRoundText)
            {
                int seconds = Mathf.Max(1, Mathf.CeilToInt(roundManager.ResultTimeRemaining));
                nextRoundText.text = (roundManager.IsLastRound ? "Final results in " : "Next round in ") + seconds + "...";
            }

            if (online && lobbyPanel && lobbyPanel.activeSelf && lobbyPlayerCountText)
                lobbyPlayerCountText.text = online.LobbyStatus(roundManager.Players.Count);
        }

        string RoomLabel()
        {
            if (MatchLaunchSettings.Mode == MatchMode.Global) return "Global Match";
            if (MatchLaunchSettings.Mode == MatchMode.PrivateRoom) return "Room: " + MatchLaunchSettings.RoomCode;
            return "Solo Practice";
        }

        static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button) button.onClick.AddListener(action);
        }

        static T Spawn<T>(T template) where T : Component
        {
            var instance = Instantiate(template, template.transform.parent);
            instance.gameObject.SetActive(true);
            return instance;
        }

        static void Clear(List<GameObject> spawned)
        {
            foreach (var go in spawned)
            {
                if (!go) continue;
                go.SetActive(false);
                Destroy(go);
            }
            spawned.Clear();
        }

        void HideOverlays()
        {
            foreach (var panel in new[] { lobbyPanel, roundResultPanel, finalLeaderboardPanel, pausePanel })
                if (panel) panel.SetActive(false);
        }

        void BeginMatch()
        {
            HideOverlays();
            if (!roundManager.StartMatch())
            {
                AddSystemMessage("No puzzles available. Check the PuzzleDatabase asset.");
                ShowToast("No puzzles available");
            }
        }

        // ---------------- Round events ----------------

        void OnRoundStarted()
        {
            if (lobbyPanel) lobbyPanel.SetActive(false);
            if (roundResultPanel) roundResultPanel.SetActive(false);
            if (finalLeaderboardPanel) finalLeaderboardPanel.SetActive(false);
            var puzzle = roundManager.CurrentPuzzle;

            if (puzzleImage)
            {
                puzzleImage.sprite = puzzle.Image;
                puzzleImage.color = Color.white;
            }
            if (puzzleText)
            {
                puzzleText.gameObject.SetActive(puzzle.Image == null);
                puzzleText.text = puzzle.RebusText;
            }

            if (roundCounter) roundCounter.text = $"Round {roundManager.RoundIndex + 1} / {roundManager.TotalRounds}";
            UpdateRoundStatus();
            AddSystemMessage($"Round {roundManager.RoundIndex + 1} has started!");
            FocusChatInput();
        }

        void OnCorrectGuess(MatchPlayer player, int position, int points)
        {
            bool isLocal = player.PlayerId == ProfileService.Current.PlayerId;
            AddCorrectGuessMessage(isLocal ? "You guessed the word" : player.DisplayName + " guessed the word");
            if (isLocal && roundStatusText) roundStatusText.text = $"Correct! +{points} points";
        }

        void OnIncorrectGuess(MatchPlayer player, string text)
        {
            AddChatMessage(player, Escape(text));
        }

        void OnRoundEnded(PuzzleDefinition puzzle)
        {
            if (puzzle == null) return;
            AddSystemMessage("The answer was: " + puzzle.CanonicalAnswer);
            if (roundStatusText) roundStatusText.text = "Round over!";
            if (timeProgressFill) timeProgressFill.fillAmount = roundManager.RemainingTime / roundManager.RoundDuration;

            if (roundResultTitle) roundResultTitle.text = $"Round {roundManager.RoundIndex + 1} Complete!";
            if (answerText) answerText.text = puzzle.CanonicalAnswer.ToUpperInvariant();

            Clear(roundRows);
            if (roundScoreRowTemplate)
            {
                var standings = roundManager.GetStandings();
                for (int i = 0; i < standings.Count; i++)
                {
                    var p = standings[i];
                    var row = Spawn(roundScoreRowTemplate);
                    row.Set(RankOf(standings, i), DisplayNameOf(p), p.AvatarId, "+" + roundManager.Scores.GetRoundPoints(p.PlayerId));
                    roundRows.Add(row.gameObject);
                }
            }
            if (roundResultPanel) roundResultPanel.SetActive(true);
        }

        void OnMatchEnded()
        {
            if (roundResultPanel) roundResultPanel.SetActive(false);
            if (pausePanel) pausePanel.SetActive(false);
            if (countdownTimer) countdownTimer.text = "0:00";

            var standings = roundManager.GetStandings();
            for (int i = 0; i < podiumPlaces.Length; i++)
            {
                if (!podiumPlaces[i]) continue;
                bool filled = i < standings.Count;
                podiumPlaces[i].gameObject.SetActive(filled);
                if (filled)
                    podiumPlaces[i].Set(0, DisplayNameOf(standings[i]), standings[i].AvatarId, Points(standings[i]));
            }

            Clear(rankingRows);
            if (rankingRowTemplate)
            {
                for (int i = 0; i < standings.Count; i++)
                {
                    var row = Spawn(rankingRowTemplate);
                    row.Set(RankOf(standings, i), DisplayNameOf(standings[i]), standings[i].AvatarId, Points(standings[i]));
                    rankingRows.Add(row.gameObject);
                }
            }

            AddSystemMessage("Match over! " + DisplayNameOf(standings[0]) + " wins with " + Points(standings[0]) + ".");
            if (finalLeaderboardPanel) finalLeaderboardPanel.SetActive(true);
        }

        // Players with equal totals share a rank.
        int RankOf(List<MatchPlayer> standings, int index)
        {
            int rank = index + 1;
            while (index > 0 && roundManager.Scores.GetTotal(standings[index - 1].PlayerId) == roundManager.Scores.GetTotal(standings[index].PlayerId))
            {
                index--;
                rank = index + 1;
            }
            return rank;
        }

        string Points(MatchPlayer p) => roundManager.Scores.GetTotal(p.PlayerId) + " pts";

        string DisplayNameOf(MatchPlayer p) =>
            p.PlayerId == ProfileService.Current.PlayerId ? p.DisplayName + " (You)" : p.DisplayName;

        void RefreshPlayerList()
        {
            if (playerListHeader) playerListHeader.text = $"Players  {roundManager.Players.Count} / {AppConfig.MaxPlayers}";
            Clear(playerCards);
            if (!playerCardTemplate) return;

            var standings = roundManager.GetStandings();
            bool roundActive = roundManager.Phase == MatchPhase.Playing || roundManager.Phase == MatchPhase.RoundResult;
            for (int i = 0; i < standings.Count; i++)
            {
                var p = standings[i];
                var card = Spawn(playerCardTemplate);
                card.Set(RankOf(standings, i), DisplayNameOf(p), p.AvatarId, Points(p), p.IsHost, roundActive && roundManager.HasSolved(p.PlayerId));
                playerCards.Add(card.gameObject);
            }
        }

        // ---------------- Chat ----------------

        void UpdateRoundStatus()
        {
            if (!roundStatusText || roundManager == null || roundManager.Phase != MatchPhase.Playing) return;
            if (localPlayer != null && roundManager.HasSolved(localPlayer.PlayerId)) return;
            roundStatusText.text = "Chat here, or type the word to guess!";
        }

        void SubmitChatInput()
        {
            if (!chatInput || localPlayer == null) return;
            var text = chatInput.text.Trim();
            if (text.Length == 0) return;
            if (text.Length > AppConfig.MaxMessageLength) text = text.Substring(0, AppConfig.MaxMessageLength);
            chatInput.text = "";
            FocusChatInput();

            if (!chatLimiter.TryConsume())
            {
                AddSystemMessage("You're sending messages too fast.");
                return;
            }

            if (online != null)
            {
                online.SendChat(text);
                return;
            }

            switch (roundManager.SubmitGuess(localPlayer.PlayerId, roundManager.RoundIndex, text))
            {
                case GuessResult.Correct:
                case GuessResult.Incorrect:
                    return;
                case GuessResult.AlreadySolved:
                    if (GuessProcessor.IsCorrect(text, roundManager.CurrentPuzzle))
                    {
                        AddSystemMessage("You already solved this puzzle!");
                        return;
                    }
                    break;
                case GuessResult.RateLimited:
                    AddSystemMessage("Slow down a little!");
                    return;
            }

            AddChatMessage(localPlayer, Escape(text));
        }

        void FocusChatInput()
        {
            if (chatInput && chatInput.isActiveAndEnabled && !Application.isMobilePlatform) chatInput.ActivateInputField();
        }

        static string Escape(string userText) => "<noparse>" + userText.Replace("</noparse>", "") + "</noparse>";

        void AddChatMessage(MatchPlayer sender, string richText) =>
            AddMessage(chatMessageTemplate, DisplayNameOf(sender), sender.AvatarId, richText);

        void AddCorrectGuessMessage(string text) => AddMessage(correctGuessTemplate, "", "", text);

        void AddSystemMessage(string text) => AddMessage(systemMessageTemplate, "", "", text);

        void AddMessage(ChatMessageView template, string sender, string avatarId, string text)
        {
            if (!template) return;
            var view = Spawn(template);
            view.Set(sender, avatarId, text);
            chatMessages.Enqueue(view.gameObject);
            while (chatMessages.Count > MaxChatMessages) Destroy(chatMessages.Dequeue());
            if (isActiveAndEnabled) StartCoroutine(ScrollChatToBottom());
        }

        IEnumerator ScrollChatToBottom()
        {
            yield return null;
            if (chatScrollRect) chatScrollRect.verticalNormalizedPosition = 0f;
        }

        // ---------------- Lobby ----------------

        public void ShowLobby()
        {
            if (showingLobby) return;
            showingLobby = true;
            roundManager.ReturnToLobby();
            HideOverlays();
            if (roundCounter) roundCounter.text = "Lobby";
            if (countdownTimer)
            {
                countdownTimer.text = $"{(int)roundManager.RoundDuration / 60}:{(int)roundManager.RoundDuration % 60:00}";
                countdownTimer.color = TimerColor;
            }
            if (timeProgressFill) timeProgressFill.fillAmount = 1f;
            if (roundStatusText) roundStatusText.text = "Waiting for the host to start the match";
            if (puzzleText)
            {
                puzzleText.gameObject.SetActive(true);
                puzzleText.text = "?";
            }
            if (puzzleImage) puzzleImage.sprite = null;

            RefreshLobby();
            RefreshPlayerList();

            bool hasCode = MatchLaunchSettings.Mode == MatchMode.PrivateRoom && !string.IsNullOrEmpty(MatchLaunchSettings.RoomCode);
            if (lobbyRoomCodeText)
                lobbyRoomCodeText.text = MatchLaunchSettings.Mode == MatchMode.Global ? "GLOBAL" : (hasCode ? MatchLaunchSettings.RoomCode : "SOLO");
            if (copyCodeButton) copyCodeButton.interactable = hasCode;
            if (shareButton) shareButton.interactable = hasCode;

            bool isHost = localPlayer != null && localPlayer.IsHost;
            if (matchSettingsRoot) matchSettingsRoot.SetActive(isHost);
            if (startMatchButton) startMatchButton.gameObject.SetActive(isHost);
            if (roundsDropdown) roundsDropdown.SetValueWithoutNotify(IndexOf(AppConfig.RoundOptions, roundManager.TotalRounds));
            if (durationDropdown) durationDropdown.SetValueWithoutNotify(IndexOf(AppConfig.DurationOptions, (int)roundManager.RoundDuration));

            if (lobbyPanel) lobbyPanel.SetActive(true);
            if (online && online.ShouldAnnounceLobby) online.AnnounceLobby();
            showingLobby = false;
        }

        static int IndexOf(int[] options, int value)
        {
            int index = System.Array.IndexOf(options, value);
            return index >= 0 ? index : 0;
        }

        void RefreshLobby()
        {
            var me = roundManager.GetPlayer(ProfileService.Current.PlayerId);
            if (me != null) localPlayer = me;
            int count = roundManager.Players.Count;
            if (lobbyPlayerCountText)
                lobbyPlayerCountText.text = online != null
                    ? online.LobbyStatus(count)
                    : $"Players {count} / {AppConfig.MaxPlayers}";

            Clear(lobbySlots);
            if (lobbySlotTemplate)
            {
                foreach (var p in roundManager.Players)
                {
                    var slot = Spawn(lobbySlotTemplate);
                    slot.Set(0, DisplayNameOf(p), p.AvatarId, p.IsReady ? "Ready" : "Not ready", p.IsHost);
                    lobbySlots.Add(slot.gameObject);
                }
            }

            if (readyButton)
            {
                var label = readyButton.GetComponentInChildren<TMP_Text>();
                if (label) label.text = localPlayer != null && localPlayer.IsReady ? "Not Ready" : "Ready";
            }
        }

        void ToggleReady()
        {
            if (localPlayer == null) return;
            if (online != null)
            {
                online.SendReady(!localPlayer.IsReady);
                return;
            }
            localPlayer.IsReady = !localPlayer.IsReady;
            RefreshLobby();
        }

        public void StartMatchFromLobby()
        {
            if (localPlayer == null || !localPlayer.IsHost) return;
            int rounds = roundsDropdown ? AppConfig.RoundOptions[Mathf.Clamp(roundsDropdown.value, 0, AppConfig.RoundOptions.Length - 1)] : roundManager.TotalRounds;
            int duration = durationDropdown ? AppConfig.DurationOptions[Mathf.Clamp(durationDropdown.value, 0, AppConfig.DurationOptions.Length - 1)] : (int)roundManager.RoundDuration;
            MatchLaunchSettings.SetMatchRules(rounds, duration);
            if (online != null)
            {
                online.HostStart(rounds, duration);
                return;
            }
            roundManager.Configure(rounds, duration);
            AddSystemMessage($"Match started: {rounds} rounds, {duration} seconds each.");
            BeginMatch();
        }

        void CopyRoomCode(string message)
        {
            if (string.IsNullOrEmpty(MatchLaunchSettings.RoomCode)) return;
            GUIUtility.systemCopyBuffer = MatchLaunchSettings.RoomCode;
            ShowToast(message);
        }

        // ---------------- Results / pause / navigation ----------------

        void PlayAgain()
        {
            if (online != null)
            {
                if (!online.IsHost)
                {
                    online.RequestPlayAgain();
                    AddSystemMessage("Asking the host to play again...");
                    return;
                }
                online.HostStart(roundManager.TotalRounds, roundManager.RoundDuration);
                return;
            }
            AddSystemMessage("New match started!");
            BeginMatch();
        }

        void TogglePause()
        {
            if (!pausePanel || !roundManager.IsMatchRunning) return;
            bool paused = !pausePanel.activeSelf;
            pausePanel.SetActive(paused);
            if (online == null || online.IsHost) roundManager.Paused = paused;
        }

        public void LeaveToMenu()
        {
            FusionSession.Instance?.BeginShutdown();
            SceneTransitionService.Load(AppConfig.MainMenuScene);
        }

        public void ApplyNetworkRoster(List<MatchPlayer> roster)
        {
            roundManager.ApplyRoster(roster);
            localPlayer = roundManager.GetPlayer(ProfileService.Current.PlayerId);
        }

        public void PresentChat(string playerId, string text)
        {
            var player = roundManager.GetPlayer(playerId);
            if (player == null) return;
            AddChatMessage(player, Escape(text));
        }

        public void PresentSystem(string text) => AddSystemMessage(text);

        void ShowToast(string message)
        {
            if (!toast) return;
            if (toastText) toastText.text = message;
            toast.SetActive(true);
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(HideToastAfterDelay());
        }

        IEnumerator HideToastAfterDelay()
        {
            yield return new WaitForSecondsRealtime(toastDuration);
            toast.SetActive(false);
            toastRoutine = null;
        }
    }
}
