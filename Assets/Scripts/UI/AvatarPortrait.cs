using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TrickyPictures
{
    /// <summary>Draws a flat avatar from body, face, eyes, brow, smile and lip choices.</summary>
    public static class AvatarPortrait
    {
        const int Size = 128;
        const int CacheLimit = 48;

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Queue<string> order = new Queue<string>();

        static readonly Color32 Backdrop = new Color32(36, 44, 82, 255);
        static readonly Color32 Outline = new Color32(28, 32, 58, 255);
        static readonly Color32 EyeWhite = new Color32(248, 250, 255, 255);
        static readonly Color32 Pupil = new Color32(36, 42, 64, 255);
        static readonly Color32 BrowColor = new Color32(62, 42, 36, 255);
        static readonly Color32 Mouth = new Color32(92, 36, 48, 255);
        static readonly Color32 Teeth = new Color32(255, 250, 245, 255);
        static readonly Color32 Tongue = new Color32(214, 96, 112, 255);

        static readonly Color32[] Shirts =
        {
            new Color32(255, 107, 107, 255), new Color32(52, 152, 219, 255), new Color32(46, 204, 113, 255),
            new Color32(241, 196, 15, 255), new Color32(155, 89, 182, 255), new Color32(230, 126, 34, 255),
        };

        static readonly Color32[] Skins =
        {
            new Color32(253, 220, 186, 255), new Color32(241, 194, 125, 255), new Color32(224, 172, 105, 255),
            new Color32(198, 134, 66, 255), new Color32(141, 85, 36, 255), new Color32(92, 51, 23, 255),
        };

        static readonly Color32[] LipColors =
        {
            new Color32(232, 160, 160, 255), new Color32(214, 69, 69, 255), new Color32(245, 166, 196, 255),
            new Color32(194, 59, 110, 255), new Color32(201, 149, 122, 255), new Color32(142, 58, 89, 255),
        };

        public static void Paint(Image slot, string avatarId)
        {
            if (!slot) return;
            var child = slot.transform.Find("AvatarFace");
            Image face;
            if (!child)
            {
                var go = new GameObject("AvatarFace", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(slot.transform, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(3f, 3f);
                rt.offsetMax = new Vector2(-3f, -3f);
                rt.SetAsFirstSibling();
                face = go.GetComponent<Image>();
                face.raycastTarget = false;
            }
            else face = child.GetComponent<Image>();

            var look = AvatarLook.Parse(avatarId);
            face.sprite = GetSprite(look);
            face.color = Color.white;
            face.preserveAspect = true;
            face.enabled = true;
        }

        public static Sprite GetSprite(AvatarLook look)
        {
            var key = look.ToId();
            if (cache.TryGetValue(key, out var cached) && cached) return cached;

            var pix = new Pix(Size, Size);
            pix.Clear(Backdrop);
            Draw(pix, look);
            var sprite = pix.ToSprite();
            cache[key] = sprite;
            order.Enqueue(key);
            while (order.Count > CacheLimit)
            {
                var old = order.Dequeue();
                if (old == key || !cache.TryGetValue(old, out var stale)) continue;
                cache.Remove(old);
                if (stale)
                {
                    var tex = stale.texture;
                    Object.Destroy(stale);
                    if (tex) Object.Destroy(tex);
                }
            }
            return sprite;
        }

        static void Draw(Pix pix, AvatarLook look)
        {
            var shirt = Shirts[look.Body];
            var skin = Skins[look.Face];
            var lip = LipColors[look.Lips];
            var shade = Darken(skin, 0.82f);

            if (look.Body == 4)
                pix.FillEllipse(64, 86, 40, 36, shirt);

            switch (look.Body)
            {
                case 1:
                    pix.FillRoundRect(40, 8, 48, 58, 16, shirt);
                    break;
                case 2:
                    pix.FillTriangle(28, 10, 100, 10, 64, 72, shirt);
                    break;
                case 3:
                    pix.FillRoundRect(14, 18, 100, 48, 18, shirt);
                    pix.FillEllipse(64, 40, 36, 26, shirt);
                    break;
                case 5:
                    pix.FillRoundRect(30, 8, 68, 58, 18, shirt);
                    pix.FillRoundRect(44, 40, 10, 36, 4, Darken(shirt, 0.75f));
                    pix.FillRoundRect(74, 40, 10, 36, 4, Darken(shirt, 0.75f));
                    break;
                default:
                    pix.FillEllipse(64, 34, look.Body == 0 ? 42 : 38, 30, shirt);
                    break;
            }

            pix.FillRoundRect(54, 48, 20, 22, 8, shade);

            switch (look.Face)
            {
                case 1:
                    pix.FillEllipse(64, 80, 26, 36, skin);
                    break;
                case 2:
                    pix.FillEllipse(64, 84, 32, 28, skin);
                    pix.FillTriangle(40, 78, 88, 78, 64, 52, skin);
                    break;
                case 3:
                    pix.FillRoundRect(34, 56, 60, 56, 14, skin);
                    break;
                case 4:
                    pix.FillEllipse(64, 80, 24, 38, skin);
                    break;
                case 5:
                    pix.FillEllipse(64, 80, 38, 28, skin);
                    break;
                default:
                    pix.FillEllipse(64, 80, 30, 32, skin);
                    break;
            }

            pix.FillEllipse(34, 78, 7, 9, skin);
            pix.FillEllipse(94, 78, 7, 9, skin);
            pix.FillEllipse(64, 74, 4, 5, shade);

            DrawMouth(pix, look, lip);
            DrawEyes(pix, look);
            DrawBrows(pix, look);
        }

        static void DrawEyes(Pix pix, AvatarLook look)
        {
            float y = 86f;
            float span = look.Eyes == 2 ? 16f : 14f;
            DrawEye(pix, 64f - span, y, look.Eyes, true);
            DrawEye(pix, 64f + span, y, look.Eyes, false);
        }

        static void DrawEye(Pix pix, float x, float y, int style, bool left)
        {
            switch (style)
            {
                case 1:
                    pix.StrokeArc(x, y - 2f, 9f, 6f, 2.4f, Pupil, upper: true);
                    break;
                case 2:
                    pix.FillEllipse(x, y, 12, 11, EyeWhite);
                    pix.FillEllipse(x, y, 12, 11, Outline, ring: 1.4f);
                    pix.FillCircle(x, y, 3.2f, Pupil);
                    pix.FillCircle(x - 2f, y + 2f, 1.3f, EyeWhite);
                    break;
                case 3:
                    pix.FillEllipse(x, y - 1f, 10, 5, EyeWhite);
                    pix.FillCircle(x, y - 1f, 3f, Pupil);
                    pix.FillRoundRect(x - 11f, y + 2f, 22, 4, 2, skinShade());
                    break;
                case 4:
                    if (left)
                    {
                        pix.FillEllipse(x, y, 10, 8, EyeWhite);
                        pix.FillCircle(x, y, 3.6f, Pupil);
                        pix.FillCircle(x - 2f, y + 2f, 1.4f, EyeWhite);
                    }
                    else pix.StrokeArc(x, y, 9f, 5f, 2.2f, Pupil, upper: true);
                    break;
                case 5:
                    pix.FillEllipse(x, y, 8, 7, EyeWhite);
                    pix.FillCircle(x - 2.2f, y + 1.5f, 2.6f, new Color32(231, 76, 96, 255));
                    pix.FillCircle(x + 2.2f, y + 1.5f, 2.6f, new Color32(231, 76, 96, 255));
                    pix.FillTriangle(x - 4.2f, y + 1f, x + 4.2f, y + 1f, x, y - 4.5f, new Color32(231, 76, 96, 255));
                    break;
                default:
                    pix.FillEllipse(x, y, 10, 8, EyeWhite);
                    pix.FillCircle(x + 0.5f, y - 0.5f, 3.8f, Pupil);
                    pix.FillCircle(x - 1.6f, y + 1.8f, 1.5f, EyeWhite);
                    break;
            }
        }

        static Color32 skinShade() => new Color32(120, 78, 58, 255);

        static void DrawBrows(Pix pix, AvatarLook look)
        {
            float y = look.Brow == 1 ? 102f : 99f;
            switch (look.Brow)
            {
                case 1:
                    Brow(pix, 50, y, 12, -18f);
                    Brow(pix, 78, y, 12, 18f);
                    break;
                case 2:
                    Brow(pix, 50, y - 1f, 13, 24f);
                    Brow(pix, 78, y - 1f, 13, -24f);
                    break;
                case 3:
                    Brow(pix, 50, y, 12, -28f);
                    Brow(pix, 78, y, 12, 28f);
                    break;
                case 4:
                    pix.FillRoundRect(38, y - 2f, 22, 5, 2, BrowColor);
                    pix.FillRoundRect(68, y - 2f, 22, 5, 2, BrowColor);
                    break;
                case 5:
                    Brow(pix, 50, y + 3f, 11, -20f);
                    pix.FillRoundRect(70, y - 3f, 18, 4, 2, BrowColor);
                    break;
                default:
                    Brow(pix, 50, y, 11, -8f);
                    Brow(pix, 78, y, 11, 8f);
                    break;
            }
        }

        static void Brow(Pix pix, float x, float y, float length, float degrees) =>
            pix.FillRotatedRect(x, y, length * 0.5f, 2.1f, degrees, BrowColor);

        static void DrawMouth(Pix pix, AvatarLook look, Color32 lip)
        {
            float x = look.Smile == 3 ? 70f : 64f;
            switch (look.Smile)
            {
                case 1:
                    pix.FillEllipse(64, 64, 18, 10, lip);
                    pix.FillRoundRect(50, 60, 28, 8, 3, Teeth);
                    pix.FillRoundRect(50, 63, 28, 2, 1, Mouth);
                    break;
                case 2:
                    pix.FillEllipse(64, 62, 14, 12, lip);
                    pix.FillEllipse(64, 61, 9, 8, Mouth);
                    pix.FillEllipse(64, 57, 5, 4, Tongue);
                    break;
                case 3:
                    pix.StrokeArc(x, 66, 12, 7, 3.2f, lip, upper: false);
                    pix.StrokeArc(x, 66, 8, 4, 1.8f, Mouth, upper: false);
                    break;
                case 4:
                    pix.FillRoundRect(50, 63, 28, 5, 2, lip);
                    break;
                case 5:
                    pix.StrokeArc(64, 68, 14, 8, 3.4f, lip, upper: false);
                    pix.FillEllipse(64, 58, 6, 7, Tongue);
                    pix.FillEllipse(64, 62, 5, 3, new Color32(232, 140, 150, 255));
                    break;
                default:
                    pix.StrokeArc(64, 66, 14, 8, 3.6f, lip, upper: false);
                    pix.StrokeArc(64, 66, 9, 4.5f, 1.6f, Mouth, upper: false);
                    break;
            }
        }

        static Color32 Darken(Color32 c, float scale) =>
            new Color32((byte)(c.r * scale), (byte)(c.g * scale), (byte)(c.b * scale), 255);

        sealed class Pix
        {
            readonly Color32[] px;
            readonly int w;
            readonly int h;

            public Pix(int width, int height)
            {
                w = width;
                h = height;
                px = new Color32[width * height];
            }

            public void Clear(Color32 c)
            {
                for (int i = 0; i < px.Length; i++) px[i] = c;
            }

            public void Plot(int x, int y, Color32 c)
            {
                if ((uint)x >= (uint)w || (uint)y >= (uint)h || c.a == 0) return;
                int i = y * w + x;
                if (c.a == 255) { px[i] = c; return; }
                var d = px[i];
                int a = c.a;
                int ia = 255 - a;
                px[i] = new Color32(
                    (byte)((c.r * a + d.r * ia) / 255),
                    (byte)((c.g * a + d.g * ia) / 255),
                    (byte)((c.b * a + d.b * ia) / 255),
                    255);
            }

            public void FillCircle(float cx, float cy, float r, Color32 c) => FillEllipse(cx, cy, r, r, c);

            public void FillEllipse(float cx, float cy, float rx, float ry, Color32 c, float ring = 0f)
            {
                int minX = Mathf.Max(0, Mathf.FloorToInt(cx - rx - 1f));
                int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(cx + rx + 1f));
                int minY = Mathf.Max(0, Mathf.FloorToInt(cy - ry - 1f));
                int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(cy + ry + 1f));
                float outer = 1f;
                float inner = ring > 0f ? Mathf.Pow(1f - ring / Mathf.Max(rx, ry), 2f) : -1f;
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float nx = (x + 0.5f - cx) / rx;
                    float ny = (y + 0.5f - cy) / ry;
                    float d = nx * nx + ny * ny;
                    if (d <= outer && d >= inner) Plot(x, y, c);
                }
            }

            public void StrokeArc(float cx, float cy, float rx, float ry, float thickness, Color32 c, bool upper)
            {
                int minX = Mathf.Max(0, Mathf.FloorToInt(cx - rx - 1f));
                int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(cx + rx + 1f));
                int minY = Mathf.Max(0, Mathf.FloorToInt(cy - ry - 1f));
                int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(cy + ry + 1f));
                float innerRx = Mathf.Max(0.5f, rx - thickness);
                float innerRy = Mathf.Max(0.5f, ry - thickness);
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float py = y + 0.5f;
                    if (upper ? py < cy : py > cy) continue;
                    float nx = (x + 0.5f - cx) / rx;
                    float ny = (py - cy) / ry;
                    float inx = (x + 0.5f - cx) / innerRx;
                    float iny = (py - cy) / innerRy;
                    if (nx * nx + ny * ny <= 1f && inx * inx + iny * iny >= 1f) Plot(x, y, c);
                }
            }

            public void FillRoundRect(float x, float y, float width, float height, float radius, Color32 c)
            {
                radius = Mathf.Min(radius, width * 0.5f, height * 0.5f);
                FillEllipse(x + radius, y + radius, radius, radius, c);
                FillEllipse(x + width - radius, y + radius, radius, radius, c);
                FillEllipse(x + radius, y + height - radius, radius, radius, c);
                FillEllipse(x + width - radius, y + height - radius, radius, radius, c);
                for (int py = Mathf.Max(0, Mathf.FloorToInt(y)); py <= Mathf.Min(h - 1, Mathf.CeilToInt(y + height)); py++)
                for (int px = Mathf.Max(0, Mathf.FloorToInt(x)); px <= Mathf.Min(w - 1, Mathf.CeilToInt(x + width)); px++)
                {
                    if (px + 0.5f >= x && px + 0.5f <= x + width && py + 0.5f >= y + radius && py + 0.5f <= y + height - radius)
                        Plot(px, py, c);
                    else if (py + 0.5f >= y && py + 0.5f <= y + height && px + 0.5f >= x + radius && px + 0.5f <= x + width - radius)
                        Plot(px, py, c);
                }
            }

            public void FillTriangle(float ax, float ay, float bx, float by, float cx, float cy, Color32 c)
            {
                float minX = Mathf.Min(ax, Mathf.Min(bx, cx));
                float maxX = Mathf.Max(ax, Mathf.Max(bx, cx));
                float minY = Mathf.Min(ay, Mathf.Min(by, cy));
                float maxY = Mathf.Max(ay, Mathf.Max(by, cy));
                float area = Edge(ax, ay, bx, by, cx, cy);
                if (Mathf.Abs(area) < 0.001f) return;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(minX));
                int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(maxX));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(minY));
                int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(maxY));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = Edge(bx, by, cx, cy, px, py);
                    float w1 = Edge(cx, cy, ax, ay, px, py);
                    float w2 = Edge(ax, ay, bx, by, px, py);
                    if (w0 * area >= 0f && w1 * area >= 0f && w2 * area >= 0f) Plot(x, y, c);
                }
            }

            public void FillRotatedRect(float cx, float cy, float halfW, float halfH, float degrees, Color32 c)
            {
                float rad = degrees * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);
                float reach = halfW + halfH + 1f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - reach));
                int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(cx + reach));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - reach));
                int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(cy + reach));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float lx = dx * cos + dy * sin;
                    float ly = -dx * sin + dy * cos;
                    if (Mathf.Abs(lx) <= halfW && Mathf.Abs(ly) <= halfH) Plot(x, y, c);
                }
            }

            public Sprite ToSprite()
            {
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.SetPixels32(px);
                tex.Apply(false, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            }

            static float Edge(float ax, float ay, float bx, float by, float px, float py) =>
                (px - ax) * (by - ay) - (py - ay) * (bx - ax);
        }
    }
}
