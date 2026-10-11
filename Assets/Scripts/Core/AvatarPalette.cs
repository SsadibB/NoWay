using UnityEngine;

namespace TrickyPictures
{
    public static class AvatarPalette
    {
        static readonly Color32[] Colors =
        {
            new Color32(255, 107, 107, 255), new Color32(255, 201, 60, 255), new Color32(46, 204, 113, 255), new Color32(52, 152, 219, 255),
            new Color32(155, 89, 182, 255), new Color32(26, 188, 156, 255), new Color32(230, 126, 34, 255), new Color32(236, 240, 241, 255),
        };

        public static int Count => Colors.Length;

        public static Color Get(int index) => Colors[Mathf.Clamp(index, 0, Colors.Length - 1)];

        public static Color Get(string avatarId) => Get(ProfileService.AvatarIndex(avatarId));
    }
}
