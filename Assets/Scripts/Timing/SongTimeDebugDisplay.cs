using UnityEngine;

namespace RhythmGame.Timing {
    public class SongTimeDebugDisplay: MonoBehaviour {
        [SerializeField] private AudioConductor conductor;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private int fontSize = 16;
        [SerializeField] private VisualTimeDriver visualTimeDriver;
        private GUIStyle _style;

        private void Awake() {
            _style = new GUIStyle {
                fontSize = fontSize,
                normal = new GUIStyleState { textColor = textColor }
            };
        }

        private void OnGUI() {
            if(conductor == null) return;
            GUI.Label(new Rect(10, 10, 240, 30), $"SongTime: {conductor.SongTime:F3}s", _style);
            GUI.Label(new Rect(10, 30, 240, 30), $"VisualTime: {visualTimeDriver.VisualTime:F3}s", _style);
        }
    }
}
