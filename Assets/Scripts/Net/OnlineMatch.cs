using System.Collections.Generic;
using System.Globalization;
using Fusion;
using UnityEngine;

namespace TrickyPictures
{
    /// <summary>Host-authoritative match sync over <see cref="FusionSession"/>.</summary>
    public class OnlineMatch : MonoBehaviour
    {
        FusionSession session;
        RoundManager rounds;
        GameUIController ui;
        readonly List<Seat> seats = new List<Seat>();
        string pendingRound;
        string lastIds = "";
        bool suppress;
        float autoStartIn = -1f;
        bool startConsumed;

        public bool IsHost => session && session.IsHost;
        public float AutoStartIn => autoStartIn;
        public bool SuppressBroadcast { get; set; }

        class Seat
        {
            public PlayerRef Ref;
            public string Id;
            public string Name;
            public string Avatar;
            public bool Ready;
            public bool Connected = true;
        }

        public void Initialize(RoundManager roundManager, GameUIController gameUi)
        {
            rounds = roundManager;
            ui = gameUi;
            session = FusionSession.Instance;
            if (!session || !session.IsOnline) return;

            rounds.RemoteDriven = !session.IsHost;
            session.MessageReceived += OnMessage;
            session.PlayerLeft += OnPlayerLeft;
            session.Disconnected += OnDisconnected;
            if (session.IsHost)
            {
                rounds.RoundStarted += OnHostRoundStarted;
                rounds.RoundEnded += OnHostRoundEnded;
                rounds.MatchEnded += OnHostMatchEnded;
                rounds.CorrectGuess += OnHostCorrect;
                rounds.IncorrectGuess += OnHostIncorrect;
            }
            session.Flush();
            SendHello();
        }

        void OnDestroy()
        {
            if (!session) return;
            session.MessageReceived -= OnMessage;
            session.PlayerLeft -= OnPlayerLeft;
            session.Disconnected -= OnDisconnected;
            if (!rounds) return;
            rounds.RoundStarted -= OnHostRoundStarted;
            rounds.RoundEnded -= OnHostRoundEnded;
            rounds.MatchEnded -= OnHostMatchEnded;
            rounds.CorrectGuess -= OnHostCorrect;
            rounds.IncorrectGuess -= OnHostIncorrect;
        }

        void Update()
        {
            if (!IsHost || rounds == null) return;
            if (rounds.Phase == MatchPhase.Playing || rounds.Phase == MatchPhase.RoundResult)
            {
                if (Time.frameCount % 15 == 0)
                    session.Broadcast(FusionSession.Pack("time",
                        ((int)rounds.Phase).ToString(),
                        rounds.RemainingTime.ToString("0.00", CultureInfo.InvariantCulture),
                        rounds.ResultTimeRemaining.ToString("0.00", CultureInfo.InvariantCulture)));
                autoStartIn = -1f;
                return;
            }

            if (rounds.Phase != MatchPhase.Lobby || MatchLaunchSettings.Mode != MatchMode.Global) 
            {
                autoStartIn = -1f;
                startConsumed = false;
                return;
            }

            int connected = 0;
            foreach (var seat in seats)
                if (seat.Connected) connected++;
            if (connected >= AppConfig.MinPublicPlayers)
            {
                if (autoStartIn < 0f) autoStartIn = 5f;
                autoStartIn -= Time.deltaTime;
                if (autoStartIn <= 0f && !startConsumed)
                {
                    startConsumed = true;
                    ui.StartMatchFromLobby();
                }
            }
            else
            {
                autoStartIn = -1f;
                startConsumed = false;
            }
        }

        public string LobbyStatus(int count)
        {
            if (autoStartIn >= 0f)
                return $"Players {count} / {AppConfig.MaxPlayers}  -  starting in {Mathf.CeilToInt(autoStartIn)}";
            if (MatchLaunchSettings.Mode == MatchMode.Global && count < AppConfig.MinPublicPlayers)
                return $"Players {count} / {AppConfig.MaxPlayers}  -  waiting for another player";
            return $"Players {count} / {AppConfig.MaxPlayers}";
        }

