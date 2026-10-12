using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrickyPictures
{
    /// <summary>Runtime face, body, eyes, brow, smile and lip controls for the profile window.</summary>
    public class AvatarPartPicker : MonoBehaviour
    {
        public event Action<AvatarLook> Changed;

        AvatarLook look;
        TMP_Text[] valueLabels;
        readonly Color32 buttonColor = new Color32(55, 68, 120, 255);
        readonly Color32 labelColor = new Color32(232, 236, 255, 255);

        public AvatarLook Look => look;

        public void Build(Transform window, TMP_FontAsset font)
        {
            var root = CreateRect("PartPicker", window);
            root.anchoredPosition = new Vector2(190f, 125f);
            root.sizeDelta = new Vector2(540f, 290f);

            valueLabels = new TMP_Text[AvatarLook.PartNames.Length];
            for (int i = 0; i < AvatarLook.PartNames.Length; i++)
            {
                var part = (AvatarPart)i;
                var row = CreateRect(part + "Row", root);
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.sizeDelta = new Vector2(0f, 36f);
                row.anchoredPosition = new Vector2(0f, -4f - i * 40f);

                var name = CreateText(AvatarLook.PartNames[i], row, font, 22, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                name.rectTransform.anchorMin = new Vector2(0f, 0f);
                name.rectTransform.anchorMax = new Vector2(0f, 1f);
                name.rectTransform.pivot = new Vector2(0f, 0.5f);
                name.rectTransform.anchoredPosition = new Vector2(8f, 0f);
                name.rectTransform.sizeDelta = new Vector2(110f, 0f);
                name.color = new Color32(255, 201, 90, 255);

                var prev = CreateButton("<", row, font, () => Step(part, -1));
                var prevRt = (RectTransform)prev.transform;
                prevRt.anchorMin = prevRt.anchorMax = new Vector2(0f, 0.5f);
                prevRt.anchoredPosition = new Vector2(150f, 0f);
                prevRt.sizeDelta = new Vector2(44f, 36f);

                valueLabels[i] = CreateText(AvatarLook.OptionNames[i][0], row, font, 20, FontStyles.Normal, TextAlignmentOptions.Center);
                var valueRt = valueLabels[i].rectTransform;
                valueRt.anchorMin = valueRt.anchorMax = new Vector2(0f, 0.5f);
                valueRt.anchoredPosition = new Vector2(292f, 0f);
                valueRt.sizeDelta = new Vector2(200f, 36f);

                var next = CreateButton(">", row, font, () => Step(part, 1));
                var nextRt = (RectTransform)next.transform;
                nextRt.anchorMin = nextRt.anchorMax = new Vector2(0f, 0.5f);
                nextRt.anchoredPosition = new Vector2(430f, 0f);
                nextRt.sizeDelta = new Vector2(44f, 36f);
            }

            var mix = CreateButton("Mix", root, font, Randomize);
            var mixRt = (RectTransform)mix.transform;
            mixRt.anchorMin = mixRt.anchorMax = new Vector2(1f, 0f);
            mixRt.pivot = new Vector2(1f, 0f);
            mixRt.anchoredPosition = new Vector2(-8f, 6f);
            mixRt.sizeDelta = new Vector2(120f, 32f);
        }

        public void SetLook(AvatarLook value, bool notify = false)
        {
            look = value;
            RefreshLabels();
            if (notify) Changed?.Invoke(look);
        }

        void Step(AvatarPart part, int delta)
        {
            int next = look.Get(part) + delta;
            if (next < 0) next = AvatarLook.OptionCount - 1;
            if (next >= AvatarLook.OptionCount) next = 0;
            look.Set(part, next);
            RefreshLabels();
            Changed?.Invoke(look);
        }

        void Randomize()
        {
            look = new AvatarLook(
                UnityEngine.Random.Range(0, AvatarLook.OptionCount),
                UnityEngine.Random.Range(0, AvatarLook.OptionCount),
                UnityEngine.Random.Range(0, AvatarLook.OptionCount),
                UnityEngine.Random.Range(0, AvatarLook.OptionCount),
                UnityEngine.Random.Range(0, AvatarLook.OptionCount),
                UnityEngine.Random.Range(0, AvatarLook.OptionCount));
            RefreshLabels();
            Changed?.Invoke(look);
        }

        void RefreshLabels()
        {
            if (valueLabels == null) return;
            for (int i = 0; i < valueLabels.Length; i++)
                if (valueLabels[i]) valueLabels[i].text = look.OptionLabel((AvatarPart)i);
        }

        Button CreateButton(string label, Transform parent, TMP_FontAsset font, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = BuiltinSprite();
            image.type = Image.Type.Sliced;
            image.color = buttonColor;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            var text = CreateText(label, go.transform, font, 22, FontStyles.Bold, TextAlignmentOptions.Center);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.color = labelColor;
            return button;
        }

        TMP_Text CreateText(string value, Transform parent, TMP_FontAsset font, float size, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font) text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = align;
            text.color = labelColor;
            text.raycastTarget = false;
            return text;
        }

        static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        static Sprite BuiltinSprite()
        {
            var sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            return sprite ? sprite : null;
        }
    }
}
