using UnityEngine;
using RhythmGame.Layout;
using RhythmGame.Notes;
using RhythmGame.Timing;

namespace RhythmGame.Notes {
    public class NoteMoverVerticalCircle : MonoBehaviour {
        [Header("References")]
        [SerializeField] private AudioConductor conductor;
        [SerializeField] private VisualTimeDriver visualTimeDriver;
        [SerializeField] private VerticalCircleLaneController circle;

        [Header("Spawn / Timing")]
        [SerializeField]
        [Tooltip("ƒm[ƒc¶¬‚©‚ç”»’èƒ‰ƒCƒ“‚Ü‚Å‚ÌŽžŠÔ(•b)")]
        private float approachTime = 2.0f;
        [SerializeField]
        [Tooltip("”»’è•½–Ê‚©‚ç’u‚­•ûŒü‚Ì‰Šú¶¬‹——£Z")]
        private float spawnDepth = 60f;

        [Header("Follow Behaviour")]
        [SerializeField]
        [Tooltip("ƒŒ[ƒ“‰ñ“]‚Éƒm[ƒc‚ð’Ç]‚³‚¹‚é‚©")]
        private bool followRotation = true;
        [SerializeField]
        [Tooltip("’Ç]‚ð’x‰„‚³‚¹‚éŽžŠÔ(1=’x‰„–³Œø)")]
        private float followLerp = 1f;
        [SerializeField]
        [Tooltip("‰ñ“]’Ç]‚ÌŠJŽn‚µ‚«‚¢’l(0.3=>Žè‘O70%‹æŠÔ‚Å’Ç]‹­‰»)")]
        private float followStartProggress = 0f;

        private NoteView noteview;
        private LaneAnchor anchor;
        private Vector3 spawnAnchorSnapshot; //”ñ’Ç],’x‰„—p

        private bool initialized;

        public void Initialize(NoteView view, LaneAnchor laneanchor, AudioConductor cond, VisualTimeDriver vtd, VerticalCircleLaneController ctrl) {
            noteview = view;
            anchor = laneanchor;
            conductor = cond;
            visualTimeDriver = vtd;
            circle = ctrl;

            spawnAnchorSnapshot = anchor.transform.position;

            Vector3 p = spawnAnchorSnapshot;
            p.z = circle.JudgePlaneZ + spawnDepth;
            transform.position = p;

            initialized = true;
        }

        private void Update() {
            if(!initialized || noteview == null || conductor == null || visualTimeDriver == null || circle == null || anchor == null) {
                return;
            }

            double vt = visualTimeDriver.VisualTime;
            float remaining = (float)(noteview.NoteTime - vt);
            float progress = 1f - Mathf.Clamp01(remaining / approachTime);//0..1

            Vector3 currentAnchor = anchor.transform.position;
            Vector3 baseAnchor = spawnAnchorSnapshot;

            Vector3 anchorForThisFrame;
            if (!followRotation) {
                anchorForThisFrame = baseAnchor;
            } else {
                float localBlend = 1f;
                if(followStartProggress > 0f) {
                    float t = Mathf.InverseLerp(followStartProggress, 1f, progress);
                    t = Mathf.Clamp01(t);
                    localBlend = (followLerp >= 1f) ? t : Mathf.Lerp(t, 1f, followLerp);
                }
                anchorForThisFrame = Vector3.Lerp(baseAnchor, currentAnchor, localBlend);
            }

            float depth = Mathf.Lerp(spawnDepth, 0f, progress);
            Vector3 pos = anchorForThisFrame;
            pos.z = circle.JudgePlaneZ + depth;
            transform.position = pos;
        }
    }
}
