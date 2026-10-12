namespace TrickyPictures
{
    public enum MatchMode
    {
        Practice,
        PrivateRoom,
        Global,
    }

    /// <summary>Settings chosen in the main menu and read by the Game scene.</summary>
    public static class MatchLaunchSettings
    {
        public static MatchMode Mode { get; private set; } = MatchMode.Practice;
        public static int Rounds { get; private set; } = AppConfig.DefaultRounds;
        public static float RoundDuration { get; private set; } = AppConfig.DefaultRoundDuration;
        public static string RoomCode { get; private set; } = "";
        public static bool IsPrivate { get; private set; }

        public static bool IsOnline => Mode == MatchMode.PrivateRoom || Mode == MatchMode.Global;

        public static void SetPractice()
        {
            Mode = MatchMode.Practice;
            Rounds = AppConfig.DefaultRounds;
            RoundDuration = AppConfig.DefaultRoundDuration;
            RoomCode = "";
            IsPrivate = false;
        }

        public static void SetGlobal(string sessionName)
        {
            Mode = MatchMode.Global;
            Rounds = AppConfig.DefaultRounds;
            RoundDuration = AppConfig.DefaultRoundDuration;
            RoomCode = sessionName ?? "";
            IsPrivate = false;
        }

        public static void SetPrivateRoom(int rounds, float roundDuration, string roomCode, bool isPrivate)
        {
            Mode = MatchMode.PrivateRoom;
            Rounds = rounds;
            RoundDuration = roundDuration;
            RoomCode = roomCode;
            IsPrivate = isPrivate;
        }

        public static void SetMatchRules(int rounds, float roundDuration)
        {
            Rounds = rounds;
            RoundDuration = roundDuration;
        }
    }
}
