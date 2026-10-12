using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickyPictures
{
    public enum MatchPhase
    {
        Lobby,
        Playing,
        RoundResult,
        Finished,
    }

    public enum GuessResult
    {
        Correct,
        Incorrect,
        Empty,
        NotPlaying,
        WrongRound,
        NotParticipant,
        AlreadySolved,
        RateLimited,
    }

    public class MatchPlayer
    {
        public readonly string PlayerId;
        public string DisplayName;
        public string AvatarId;
        public bool IsHost;
        public bool IsReady;
        public bool IsConnected = true;

        public MatchPlayer(string playerId, string displayName, string avatarId, bool isHost)
        {
            PlayerId = playerId;
            DisplayName = displayName;
            AvatarId = avatarId;
            IsHost = isHost;
        }
    }

    /// <summary>
    /// Authoritative round lifecycle for a local (offline) match. All scoring decisions happen here;
    /// UI only submits requests and reacts to events.
    /// </summary>
    public class RoundManager : MonoBehaviour
    {
        [SerializeField] PuzzleDatabase puzzleDatabase;
        [SerializeField] float betweenRoundPause = AppConfig.BetweenRoundPause;

        public event Action<MatchPhase> PhaseChanged;
        public event Action RosterChanged;
        public event Action RoundStarted;
        public event Action<MatchPlayer, int, int> CorrectGuess;
        public event Action<MatchPlayer, string> IncorrectGuess;
        public event Action<PuzzleDefinition> RoundEnded;
        public event Action MatchEnded;
        public event Action ScoresChanged;

        readonly List<MatchPlayer> players = new List<MatchPlayer>();
        readonly HashSet<string> solvedThisRound = new HashSet<string>();
        readonly Dictionary<string, RateLimiter> guessLimiters = new Dictionary<string, RateLimiter>();
        readonly ScoreManager scores = new ScoreManager();
        List<PuzzleDefinition> matchPuzzles = new List<PuzzleDefinition>();
        int correctGuessCount;

        public MatchPhase Phase { get; private set; } = MatchPhase.Lobby;
        public int RoundIndex { get; private set; } = -1;
        public int TotalRounds { get; private set; } = AppConfig.DefaultRounds;
        public float RoundDuration { get; private set; } = AppConfig.DefaultRoundDuration;
        public float RemainingTime { get; private set; }
        public float ResultTimeRemaining { get; private set; }
        public bool Paused { get; set; }
        public PuzzleDefinition CurrentPuzzle { get; private set; }
        public IReadOnlyList<MatchPlayer> Players => players;
        public ScoreManager Scores => scores;
        public bool IsLastRound => RoundIndex >= TotalRounds - 1;
        public bool IsMatchRunning => Phase == MatchPhase.Playing || Phase == MatchPhase.RoundResult;
        public bool RemoteDriven { get; set; }
        public int LoadedPuzzleCount => matchPuzzles.Count;

        public bool HasSolved(string playerId) => solvedThisRound.Contains(playerId);

        public bool Configure(int rounds, float roundDuration)
        {
            if (IsMatchRunning) return false;
            TotalRounds = Mathf.Max(1, rounds);
            RoundDuration = Mathf.Max(5f, roundDuration);
            return true;
        }

        public bool AddPlayer(MatchPlayer player)
        {
            if (players.Count >= AppConfig.MaxPlayers || GetPlayer(player.PlayerId) != null) return false;
            players.Add(player);
            scores.Register(player.PlayerId);
            ScoresChanged?.Invoke();
            return true;
        }

        public MatchPlayer GetPlayer(string playerId)
        {
            foreach (var p in players)
                if (p.PlayerId == playerId) return p;
            return null;
        }

        public List<string> DrawPuzzleIds()
        {
            var ids = new List<string>();
            if (!puzzleDatabase) return ids;
            foreach (var puzzle in puzzleDatabase.PickForMatch(TotalRounds))
                if (puzzle) ids.Add(puzzle.PuzzleId);
            return ids;
        }

        public bool LoadIds(IReadOnlyList<string> ids, int rounds, float duration)
        {
            if (Phase == MatchPhase.Playing) return false;
            TotalRounds = Mathf.Max(1, rounds);
            RoundDuration = Mathf.Max(5f, duration);
            matchPuzzles = new List<PuzzleDefinition>();
            if (puzzleDatabase != null && ids != null)
            {
                foreach (var id in ids)
                {
                    foreach (var puzzle in puzzleDatabase.Puzzles)
                    {
                        if (puzzle && puzzle.PuzzleId == id)
                        {
                            matchPuzzles.Add(puzzle);
                            break;
                        }
                    }
                }
            }
            scores.ResetAll();
            RoundIndex = -1;
            CurrentPuzzle = null;
            solvedThisRound.Clear();
            correctGuessCount = 0;
            Paused = false;
            return matchPuzzles.Count > 0;
        }

        public bool StartMatch()
        {
            if (puzzleDatabase == null)
            {
                Debug.LogError("[RoundManager] No PuzzleDatabase assigned.", this);
                return false;
            }
            var ids = DrawPuzzleIds();
            if (!LoadIds(ids, TotalRounds, RoundDuration))
            {
                Debug.LogError("[RoundManager] PuzzleDatabase has no active puzzles.", this);
                return false;
            }
            Kickoff();
            return true;
        }

        public void Kickoff()
        {
            RoundIndex = -1;
            BeginNextRound();
        }

        public void ApplyRoster(List<MatchPlayer> roster)
        {
            players.Clear();
            if (roster != null) players.AddRange(roster);
            foreach (var player in players) scores.Register(player.PlayerId);
            RosterChanged?.Invoke();
            ScoresChanged?.Invoke();
        }

        public void RemoteBeginRound(int index, string puzzleId, float remaining, float duration)
        {
            if (duration > 0f) RoundDuration = duration;
            if (Phase == MatchPhase.Playing && RoundIndex == index)
            {
                RemainingTime = remaining;
                return;
            }

            RoundIndex = index;
            CurrentPuzzle = FindPuzzle(puzzleId);
            if (CurrentPuzzle == null && matchPuzzles.Count > 0)
                CurrentPuzzle = matchPuzzles[Mathf.Abs(index) % matchPuzzles.Count];
            solvedThisRound.Clear();
            correctGuessCount = 0;
            scores.BeginRound();
            RemainingTime = remaining > 0f ? remaining : RoundDuration;
            SetPhase(MatchPhase.Playing);
            RoundStarted?.Invoke();
            ScoresChanged?.Invoke();
        }

        public void RemoteEnterResult(float pause)
        {
            ResultTimeRemaining = Mathf.Max(0.5f, pause);
            SetPhase(MatchPhase.RoundResult);
            RoundEnded?.Invoke(CurrentPuzzle);
        }

        public void RemoteFinish()
        {
            SetPhase(MatchPhase.Finished);
            MatchEnded?.Invoke();
        }

        public void RemoteSetClock(float remaining, float resultRemaining)
        {
            if (Phase == MatchPhase.Playing) RemainingTime = Mathf.Max(0f, remaining);
            else if (Phase == MatchPhase.RoundResult) ResultTimeRemaining = Mathf.Max(0f, resultRemaining);
        }

        public void RemoteApplyCorrect(string playerId, int position)
        {
            if (Phase != MatchPhase.Playing || solvedThisRound.Contains(playerId)) return;
            var player = GetPlayer(playerId);
            if (player == null) return;
            correctGuessCount = Mathf.Max(correctGuessCount, position);
            int points = scores.AwardCorrectGuess(playerId, position);
            solvedThisRound.Add(playerId);
            CorrectGuess?.Invoke(player, position, points);
            ScoresChanged?.Invoke();
        }

        public void RemoteApplyIncorrect(string playerId, string text)
        {
            var player = GetPlayer(playerId);
            if (player == null) return;
            IncorrectGuess?.Invoke(player, text);
        }

        public bool HostEndRoundIfComplete()
        {
            if (RemoteDriven || Phase != MatchPhase.Playing || !AllEligiblePlayersSolved()) return false;
            EndRound();
            return true;
        }

        public GuessResult SubmitGuess(string playerId, int roundIndex, string text)
        {
            if (Phase != MatchPhase.Playing) return GuessResult.NotPlaying;
            if (roundIndex != RoundIndex) return GuessResult.WrongRound;
            var player = GetPlayer(playerId);
            if (player == null) return GuessResult.NotParticipant;
            if (solvedThisRound.Contains(playerId)) return GuessResult.AlreadySolved;
            if (!GetGuessLimiter(playerId).TryConsume()) return GuessResult.RateLimited;
            if (string.IsNullOrWhiteSpace(text)) return GuessResult.Empty;

            if (!GuessProcessor.IsCorrect(text, CurrentPuzzle))
            {
                IncorrectGuess?.Invoke(player, text);
                return GuessResult.Incorrect;
            }

            correctGuessCount++;
            int points = scores.AwardCorrectGuess(playerId, correctGuessCount);
            solvedThisRound.Add(playerId);
            CorrectGuess?.Invoke(player, correctGuessCount, points);
            ScoresChanged?.Invoke();

            if (AllEligiblePlayersSolved()) EndRound();
            return GuessResult.Correct;
        }

        public void ReturnToLobby()
        {
            RoundIndex = -1;
            CurrentPuzzle = null;
            Paused = false;
            SetPhase(MatchPhase.Lobby);
        }

        public List<MatchPlayer> GetStandings()
        {
            var list = new List<MatchPlayer>(players);
            list.Sort((a, b) =>
            {
                int c = scores.GetTotal(b.PlayerId).CompareTo(scores.GetTotal(a.PlayerId));
                if (c != 0) return c;
                c = scores.GetFirstPlaceGuesses(b.PlayerId).CompareTo(scores.GetFirstPlaceGuesses(a.PlayerId));
                if (c != 0) return c;
                c = scores.GetCorrectGuesses(b.PlayerId).CompareTo(scores.GetCorrectGuesses(a.PlayerId));
                if (c != 0) return c;
                c = string.CompareOrdinal(a.DisplayName, b.DisplayName);
                return c != 0 ? c : string.CompareOrdinal(a.PlayerId, b.PlayerId);
            });
            return list;
        }

        void Update()
        {
            if (Paused) return;

            if (RemoteDriven)
            {
                if (Phase == MatchPhase.Playing) RemainingTime = Mathf.Max(0f, RemainingTime - Time.deltaTime);
                else if (Phase == MatchPhase.RoundResult) ResultTimeRemaining = Mathf.Max(0f, ResultTimeRemaining - Time.deltaTime);
                return;
            }

            if (Phase == MatchPhase.Playing)
            {
                RemainingTime = Mathf.Max(0f, RemainingTime - Time.deltaTime);
                if (RemainingTime <= 0f) EndRound();
            }
            else if (Phase == MatchPhase.RoundResult)
            {
                ResultTimeRemaining -= Time.deltaTime;
                if (ResultTimeRemaining <= 0f) BeginNextRound();
            }
        }

        void BeginNextRound()
        {
            RoundIndex++;
            if (RoundIndex >= TotalRounds)
            {
                SetPhase(MatchPhase.Finished);
                MatchEnded?.Invoke();
                return;
            }

            CurrentPuzzle = matchPuzzles[RoundIndex % matchPuzzles.Count];
            solvedThisRound.Clear();
            correctGuessCount = 0;
            scores.BeginRound();
            RemainingTime = RoundDuration;
            SetPhase(MatchPhase.Playing);
            RoundStarted?.Invoke();
            ScoresChanged?.Invoke();
        }

        void EndRound()
        {
            ResultTimeRemaining = betweenRoundPause;
            SetPhase(MatchPhase.RoundResult);
            RoundEnded?.Invoke(CurrentPuzzle);
        }

        bool AllEligiblePlayersSolved()
        {
            foreach (var p in players)
                if (p.IsConnected && !solvedThisRound.Contains(p.PlayerId)) return false;
            return true;
        }

        RateLimiter GetGuessLimiter(string playerId)
        {
            if (!guessLimiters.TryGetValue(playerId, out var limiter))
            {
                limiter = new RateLimiter(AppConfig.GuessRateLimitCount, AppConfig.GuessRateLimitWindow);
                guessLimiters[playerId] = limiter;
            }
            return limiter;
        }

        PuzzleDefinition FindPuzzle(string puzzleId)
        {
            foreach (var puzzle in matchPuzzles)
                if (puzzle && puzzle.PuzzleId == puzzleId) return puzzle;
            if (puzzleDatabase == null) return null;
            foreach (var puzzle in puzzleDatabase.Puzzles)
                if (puzzle && puzzle.PuzzleId == puzzleId) return puzzle;
            return null;
        }

        void SetPhase(MatchPhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
