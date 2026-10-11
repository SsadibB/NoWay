using System.Collections.Generic;
using UnityEngine;

namespace TrickyPictures
{
    public class ScoreManager
    {
        class Entry
        {
            public int Total;
            public int Round;
            public int CorrectGuesses;
            public int FirstPlaceGuesses;
        }

        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

        public static int PointsForPosition(int position)
        {
            if (position <= 0) return 0;
            var table = AppConfig.PointsByPosition;
            return table[Mathf.Min(position, table.Length) - 1];
        }

        public void Register(string playerId)
        {
            if (!entries.ContainsKey(playerId)) entries[playerId] = new Entry();
        }

        public void ResetAll()
        {
            foreach (var e in entries.Values)
            {
                e.Total = 0;
                e.Round = 0;
                e.CorrectGuesses = 0;
                e.FirstPlaceGuesses = 0;
            }
        }

        public void BeginRound()
        {
            foreach (var e in entries.Values) e.Round = 0;
        }

        public int AwardCorrectGuess(string playerId, int position)
        {
            Register(playerId);
            var e = entries[playerId];
            int points = PointsForPosition(position);
            e.Total += points;
            e.Round += points;
            e.CorrectGuesses++;
            if (position == 1) e.FirstPlaceGuesses++;
            return points;
        }

        public int GetTotal(string playerId) => entries.TryGetValue(playerId, out var e) ? e.Total : 0;
        public int GetRoundPoints(string playerId) => entries.TryGetValue(playerId, out var e) ? e.Round : 0;
        public int GetCorrectGuesses(string playerId) => entries.TryGetValue(playerId, out var e) ? e.CorrectGuesses : 0;
        public int GetFirstPlaceGuesses(string playerId) => entries.TryGetValue(playerId, out var e) ? e.FirstPlaceGuesses : 0;
    }
}
