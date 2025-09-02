using UnityEngine;
using RhythmGame.Timing;

namespace RhythmGame.Boot {
    public class  AutoStart:MonoBehaviour {
        [SerializeField] private AudioConductor conductor;
        [SerializeField] private bool playOnStart = true;
        private void Start() {
            if(playOnStart && conductor != null) {
                conductor.PlayScheduled();
            }
        }
    }
}
