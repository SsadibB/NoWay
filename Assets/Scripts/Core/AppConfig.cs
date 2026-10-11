namespace TrickyPictures
{
    public static class AppConfig
    {
        public const string BootstrapScene = "Bootstrap";
        public const string MainMenuScene = "MainMenu";
        public const string GameScene = "Game";

        public const int MaxPlayers = 8;
        public const int MinPublicPlayers = 2;
        public const int DefaultRounds = 10;
        public const float DefaultRoundDuration = 30f;
        public const float BetweenRoundPause = 4f;
        public const float MatchmakingTimeout = 15f;

        public const int MaxMessageLength = 160;
        public const int MaxDisplayNameLength = 20;
        public const string DefaultDisplayName = "Player";

        public const int ChatRateLimitCount = 4;
        public const float ChatRateLimitWindow = 5f;
        public const int GuessRateLimitCount = 5;
        public const float GuessRateLimitWindow = 1f;

        public const int RoomCodeLength = 6;
        public const string RoomCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static readonly int[] PointsByPosition = { 100, 80, 60, 40, 20 };

        // Must match the option order of the Rounds / Round Duration dropdowns.
        public static readonly int[] RoundOptions = { 5, 10, 15, 20 };
        public static readonly int[] DurationOptions = { 20, 30, 45, 60, 90 };
    }
}
