using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrickyPictures
{
    /// <summary>Used by player cards, lobby slots, score rows and podium places. Every field is optional.</summary>
    public class PlayerCardView : MonoBehaviour
    {
        [SerializeField] TMP_Text rankText;
        [SerializeField] Image avatar;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text valueText;
        [SerializeField] GameObject hostBadge;
        [SerializeField] GameObject guessedIcon;

        public void Set(int rank, string displayName, string avatarId, string value, bool isHost = false, bool guessed = false)
        {
            if (rankText) rankText.text = rank > 0 ? "#" + rank : "";
            if (avatar) AvatarPortrait.Paint(avatar, avatarId);
            if (nameText) nameText.text = displayName;
            if (valueText) valueText.text = value;
            if (hostBadge) hostBadge.SetActive(isHost);
            if (guessedIcon) guessedIcon.SetActive(guessed);
        }
    }
}
