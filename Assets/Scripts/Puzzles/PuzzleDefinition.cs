using UnityEngine;

namespace TrickyPictures
{
    public enum PuzzleDifficulty
    {
        Easy,
        Medium,
        Hard,
    }

    [CreateAssetMenu(menuName = "Tricky Pictures/Puzzle Definition", fileName = "Puzzle")]
    public class PuzzleDefinition : ScriptableObject
    {
        public string PuzzleId;

        [Tooltip("Rebus picture. When empty, Rebus Text is shown instead.")]
        public Sprite Image;

        [Tooltip("TextMeshPro rich text shown when no Image is assigned.")]
        [TextArea(3, 10)] public string RebusText;

        public string CanonicalAnswer;
        public string[] AcceptedAnswers = new string[0];
        public PuzzleDifficulty Difficulty = PuzzleDifficulty.Easy;
        public string Category = "General";
        public string Language = "en";
        public bool IsActive = true;
    }
}
