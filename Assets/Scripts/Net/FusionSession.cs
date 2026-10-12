using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using Photon.Realtime;
using UnityEngine;

namespace TrickyPictures
{
    /// <summary>
    /// Photon Fusion session for global matchmaking and friend rooms.
    /// Game messages travel as reliable payloads so the match stays host-authoritative.
    /// </summary>
    public class FusionSession : MonoBehaviour, INetworkRunnerCallbacks
    {
        public const char Sep = '\u001f';

        public static FusionSession Instance { get; private set; }

        static readonly ReliableKey MessageKey = ReliableKey.FromInts(0x5450, 1, 0, 0);

        NetworkRunner runner;
        Task pendingShutdown = Task.CompletedTask;
        bool leaving;

        public bool IsOnline => runner && runner.IsRunning;
        public bool IsHost => IsOnline && runner.IsServer;
        public bool JoinsOpen { get; private set; } = true;
        public bool Listed { get; private set; }
        public PlayerRef LocalRef => runner ? runner.LocalPlayer : default;

        public string RoomName
        {
            get
            {
                if (!runner || !runner.IsRunning) return "";
                var info = runner.SessionInfo;
                return info != null && info.IsValid ? info.Name : "";
            }
        }

        public event Action<PlayerRef> PlayerJoined;
        public event Action<PlayerRef> PlayerLeft;
        public event Action<PlayerRef, string> MessageReceived;
        public event Action<string> Disconnected;

        readonly List<(PlayerRef from, string message)> inbox = new List<(PlayerRef, string)>();
        readonly List<PlayerRef> leftInbox = new List<PlayerRef>();

        public struct ConnectResult
        {
            public bool Ok;
            public string Error;
            public string RoomName;
        }

        public static FusionSession Ensure()
        {
            if (Instance) return Instance;
            var go = new GameObject("FusionSession");
            return go.AddComponent<FusionSession>();
        }

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public Task<ConnectResult> ConnectGlobal(CancellationToken token) =>
            Run(new StartGameArgs
            {
                GameMode = GameMode.AutoHostOrClient,
                SessionProperties = new Dictionary<string, SessionProperty> { { "queue", "global" } },
                MatchmakingMode = MatchmakingMode.FillRoom,
                IsOpen = true,
                IsVisible = true,
                PlayerCount = AppConfig.MaxPlayers,
            }, token, listed: true);

        public Task<ConnectResult> CreateRoom(string code, bool isPrivate, CancellationToken token)
        {
            var args = new StartGameArgs
            {
                GameMode = GameMode.Host,
                SessionName = code,
                IsOpen = true,
                IsVisible = !isPrivate,
                PlayerCount = AppConfig.MaxPlayers,
            };
            if (!isPrivate)
                args.SessionProperties = new Dictionary<string, SessionProperty> { { "queue", "global" } };
            return Run(args, token, listed: !isPrivate);
        }

        public Task<ConnectResult> JoinRoom(string code, CancellationToken token) =>
            Run(new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = code,
                EnableClientSessionCreation = false,
                PlayerCount = AppConfig.MaxPlayers,
            }, token, listed: false);

        public void BeginShutdown()
        {
            leaving = true;
            if (!runner) return;
            var current = runner;
            runner = null;
            pendingShutdown = ShutdownQuiet(current);
        }

        public void SetJoinsOpen(bool open)
        {
            JoinsOpen = open;
            if (!IsHost) return;
            var info = runner.SessionInfo;
            if (info == null || !info.IsValid) return;
            var queue = open && Listed ? "global" : "closed";
            info.UpdateCustomProperties(new Dictionary<string, SessionProperty> { { "queue", queue } });
        }

        public void SendToHost(string message)
        {
            if (!IsOnline || string.IsNullOrEmpty(message)) return;
            var bytes = Encode(message);
            if (IsHost) RaiseMessage(runner.LocalPlayer, message);
            else runner.SendReliableDataToServer(MessageKey, bytes);
        }

        public void Broadcast(string message)
        {
            if (!IsHost || string.IsNullOrEmpty(message)) return;
            var bytes = Encode(message);
            foreach (var player in runner.ActivePlayers)
            {
                if (player == runner.LocalPlayer) continue;
                runner.SendReliableDataToPlayer(player, MessageKey, bytes);
            }
        }

        public void SendTo(PlayerRef player, string message)
        {
            if (!IsOnline || string.IsNullOrEmpty(message)) return;
            if (player == runner.LocalPlayer)
            {
                RaiseMessage(player, message);
                return;
            }
            runner.SendReliableDataToPlayer(player, MessageKey, Encode(message));
        }

        public void Flush()
        {
            if (inbox.Count > 0)
            {
                var pending = inbox.ToArray();
                inbox.Clear();
                foreach (var item in pending) MessageReceived?.Invoke(item.from, item.message);
            }
            if (leftInbox.Count > 0)
            {
                var pending = leftInbox.ToArray();
                leftInbox.Clear();
                foreach (var player in pending) PlayerLeft?.Invoke(player);
            }
        }

        public static string Pack(string type, params string[] fields)
        {
            var sb = new StringBuilder(type);
            if (fields == null) return sb.ToString();
            foreach (var field in fields)
            {
                sb.Append(Sep);
                if (string.IsNullOrEmpty(field)) continue;
                foreach (var c in field)
                    if (c != Sep) sb.Append(c);
            }
            return sb.ToString();
        }

