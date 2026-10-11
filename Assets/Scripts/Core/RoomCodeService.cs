using System.Text;
using UnityEngine;

namespace TrickyPictures
{
    /// <summary>
    /// Six-character codes without I, O, 0 or 1. A code is a friendly locator only;
    /// uniqueness and session mapping must come from a trusted service once networking exists.
    /// </summary>
    public static class RoomCodeService
    {
        public static string Generate()
        {
            var sb = new StringBuilder(AppConfig.RoomCodeLength);
            for (int i = 0; i < AppConfig.RoomCodeLength; i++)
                sb.Append(AppConfig.RoomCodeAlphabet[Random.Range(0, AppConfig.RoomCodeAlphabet.Length)]);
            return sb.ToString();
        }

        public static string Normalize(string code) => (code ?? "").Trim().ToUpperInvariant();

        public static bool IsValidFormat(string code)
        {
            if (code == null || code.Length != AppConfig.RoomCodeLength) return false;
            foreach (var c in code)
                if (AppConfig.RoomCodeAlphabet.IndexOf(c) < 0) return false;
            return true;
        }
    }
}
