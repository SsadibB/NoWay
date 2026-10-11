using System.Collections.Generic;
using UnityEngine;

namespace TrickyPictures
{
    [CreateAssetMenu(menuName = "Tricky Pictures/Puzzle Database", fileName = "PuzzleDatabase")]
    public class PuzzleDatabase : ScriptableObject
    {
        public List<PuzzleDefinition> Puzzles = new List<PuzzleDefinition>();

        public List<PuzzleDefinition> GetActivePuzzles(string language = "en")
        {
            var result = new List<PuzzleDefinition>();
            foreach (var p in Puzzles)
                if (p != null && p.IsActive && p.Language == language)
                    result.Add(p);
            return result;
        }

        /// <summary>Picks puzzles without repeats until the active pool is exhausted.</summary>
        public List<PuzzleDefinition> PickForMatch(int count, string language = "en")
        {
            var active = GetActivePuzzles(language);
            var result = new List<PuzzleDefinition>(count);
            if (active.Count == 0) return result;

            var pool = new List<PuzzleDefinition>();
            while (result.Count < count)
            {
                if (pool.Count == 0)
                {
                    pool.AddRange(active);
                    Shuffle(pool);
                    if (result.Count > 0 && pool.Count > 1 && pool[0] == result[result.Count - 1])
                        (pool[0], pool[pool.Count - 1]) = (pool[pool.Count - 1], pool[0]);
                }
                result.Add(pool[0]);
                pool.RemoveAt(0);
            }
            return result;
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