        async Task<ConnectResult> Run(StartGameArgs args, CancellationToken token, bool listed)
        {
            if (!HasAppId(out var appError)) return Fail(appError);

            leaving = false;
            JoinsOpen = true;
            Listed = listed;
            try
            {
                if (pendingShutdown != null) await pendingShutdown;
                if (token.IsCancellationRequested) return Fail("Search cancelled.");

                ResetRunner();
                args.StartGameCancellationToken = token;
                args.PlayerCount = AppConfig.MaxPlayers;
                var result = await runner.StartGame(args);
                if (token.IsCancellationRequested || leaving)
                {
                    BeginShutdown();
                    return Fail("Search cancelled.");
                }
                if (!result.Ok)
                {
                    var message = Describe(result.ShutdownReason);
                    if (!string.IsNullOrEmpty(result.ErrorMessage)) message += " " + result.ErrorMessage;
                    Debug.LogWarning("[Fusion] " + message);
                    BeginShutdown();
                    return Fail(message);
                }
                return new ConnectResult { Ok = true, RoomName = RoomName };
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Fusion] " + e.Message);
                BeginShutdown();
                return Fail(e.Message);
            }
        }

        void ResetRunner()
        {
            if (runner)
            {
                var old = runner;
                runner = null;
                Destroy(old);
            }
            runner = gameObject.AddComponent<NetworkRunner>();
            runner.ProvideInput = false;
            runner.AddCallbacks(this);
        }

        static async Task ShutdownQuiet(NetworkRunner current)
        {
            try
            {
                if (current && current.IsRunning) await current.Shutdown();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Fusion] " + e.Message);
            }
            if (current) Destroy(current);
        }

        static bool HasAppId(out string error)
        {
            if (PhotonAppSettings.TryGetGlobal(out var settings) &&
                settings.AppSettings != null &&
                !string.IsNullOrEmpty(settings.AppSettings.AppIdFusion))
            {
                error = null;
                return true;
            }
            error = "Add your Fusion App Id in Assets/Photon/Fusion/Resources/PhotonAppSettings, then try again.";
            return false;
        }

        static ConnectResult Fail(string error) => new ConnectResult { Ok = false, Error = error };

        static byte[] Encode(string message)
        {
            if (message.Length > 800) message = message.Substring(0, 800);
            return Encoding.UTF8.GetBytes(message);
        }

        void RaiseMessage(PlayerRef from, string message)
        {
            if (MessageReceived != null) MessageReceived.Invoke(from, message);
            else inbox.Add((from, message));
        }

        static string Describe(ShutdownReason reason)
        {
            switch (reason)
            {
                case ShutdownReason.Ok: return "Disconnected.";
                case ShutdownReason.GameNotFound: return "Room not found. Check the code and try again.";
                case ShutdownReason.GameIsFull: return "That room is full.";
                case ShutdownReason.GameClosed: return "That room is closed.";
                case ShutdownReason.GameIdAlreadyExists: return "That room code is already in use. Try creating again.";
                case ShutdownReason.InvalidAuthentication: return "Photon rejected the App Id. Check App Id Fusion in PhotonAppSettings.";
                case ShutdownReason.CustomAuthenticationFailed: return "Photon authentication failed.";
                case ShutdownReason.MaxCcuReached: return "The Photon CCU limit has been reached.";
                case ShutdownReason.InvalidRegion: return "Photon could not use the selected region.";
                case ShutdownReason.OperationCanceled: return "Search cancelled.";
                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.PhotonCloudTimeout: return "Timed out connecting to Photon.";
                default: return "Could not connect (" + reason + ").";
            }
        }

        public void OnPlayerJoined(NetworkRunner _, PlayerRef player) => PlayerJoined?.Invoke(player);

        public void OnPlayerLeft(NetworkRunner _, PlayerRef player)
        {
            if (PlayerLeft != null) PlayerLeft.Invoke(player);
            else leftInbox.Add(player);
        }

        public void OnReliableDataReceived(NetworkRunner _, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0 || data.Length > 2000) return;
            RaiseMessage(player, Encoding.UTF8.GetString(data.ToArray()));
        }

        public void OnConnectRequest(NetworkRunner _, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if (JoinsOpen) request.Accept();
            else request.Refuse();
        }

        public void OnShutdown(NetworkRunner which, ShutdownReason reason)
        {
            if (which == runner) runner = null;
            if (leaving || reason == ShutdownReason.Ok) return;
            Disconnected?.Invoke(Describe(reason));
        }

        public void OnDisconnectedFromServer(NetworkRunner _, NetDisconnectReason reason)
        {
            if (leaving) return;
            Disconnected?.Invoke("Disconnected from the host.");
        }

        public void OnConnectedToServer(NetworkRunner _) { }
        public void OnConnectFailed(NetworkRunner _, NetAddress address, NetConnectFailedReason reason) { }
        public void OnInput(NetworkRunner _, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner _, PlayerRef player, NetworkInput input) { }
        public void OnSessionListUpdated(NetworkRunner _, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner _, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner _, HostMigrationToken token) { }
        public void OnSceneLoadDone(NetworkRunner _) { }
        public void OnSceneLoadStart(NetworkRunner _) { }
        public void OnObjectExitAOI(NetworkRunner _, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner _, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataProgress(NetworkRunner _, PlayerRef player, ReliableKey key, float progress) { }
        public void OnUserSimulationMessage(NetworkRunner _, SimulationMessagePtr message) { }
    }
}
