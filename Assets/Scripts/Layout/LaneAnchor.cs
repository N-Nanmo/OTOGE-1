using UnityEngine;

namespace RhythmGame.Layout {
    public class  LaneAnchor : MonoBehaviour {
        [SerializeField] private int laneIndex;
        public int LaneIndex => laneIndex;

        public void Initialized(int index) {
            laneIndex = index;
        }
    }
}
