using UnityEngine;
using RhythmGame.Layout;
using RhythmGame.Data.Chart;
using RhythmGame.Notes;

namespace RhythmGame.Notes {

    public class NoteDebugSpawner : MonoBehaviour {
        [SerializeField] private VerticalCircleLaneController Controller;
        [SerializeField] private NoteView notePrefab;
        [SerializeField] private NoteVisualStyle noteStyle;
        [SerializeField] private float baseTime = 1f;
        [SerializeField] private float laneTimeStep = 0.2f;
        [SerializeField] private float verticalOffset = 0.5f;

        private void Start() {
            if (Controller == null) {
                Controller = Object.FindFirstObjectByType<VerticalCircleLaneController>();
            }
            if(Controller == null || notePrefab == null) {
                Debug.LogWarning("[NoteDebugSpawner] Missing references.");
                return;
            }

            var anchors = Controller.Anchors;
            if(anchors == null || anchors.Count == 0) {
                Controller.ForceFullRebuild();
                anchors = Controller.Anchors;
            }
            for (int i = 0; i < anchors.Count; i++) {
                var anchor = anchors[i];
                var view = Instantiate(notePrefab, anchor.transform);

                view.transform.localPosition = new Vector3(
                    0,
                    verticalOffset,
                    -2f
                );
                view.Initialize(
                    i,
                    baseTime + laneTimeStep * i,
                    NoteType.Tap,
                    noteStyle
                );
            }
        }
    }
}
