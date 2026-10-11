using UnityEngine;

namespace TrickyPictures
{
    public class AudioService : MonoBehaviour
    {
        const string MusicKey = "tp.musicVolume";
        const string SfxKey = "tp.sfxVolume";

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.8f);
            set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, 0.8f);
            set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); ApplySavedSettings(); }
        }

        public static void ApplySavedSettings() => AudioListener.volume = SfxVolume;

        void Awake() => ApplySavedSettings();
    }
}
