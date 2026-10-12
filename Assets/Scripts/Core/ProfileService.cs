using System.Text.RegularExpressions;
using UnityEngine;

namespace TrickyPictures
{
    public class ProfileService : MonoBehaviour
    {
        const string PlayerIdKey = "tp.playerId";
        const string DisplayNameKey = "tp.displayName";
        const string AvatarIdKey = "tp.avatarId";
        const string AvatarPrefix = "avatar_";

        static PlayerProfileData current;

        public static PlayerProfileData Current => current ??= Load();

        static PlayerProfileData Load()
        {
            var id = PlayerPrefs.GetString(PlayerIdKey, "");
            if (string.IsNullOrEmpty(id))
            {
                id = System.Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(PlayerIdKey, id);
                PlayerPrefs.Save();
            }

            var name = SanitizeName(PlayerPrefs.GetString(DisplayNameKey, AppConfig.DefaultDisplayName), out var error);
            return new PlayerProfileData
            {
                PlayerId = id,
                DisplayName = error == null ? name : AppConfig.DefaultDisplayName,
                AvatarId = AvatarLook.Parse(PlayerPrefs.GetString(AvatarIdKey, "")).ToId(),
            };
        }

        public static void Save(string displayName, AvatarLook look)
        {
            var profile = Current;
            profile.DisplayName = displayName;
            profile.AvatarId = look.ToId();
            PlayerPrefs.SetString(DisplayNameKey, profile.DisplayName);
            PlayerPrefs.SetString(AvatarIdKey, profile.AvatarId);
            PlayerPrefs.Save();
        }

        /// <summary>Returns the cleaned name; <paramref name="error"/> is null when the name is valid.</summary>
        public static string SanitizeName(string raw, out string error)
        {
            var name = Regex.Replace(raw ?? "", @"\s+", " ").Trim();
            name = name.Replace("<", "").Replace(">", "");
            error = null;
            if (name.Length == 0) error = "Name can't be empty";
            else if (name.Length > AppConfig.MaxDisplayNameLength) error = $"Name must be {AppConfig.MaxDisplayNameLength} characters or fewer";
            return name;
        }

        public static string AvatarIdFromIndex(int index) =>
            AvatarPrefix + Mathf.Clamp(index, 0, AvatarPalette.Count - 1);

        public static int AvatarIndex(string avatarId)
        {
            if (!string.IsNullOrEmpty(avatarId) && avatarId.StartsWith(AvatarPrefix) &&
                int.TryParse(avatarId.Substring(AvatarPrefix.Length), out var index))
                return Mathf.Clamp(index, 0, AvatarPalette.Count - 1);
            return 0;
        }
    }
}
