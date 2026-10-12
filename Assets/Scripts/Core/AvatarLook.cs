using UnityEngine;

namespace TrickyPictures
{
    public enum AvatarPart
    {
        Body,
        Face,
        Eyes,
        Brow,
        Smile,
        Lips,
    }

    /// <summary>Six-part avatar stored as <c>a</c> plus one digit per part.</summary>
    public struct AvatarLook
    {
        public const int OptionCount = 6;

        public static readonly string[] PartNames = { "Body", "Face", "Eyes", "Brow", "Smile", "Lips" };

        public static readonly string[][] OptionNames =
        {
            new[] { "Round", "Slim", "Dress", "Broad", "Hoodie", "Overalls" },
            new[] { "Round", "Oval", "Heart", "Square", "Long", "Wide" },
            new[] { "Open", "Happy", "Wide", "Sleepy", "Wink", "Hearts" },
            new[] { "Soft", "Raised", "Angry", "Worried", "Thick", "Skeptical" },
            new[] { "Smile", "Grin", "Laugh", "Smirk", "Flat", "Tongue" },
            new[] { "Rose", "Red", "Pink", "Berry", "Nude", "Plum" },
        };

        public static readonly AvatarLook[] Presets =
        {
            new AvatarLook(0, 0, 0, 0, 0, 0),
            new AvatarLook(1, 1, 1, 1, 1, 1),
            new AvatarLook(2, 2, 2, 2, 2, 2),
            new AvatarLook(3, 3, 3, 3, 3, 3),
            new AvatarLook(4, 4, 4, 4, 4, 5),
            new AvatarLook(5, 5, 5, 5, 5, 3),
            new AvatarLook(3, 1, 4, 2, 3, 1),
            new AvatarLook(4, 2, 1, 5, 5, 2),
        };

        public int Body;
        public int Face;
        public int Eyes;
        public int Brow;
        public int Smile;
        public int Lips;

        public AvatarLook(int body, int face, int eyes, int brow, int smile, int lips)
        {
            Body = Clamp(body);
            Face = Clamp(face);
            Eyes = Clamp(eyes);
            Brow = Clamp(brow);
            Smile = Clamp(smile);
            Lips = Clamp(lips);
        }

        public string ToId() => $"a{Body}{Face}{Eyes}{Brow}{Smile}{Lips}";

        public int Get(AvatarPart part)
        {
            switch (part)
            {
                case AvatarPart.Body: return Body;
                case AvatarPart.Face: return Face;
                case AvatarPart.Eyes: return Eyes;
                case AvatarPart.Brow: return Brow;
                case AvatarPart.Smile: return Smile;
                default: return Lips;
            }
        }

        public void Set(AvatarPart part, int value)
        {
            value = Clamp(value);
            switch (part)
            {
                case AvatarPart.Body: Body = value; break;
                case AvatarPart.Face: Face = value; break;
                case AvatarPart.Eyes: Eyes = value; break;
                case AvatarPart.Brow: Brow = value; break;
                case AvatarPart.Smile: Smile = value; break;
                default: Lips = value; break;
            }
        }

        public AvatarLook With(AvatarPart part, int value)
        {
            var copy = this;
            copy.Set(part, value);
            return copy;
        }

        public static AvatarLook Parse(string id)
        {
            if (!string.IsNullOrEmpty(id) && id.Length == 7 && id[0] == 'a')
            {
                bool digits = true;
                for (int i = 1; i < 7; i++)
                    if (id[i] < '0' || id[i] > '5') digits = false;
                if (digits)
                    return new AvatarLook(id[1] - '0', id[2] - '0', id[3] - '0', id[4] - '0', id[5] - '0', id[6] - '0');
            }

            int index = 0;
            const string prefix = "avatar_";
            if (!string.IsNullOrEmpty(id) && id.StartsWith(prefix) &&
                int.TryParse(id.Substring(prefix.Length), out var parsed))
                index = Mathf.Clamp(parsed, 0, Presets.Length - 1);
            return Presets[index];
        }

        public string OptionLabel(AvatarPart part) => OptionNames[(int)part][Get(part)];

        static int Clamp(int value) => Mathf.Clamp(value, 0, OptionCount - 1);
    }
}
