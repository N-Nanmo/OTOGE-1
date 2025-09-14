using UnityEngine;

namespace RhythmGame.UI {
    public class Retry_Button : MonoBehaviour {
        public void OnClick() {
            // Reload the current scene
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }
}
