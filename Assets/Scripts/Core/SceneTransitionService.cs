using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickyPictures
{
    public class SceneTransitionService : MonoBehaviour
    {
        public static void Load(string sceneName)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }
    }
}