        public void SendChat(string text) => session.SendToHost(FusionSession.Pack("chat", text));

        public void SendGuess(string text) =>
            session.SendToHost(FusionSession.Pack("guess", rounds.RoundIndex.ToString(), text));

        public void SendReady(bool ready) => session.SendToHost(FusionSession.Pack("ready", ready ? "1" : "0"));

        public void RequestPlayAgain() => session.SendToHost(FusionSession.Pack("again"));

        public void AnnounceLobby()
        {
            if (!IsHost) return;
            session.SetJoinsOpen(true);
            session.Broadcast(FusionSession.Pack("lobby"));
        }

        public void HostStart(int roundCount, float duration)
        {
            if (!IsHost) return;
            startConsumed = true;
            autoStartIn = -1f;
            rounds.Configure(roundCount, duration);
            var ids = rounds.DrawPuzzleIds();
            if (ids.Count == 0)
            {
                startConsumed = false;
                autoStartIn = 5f;
                ui.PresentSystem("No puzzles available. Check the PuzzleDatabase asset.");
                return;
            }
            lastIds = string.Join(",", ids);
            session.Broadcast(FusionSession.Pack("deal", roundCount.ToString(), duration.ToString(CultureInfo.InvariantCulture), lastIds));
            if (!rounds.LoadIds(ids, roundCount, duration))
            {
                startConsumed = false;
                autoStartIn = 5f;
                ui.PresentSystem("No puzzles available. Check the PuzzleDatabase asset.");
                return;
            }
            session.SetJoinsOpen(false);
            rounds.Kickoff();
            ui.PresentSystem($"Match started: {roundCount} rounds, {duration:0} seconds each.");
        }

        void SendHello()
        {
            var profile = ProfileService.Current;
            var message = FusionSession.Pack("hello", profile.PlayerId, profile.DisplayName, profile.AvatarId);
            if (IsHost) OnMessage(session.LocalRef, message);
            else session.SendToHost(message);
        }

