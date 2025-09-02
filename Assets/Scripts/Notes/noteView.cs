using UnityEngine;
using RhythmGame.Layout;
using RhythmGame.Data.Chart;

namespace RhythmGame.Notes {
    public class NoteView : MonoBehaviour {
        [Header("Runtime Data (readonly")]
        [SerializeField] private int laneIndex;
        [SerializeField] private float noteTime;
        [SerializeField] private NoteType noteType;

        [Header("Style")]
        [SerializeField] private NoteVisualStyle visualStyle;
        [SerializeField] private MeshRenderer meshRenderer;

        public int LaneIndex => laneIndex;
        public float NoteTime => noteTime;
        public NoteType Type => noteType;

        public void Initialize(int lane, float time, NoteType type, NoteVisualStyle style) {
            laneIndex = lane;
            noteTime = time;
            noteType = type;
            visualStyle = style;

            ApplyStyle();
        }

        private void Awake() {
            if(meshRenderer == null) {
                meshRenderer = GetComponentInChildren<MeshRenderer>();
            }
        }

        private void ApplyStyle() {
            if (visualStyle == null || meshRenderer == null) {
                return;
            }

            Color c = (noteType == NoteType.HoldStart || noteType == NoteType.HoldEnd) ? visualStyle.holdColor : visualStyle.baseColor;
            if(visualStyle.material != null) {
                var matInstance = Instantiate(visualStyle.material);
                matInstance.color = c;
                meshRenderer.material = matInstance;
            } else {
                meshRenderer.material = new Material(meshRenderer.material);
                meshRenderer.material.color = c;
            }

            transform.localScale = visualStyle.baseScale;
        }
    }
}