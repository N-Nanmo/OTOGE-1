using UnityEngine;

namespace RhythmGame.Timing {
    //外部UIやデバッグメニューからtimedcaleを操作
    public class TimeScaleController : MonoBehaviour {
        [SerializeField] private VisualTimeDriver visualTimeDriver;
        [SerializeField] private AudioConductor conductor;

        public void SetScale(float scale) {
            if (visualTimeDriver != null) {
                visualTimeDriver.SetTimeScale(scale);
            }
        }

        public void Pause(bool pause) {
            if (conductor != null) {
                conductor.Pause(pause);
            }
        }

        public void Snap() {
            if(visualTimeDriver != null) {
                visualTimeDriver.ForceSnap();
            }
        }
    }
}
