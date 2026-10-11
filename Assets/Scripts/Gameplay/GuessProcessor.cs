using System.Text;

namespace TrickyPictures
{
    public static class GuessProcessor
    {
        /// <summary>Lower-cases, turns punctuation into spaces and collapses whitespace.</summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length);
            bool pendingSpace = false;
            foreach (var raw in text.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(raw))
                {
                    if (pendingSpace && sb.Length > 0) sb.Append(' ');
                    pendingSpace = false;
                    sb.Append(raw);
                }
                else
                {
                    pendingSpace = true;
                }
            }
            return sb.ToString();
        }

        public static bool IsCorrect(string guess, PuzzleDefinition puzzle)
        {
            if (puzzle == null) return false;
            var normalized = Normalize(guess);
            if (normalized.Length == 0) return false;
            if (normalized == Normalize(puzzle.CanonicalAnswer)) return true;
            if (puzzle.AcceptedAnswers == null) return false;
            foreach (var alt in puzzle.AcceptedAnswers)
                if (normalized == Normalize(alt)) return true;
            return false;
        }
    }
}
