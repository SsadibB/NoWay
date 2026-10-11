using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickyPictures
{
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] Image loadingFill;
        [SerializeField] TMP_Text statusText;
        [SerializeField] float minimumDisplayTime = 1f;

        IEnumerator Start()
        {
            SetStatus("Loading profile...", 0.1f);
            var profile = ProfileService.Current;
            AudioService.ApplySavedSettings();
            yield return null;

            SetStatus($"Welcome, {profile.DisplayName}!", 0.3f);
            var op = SceneManager.LoadSceneAsync(AppConfig.MainMenuScene);
            op.allowSceneActivation = false;

            float elapsed = 0f;
            while (elapsed < minimumDisplayTime || op.progress < 0.9f)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Min(elapsed / minimumDisplayTime, op.progress / 0.9f);
                SetFill(0.3f + 0.7f * progress);
                yield return null;
            }

            SetFill(1f);
            op.allowSceneActivation = true;
        }

        void SetStatus(string text, float fill)
        {
            if (statusText) statusText.text = text;
            SetFill(fill);
        }

        void SetFill(float amount)
        {
            if (loadingFill) loadingFill.fillAmount = amount;
        }
    }
}
