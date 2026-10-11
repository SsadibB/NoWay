using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrickyPictures
{
    public class ChatMessageView : MonoBehaviour
    {
        [SerializeField] TMP_Text senderName;
        [SerializeField] Image avatar;
        [SerializeField] TMP_Text messageText;

        public void Set(string sender, string avatarId, string message)
        {
            if (senderName) senderName.text = sender;
            if (avatar) avatar.color = AvatarPalette.Get(avatarId);
            if (messageText)
            {
                messageText.text = message;
                FitHeightToText();
            }
        }

        // Grows the row so long messages wrap instead of overflowing; never shrinks below the template height.
        void FitHeightToText()
        {
            var rt = (RectTransform)transform;
            var textRect = messageText.rectTransform.rect;
            if (textRect.width <= 0f) return;
            float chrome = rt.rect.height - textRect.height;
            float preferred = messageText.GetPreferredValues(messageText.text, textRect.width, 0f).y;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Max(rt.rect.height, preferred + chrome));
        }
    }
}
