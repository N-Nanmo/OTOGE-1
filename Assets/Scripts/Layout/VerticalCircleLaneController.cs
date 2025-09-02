using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RhythmGame.Layout {
    public class VerticalCircleLaneController : MonoBehaviour {
        public enum LayoutMode {
            HalfArc
        }

        [Header("Lane Source")]
        [Tooltip("明示的なlane数設定(ChartDataAssetを優先)")]
        [SerializeField] private int laneCount = 14;

        [Header("Geometry")]
        [Tooltip("リング半径")]
        [SerializeField] private float radius = 6f;
        [Tooltip("判定円のZ座標")]
        [SerializeField] private float height = 0f;
        [Tooltip("リング全体の平行移動オフセット")]
        [SerializeField] private Vector3 globalOffset = Vector3.zero;

        [Header("Rotation")]
        [Tooltip("初期回転(度)正は右回転")]
        [SerializeField] private float baseRotationDeg = 0f;
        [Tooltip("アクションによる動的回転(度)")]
        [SerializeField] private float dynamicRotationDeg = 0f;
        [Tooltip("Recalculate時にアンカー位置を更新する")]
        [SerializeField] private bool autoRecalculateOnRotationChange = true;

        [Header("Generated Anchors(raedonly)")]
        [SerializeField] private List<LaneAnchor> anchors = new();

        [SerializeField]
        [HideInInspector]
        private Transform lanesContainer;
        [SerializeField, HideInInspector] private bool _pendingFull;
        [SerializeField, HideInInspector] private bool _pendingRecalc;
        [SerializeField, HideInInspector] private bool rescueExistingChildren;

        private const int FIXED_LANE_COUNT = 14;
        private const float FULL_CIRCLE = 360f;
        private const string CONTAINER_NAME = "__GeneratedLanes";


        public IReadOnlyList<LaneAnchor> Anchors => anchors;
        public int LaneCount => FIXED_LANE_COUNT;
        public float JudgePlaneZ => height;
        public float Rdius => radius;
        public float BaseRotationDeg
        {
            get => baseRotationDeg;
            set
            {
                if (Mathf.Approximately(baseRotationDeg, value)) return;
                baseRotationDeg = value;
                RequestRecalc();
            }
        }
        public float DynamicRotationDeg
        {
            get => dynamicRotationDeg;
            set
            {
                float norm = value % 360f;
                if (norm < 0f) norm += 360f;
                if (Mathf.Approximately(dynamicRotationDeg, norm)) return;
                dynamicRotationDeg = value;
                RequestRecalc();
            }
        }
        public void SetActionRotation(float absoluteDeg) => DynamicRotationDeg = absoluteDeg;

        public void AddActionRotation(float DeltaDeg) => DynamicRotationDeg = dynamicRotationDeg + DeltaDeg;

        public void ClearActionRotation(float angleDeg) => DynamicRotationDeg = 0f;

        public void ForceFullRebuild() {
            RequestFull();
            ProcessPending(Application.isPlaying);
        }

        public void ForceRecalculate() {
            RequestRecalc();
            ProcessPending(Application.isPlaying);
        }

        private void CleanDuplicatesContext() {
            RescueOrphans(always: true);
            RequestRecalc();
            ProcessPending(Application.isPlaying);
        }

        private void OnEnable() {
            EnforceLaneCount();
            EnsureContainer();
            RefreshAnchorsFromContainer();
            if (rescueExistingChildren) RescueOrphans();
            if (anchors.Count != FIXED_LANE_COUNT) {
                RequestFull();
            } else {
                RequestRecalc();
            }

            ProcessPending(Application.isPlaying);
        }

        private void OnValidate() {
            EnforceLaneCount();
            EnsureContainer();
            RefreshAnchorsFromContainer();
            if (anchors.Count != FIXED_LANE_COUNT) RequestFull();
            else RequestRecalc();
        }

        private void Update() {
            ProcessPending(Application.isPlaying);
        }

        private void RequestFull() {
            _pendingFull = true;
            _pendingRecalc = false;
        }
        private void RequestRecalc() {
            if (_pendingFull) return;
            _pendingRecalc = true;
        }

        private void ProcessPending(bool playmode) {
            if (_pendingFull) {
                DoFullRebuild(playmode);
                _pendingFull = false;
                _pendingRecalc = false;
            } else if (_pendingRecalc) {
                DoRecalculate(playmode);
                _pendingRecalc = false;
            }
        }
        private void DoFullRebuild(bool playmode) {
            EnsureContainer();
            var toRemove = new List<GameObject>();
            for (int i = lanesContainer.childCount - 1; i >= 0; i--) {
                var c = lanesContainer.GetChild(i);
                if (c.GetComponent<LaneAnchor>()) toRemove.Add(c.gameObject);
            }
            foreach (var go in toRemove) {
                if (playmode) Destroy(go);
#if UNITY_EDITOR
                else DestroyImmediate(go);
#endif
            }

            anchors.Clear();

            float step = FULL_CIRCLE / FIXED_LANE_COUNT;
            for (int i = 0; i < FIXED_LANE_COUNT; i++) {
                float angle = i * step + baseRotationDeg + dynamicRotationDeg;
                var p = AngleToPosition(angle);
                var child = new GameObject($"Lane_{i}");
                child.transform.SetParent(lanesContainer, false);
                child.transform.localPosition = p;
                child.transform.localRotation = Quaternion.identity;
                var anchor = child.AddComponent<LaneAnchor>();
                anchor.Initialized(i);
                anchors.Add(anchor);
            }
            MarkDirty();
        }

        private void DoRecalculate(bool playmode) {

            if (anchors.Count != FIXED_LANE_COUNT) {
                DoFullRebuild(playmode);
                return;
            }
            for(int i=0; i<anchors.Count; i++) {
                if (anchors[i] == null) {
                    DoFullRebuild(playmode);
                    return;
                }
            }
            float step = FULL_CIRCLE / FIXED_LANE_COUNT;
            for (int i = 0; i < FIXED_LANE_COUNT; i++) {
                float angle = i * step + baseRotationDeg + dynamicRotationDeg;
                anchors[i].transform.localPosition = AngleToPosition(angle);
            }
            MarkDirty();
        }

        private void EnforceLaneCount() {
            if (laneCount != FIXED_LANE_COUNT) laneCount = FIXED_LANE_COUNT;
        }

        private void EnsureContainer() {
            if (lanesContainer != null) return;
            var found = transform.Find(CONTAINER_NAME);
            if(found != null) {
                lanesContainer = found;
                return;
            }
            var go = new GameObject(CONTAINER_NAME);
            go.transform.SetParent(transform, false);
            lanesContainer = go.transform;
            MarkDirty();
        }

        private void RefreshAnchorsFromContainer() {
            if (lanesContainer == null) return;
            bool need = anchors == null || anchors.Count != FIXED_LANE_COUNT;
            if(!need && anchors != null) {
                for(int i=0; i<anchors.Count; i++) {
                    if (anchors[i] == null) {
                        need = true;
                        break;
                    }
                }
            }
            if (!need) return;

            var list = new List<LaneAnchor>(FIXED_LANE_COUNT);
            for(int i=0; i<lanesContainer.childCount; i++) {
                var c = lanesContainer.GetChild(i);
                var a = c.GetComponent<LaneAnchor>();
                if (a) {
                    list.Add(a);
                }
            }
            list.Sort((a, b) => a.LaneIndex.CompareTo(b.LaneIndex));
            anchors = list;
        }

        private void RescueOrphans(bool always = false) {
            if(!always && !rescueExistingChildren) return;

            EnsureContainer();
            bool movedAny = false;
            var toMove = new List<Transform>();
            for(int i=0; i<transform.childCount; i++) {
                var c = transform.GetChild(i);
                if (c == lanesContainer) continue;
                if (c.name.StartsWith("Lane_")) {
                    toMove.Add(c);
                }
            }
            foreach(var t in toMove) {
                t.SetParent(lanesContainer, true);
                movedAny = true;
                var a = t.GetComponent<LaneAnchor>();
                if(TryParseIndexFromName(t.name, out int idx)) {
                    a.Initialized(idx);
                }
            }
            if (movedAny) {
                RefreshAnchorsFromContainer();
                MarkDirty();
            }
            rescueExistingChildren = false;
        }

        private bool TryParseIndexFromName(string name, out int index) {
            index = -1;
            if (!name.StartsWith("Lane_")) return false;
            var part = name.Substring("Lane_".Length);
            return int.TryParse(part, out index);
        }

        private Vector3 AngleToPosition(float angleDeg) {
            float rad = angleDeg * Mathf.Deg2Rad;
            float y = radius * Mathf.Cos(rad);
            float x = radius * Mathf.Sin(rad);
            float z = JudgePlaneZ + globalOffset.z;
            Vector3 center = new Vector3(globalOffset.x, globalOffset.y, 0f);
            return new Vector3(x, y, z) + center;
        }

        private void MarkDirty() {
#if UNITY_EDITOR
            if(Application.isPlaying) return;
            EditorUtility.SetDirty(this);
            var scn = gameObject.scene;
            if (scn.IsValid()) {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scn);
            }
#endif
        }
    }
}