        void OnMessage(PlayerRef from, string message)
        {
            var parts = message.Split(FusionSession.Sep);
            if (parts.Length == 0) return;
            switch (parts[0])
            {
                case "hello" when IsHost && parts.Length >= 4:
                    Welcome(from, parts[1], parts[2], parts[3]);
                    break;
                case "ready" when IsHost && parts.Length >= 2:
                    SetReady(from, parts[1] == "1");
                    break;
                case "chat" when IsHost && parts.Length >= 2:
                    RelayChat(from, parts[1]);
                    break;
                case "guess" when IsHost && parts.Length >= 3:
                    TakeGuess(from, parts[1], parts[2]);
                    break;
                case "again" when IsHost:
                    HostStart(rounds.TotalRounds, rounds.RoundDuration);
                    break;
                case "roster":
                    ApplyRoster(parts.Length > 1 ? parts[1] : "");
                    break;
                case "deal" when !IsHost && parts.Length >= 4:
                    AcceptDeal(parts[1], parts[2], parts[3]);
                    break;
                case "round" when !IsHost:
                    AcceptRound(parts.Length > 1 ? parts[1] : "");
                    break;
                case "result" when !IsHost && parts.Length >= 2:
                    if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pause))
                        rounds.RemoteEnterResult(pause);
                    break;
                case "finished" when !IsHost:
                    rounds.RemoteFinish();
                    break;
                case "time" when !IsHost && parts.Length >= 4:
                    if (float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var remaining) &&
                        float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var resultRemaining))
                        rounds.RemoteSetClock(remaining, resultRemaining);
                    break;
                case "said" when parts.Length >= 3:
                    ui.PresentChat(parts[1], parts[2]);
                    break;
                case "ok" when !IsHost && parts.Length >= 3:
                    if (int.TryParse(parts[2], out var position))
                        rounds.RemoteApplyCorrect(parts[1], position);
                    break;
                case "no" when !IsHost && parts.Length >= 3:
                    rounds.RemoteApplyIncorrect(parts[1], parts[2]);
                    break;
                case "sys" when parts.Length >= 2:
                    ui.PresentSystem(parts[1]);
                    break;
                case "lobby" when !IsHost:
                    suppress = true;
                    ui.ShowLobby();
                    suppress = false;
                    break;
            }
        }

        void Welcome(PlayerRef from, string id, string displayName, string avatarId)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64) return;
            displayName = Sanitize(string.IsNullOrWhiteSpace(displayName) ? AppConfig.DefaultDisplayName : displayName.Trim());
            if (displayName.Length > AppConfig.MaxDisplayNameLength) displayName = displayName.Substring(0, AppConfig.MaxDisplayNameLength);
            var look = AvatarLook.Parse(avatarId);

            Seat seat = null;
            foreach (var existing in seats)
            {
                if (existing.Ref == from) seat = existing;
                else if (existing.Id == id && existing.Connected)
                    return;
            }
            if (seat == null)
            {
                seat = new Seat { Ref = from };
                seats.Add(seat);
            }
            seat.Id = id;
            seat.Name = displayName;
            seat.Avatar = look.ToId();
            seat.Connected = true;
            Publish();
            if (rounds.Phase != MatchPhase.Lobby && !string.IsNullOrEmpty(lastIds))
            {
                session.SendTo(from, FusionSession.Pack("deal",
                    rounds.TotalRounds.ToString(),
                    rounds.RoundDuration.ToString(CultureInfo.InvariantCulture),
                    lastIds));
                session.SendTo(from, FusionSession.Pack("round", RoundPayload()));
            }
        }

        static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace(FusionSession.Sep.ToString(), "").Replace("\u001e", "").Replace("\u001d", "");
        }

        void SetReady(PlayerRef from, bool ready)
        {
            var seat = Find(from);
            if (seat == null) return;
            seat.Ready = ready;
            Publish();
        }

        void RelayChat(PlayerRef from, string text)
        {
            var seat = Find(from);
            if (seat == null || string.IsNullOrWhiteSpace(text)) return;
            text = Sanitize(text);
            if (text.Length > AppConfig.MaxMessageLength) text = text.Substring(0, AppConfig.MaxMessageLength);
            ui.PresentChat(seat.Id, text);
            session.Broadcast(FusionSession.Pack("said", seat.Id, text));
        }

        void TakeGuess(PlayerRef from, string roundText, string text)
        {
            var seat = Find(from);
            if (seat == null) return;
            if (!int.TryParse(roundText, out var roundIndex)) return;
            text = Sanitize(text);
            switch (rounds.SubmitGuess(seat.Id, roundIndex, text))
            {
                case GuessResult.AlreadySolved:
                    session.SendTo(from, FusionSession.Pack("sys", "You already solved this puzzle!"));
                    break;
                case GuessResult.RateLimited:
                    session.SendTo(from, FusionSession.Pack("sys", "Slow down a little!"));
                    break;
                case GuessResult.NotPlaying:
                    session.SendTo(from, FusionSession.Pack("sys", "Guesses are only accepted while a round is running."));
                    break;
                case GuessResult.WrongRound:
                    session.SendTo(from, FusionSession.Pack("sys", "Too late - that round already moved on."));
                    break;
            }
        }

        void OnPlayerLeft(PlayerRef player)
        {
            if (!IsHost) return;
            var seat = Find(player);
            if (seat == null) return;
            seat.Connected = false;
            Publish();
            rounds.HostEndRoundIfComplete();
        }

        void Publish()
        {
            var records = new List<string>(seats.Count);
            foreach (var seat in seats)
            {
                records.Add(string.Join("\u001d", new[]
                {
                    seat.Id,
                    seat.Name,
                    seat.Avatar,
                    seat.Ref == session.LocalRef ? "1" : "0",
                    seat.Ready ? "1" : "0",
                    seat.Connected ? "1" : "0",
                }));
            }
            var payload = string.Join("\u001e", records);
            ApplyRoster(payload);
            session.Broadcast(FusionSession.Pack("roster", payload));
        }

        void ApplyRoster(string payload)
        {
            var roster = new List<MatchPlayer>();
            if (!string.IsNullOrEmpty(payload))
            {
                foreach (var record in payload.Split('\u001e'))
                {
                    var f = record.Split('\u001d');
                    if (f.Length < 6 || string.IsNullOrEmpty(f[0])) continue;
                    var player = new MatchPlayer(f[0], f[1], f[2], f[3] == "1")
                    {
                        IsReady = f[4] == "1",
                        IsConnected = f[5] == "1",
                    };
                    roster.Add(player);
                }
            }
            ui.ApplyNetworkRoster(roster);
        }

        void AcceptDeal(string roundsText, string durationText, string idCsv)
        {
            int.TryParse(roundsText, out var count);
            float.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration);
            var ids = new List<string>();
            if (!string.IsNullOrEmpty(idCsv))
                foreach (var id in idCsv.Split(','))
                    if (!string.IsNullOrEmpty(id)) ids.Add(id);
            rounds.LoadIds(ids, count > 0 ? count : AppConfig.DefaultRounds, duration > 0f ? duration : AppConfig.DefaultRoundDuration);
            if (!string.IsNullOrEmpty(pendingRound))
            {
                var queued = pendingRound;
                pendingRound = null;
                AcceptRound(queued);
            }
        }

        void AcceptRound(string payload)
        {
            if (rounds.LoadedPuzzleCount == 0)
            {
                pendingRound = payload;
                return;
            }
            var f = payload.Split('\u001d');
            if (f.Length < 4 || !int.TryParse(f[0], out var index)) return;
            float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var remaining);
            float.TryParse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var duration);
            rounds.RemoteBeginRound(index, f[1], remaining, duration);
        }

        void OnHostRoundStarted()
        {
            if (!IsHost) return;
            session.Broadcast(FusionSession.Pack("round", RoundPayload()));
        }

        string RoundPayload()
        {
            var puzzle = rounds.CurrentPuzzle;
            return string.Join("\u001d", new[]
            {
                rounds.RoundIndex.ToString(),
                puzzle ? puzzle.PuzzleId : "",
                rounds.RemainingTime.ToString("0.00", CultureInfo.InvariantCulture),
                rounds.RoundDuration.ToString("0.00", CultureInfo.InvariantCulture),
            });
        }

        void OnHostRoundEnded(PuzzleDefinition puzzle)
        {
            if (!IsHost) return;
            session.Broadcast(FusionSession.Pack("result", rounds.ResultTimeRemaining.ToString("0.00", CultureInfo.InvariantCulture)));
        }

        void OnHostMatchEnded()
        {
            if (!IsHost) return;
            session.Broadcast(FusionSession.Pack("finished"));
        }

        void OnHostCorrect(MatchPlayer player, int position, int points)
        {
            if (!IsHost || player == null) return;
            session.Broadcast(FusionSession.Pack("ok", player.PlayerId, position.ToString()));
        }

        void OnHostIncorrect(MatchPlayer player, string text)
        {
            if (!IsHost || player == null) return;
            session.Broadcast(FusionSession.Pack("no", player.PlayerId, text));
        }

        void OnDisconnected(string reason)
        {
            if (!ui) return;
            ui.PresentSystem(reason);
            ui.LeaveToMenu();
        }

        Seat Find(PlayerRef player)
        {
            foreach (var seat in seats)
                if (seat.Ref == player) return seat;
            return null;
        }

        public bool ShouldAnnounceLobby => IsHost && !suppress && !SuppressBroadcast;
    }
}
