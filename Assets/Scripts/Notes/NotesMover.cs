using System.Collections.Generic;
using UnityEngine;
using RhythmGame.Data.Chart;
using RhythmGame.Timing;
using RhythmGame.Boot; // for AutoStart.LaneMode
using RhythmGame.Special; // for ModeSwitchNoteManager

namespace RhythmGame.Notes
{
    public class NotesMover : MonoBehaviour
    {
        [Header("Chart / Time References")]
        [SerializeField] private ChartData chartData;
        [SerializeField] private VisualTimeDriver visualTimeDriver;
        [Header("Lane Mode Timeline (optional)")]
        [SerializeField, Tooltip("切替ノーツの時間軸を参照してノートの生成モードを決定する")] private ModeSwitchNoteManager modeSwitchManager;

        [Header("Lane Bases (0~13)")]
        [SerializeField] private Transform[] laneBases = new Transform[14];

        [Header("Mode Switch Behavior")]
        [SerializeField, Tooltip("モード切替時に既に生成されたノーツを消さずに表示を維持する")] private bool keepSpawnedNotesVisibleOnModeChange = true;

        [Header("Note Prefab")]
        [SerializeField] private NoteView notePrefab;
        [SerializeField] private NoteVisualStyle noteStyle;
        [Header("Long Note (Hold)")]
        [Tooltip("ロングノーツのボディ描画に使うプレハブ(Quad/Image 等)。レーン子に配置されZ方向へ伸縮します")] 
        [SerializeField] private GameObject holdBodyPrefab;
        [Tooltip("ロングノーツの最小長さ(視認用)"), SerializeField]
        private float holdMinLengthZ = 0.02f;
        [Tooltip("ロングノーツボディの横幅(X)"), SerializeField]
        private float holdBodyWidthX = 0.6f;
        [Tooltip("ロングノーツボディの高さ(Y)"), SerializeField]
        private float holdBodyHeightY = 0.2f;
        [Tooltip("ホールドボディに holdColor を適用する (Renderer の色を per-instance で変更)")]
        [SerializeField] private bool tintHoldBody = true;

        [Header("Note Positions (local)")]
        [Tooltip("ノート開始位置Z(ローカル)")]
        [SerializeField] private float noteStartZ = 150f;
        [Tooltip("ノート判定位置Z(ローカル)")]
        [SerializeField] private float noteHitZ = 0f;
        [Tooltip("ノート通過後位置Z(ローカル)")]
        [SerializeField] private float noteEndZ = -150f;
        [Tooltip("ノートのローカルY")]
        [SerializeField] private float noteLocalY = 0f;

        [Header("Spawn Timing")]
        [SerializeField] private float approachTime = 2.0f;
        [SerializeField] private bool enablePostTravel = true;

        [Header("Judge Windows (sec |Δ|)")]
        [SerializeField] private float perfectWindow = 0.03f;
        [SerializeField] private float greatWindow = 0.06f;
        [SerializeField] private float goodWindow = 0.10f;
        [SerializeField] private float missWindow = 0.18f;

        [Header("Pooling")]
        [SerializeField] private int initialPool = 64;
        [SerializeField] private bool expandPool = true;

        [Header("Debug (readonly)")]
        [SerializeField] private int activeCount;
        [SerializeField] private int spawnedCount;
        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;
        [Tooltip("論理レーン(=block)をそのまま物理レーンとして使う")]
        [SerializeField] private bool forceIdentityMapping = false;

        // Measure Pipe (SIMPLE VERSION)
        [Header("Measure Pipes")]
        [SerializeField] private bool enableMeasurePipes = true;
        [SerializeField] private GameObject pipePrefab;
        [Tooltip("パイプ親。null の場合この NotesMover を親にする")]
        [SerializeField] private Transform pipeParent;
        [Tooltip(">0 なら小節長秒数を手動指定 (0で 4/4 自動)")]
        [SerializeField] private float manualMeasureDuration = 0f;
        [Tooltip("パイプ開始ローカル座標(親基準)")]
        [SerializeField] private Vector3 pipeStartLocal = new Vector3(0f, 150f, 0f);
        [Tooltip("パイプ終了ローカル座標(親基準)")]
        [SerializeField] private Vector3 pipeEndLocal = new Vector3(0f, -150f, 0f);

        [Header("Measure Pipe Override")]
        [Tooltip("ONで上下方向(0,topY,0)->(0,bottomY,0)へ強制。既存シリアライズ値を無視")]
        [SerializeField] private bool verticalPipeMode = true;
        [SerializeField] private float pipeTopY = 150f;
        [SerializeField] private float pipeBottomY = -150f;

        // Lane subset
        [Header("Lane Subset (runtime)")]
        [SerializeField] private bool useLaneSubset = true;
        [SerializeField] private bool[] laneEnabledPreview = new bool[14];

        // Chord Lines
        [Header("Chord Lines")]
        [SerializeField] private bool enableChordLines = true;
        [SerializeField] private GameObject chordLinePrefab;
        [Tooltip("同時判定の時間許容差(秒)")]
        [SerializeField] private float chordTimeEpsilon = 0.0001f;
        [Tooltip("角度ソートの中心。null ならこのオブジェクト")]
        [SerializeField] private Transform chordCenter;

        [Header("Chord Movement")]
        [Tooltip("生成時Z")]
        [SerializeField] private float chordStartZ = 150f;
        [Tooltip("終了時Z")]
        [SerializeField] private float chordEndZ = -150f;
        [Tooltip("Z移動に使うリード時間(0でノーツと同じ approachTime)")]
        [SerializeField] private float chordApproachTime = 0f;
        [Tooltip("ラインのローカル回転(Euler)")]
        [SerializeField] private Vector3 chordLineRotationEuler = new Vector3(0f, 0f, 0f);

        [Header("Chord Arc Settings")]
        [Tooltip("弧のセグメント数 (最小 2)")]
        [SerializeField] [Range(2, 128)] private int chordArcSegments = 24;
        [Tooltip("0 で自動(対象レーン距離の平均)。>0 で固定半径")]
        [SerializeField] private float chordArcRadiusOverride = 0f;
        [Tooltip("角度方向補間を最短経路にする")]
        [SerializeField] private bool chordArcShortest = true;
        [Tooltip("線を消すまでの遅延(ヒット後)。0で即時")]
        [SerializeField] private float chordCleanupDelay = 0.05f;

        // Provide safe defaults for note local positions matching the original implementation
        private static readonly Vector3 DEFAULT_NOTE_START_LOCAL = new(0f, -0.5f, 0.5f);
        private static readonly Vector3 DEFAULT_NOTE_HIT_LOCAL   = new(0f, -0.5f, -0.496f);
        private static readonly Vector3 DEFAULT_NOTE_END_LOCAL   = new(0f, -0.5f, -0.5f);

        // Backing fields to detect if user overrode positions
        private bool _useCustomNotePositions = false;

        private Vector3 NoteStartLocal => _useCustomNotePositions ? new Vector3(0f, noteLocalY, noteStartZ) : DEFAULT_NOTE_START_LOCAL;
        private Vector3 NoteHitLocal   => _useCustomNotePositions ? new Vector3(0f, noteLocalY, noteHitZ)   : DEFAULT_NOTE_HIT_LOCAL;
        private Vector3 NoteEndLocal   => _useCustomNotePositions ? new Vector3(0f, noteLocalY, noteEndZ)   : DEFAULT_NOTE_END_LOCAL;

        private const float POST_TOTAL_DELTA = 0.01f;

        private float _mainDistance;
        private float _speed;
        private float _postDuration;
        private int _laneCount = 14; // logical lanes from chart
        private int[] _nextIndexPerLane; // logical cursor per lane

        private readonly Queue<NoteView> _pool = new();
        private readonly List<ActiveNote> _active = new();
        
        private struct ActiveNote
        {
            public int lane; // physical lane index (0..13)
            public float noteTime;
            public NoteView view;
        }
        
        // Expose snapshot struct and judgment enum (restored)
        public struct ActiveNoteSnapshot { public int lane; public float noteTime; }
        public enum JudgmentType { None, Perfect, Great, Good, Miss }
        
        private readonly bool[] _laneEnabled = new bool[14];

        // Logical->Physical lane mapping (for 7-lane charts)
        // If null or length mismatch with chart logical lanes, identity mapping is used.
        private int[] _logicalToPhysical;
        // Desired mapping for 7-lane charts (set even before chart is assigned)
        private int[] _desiredMap7;

        // Hold runtime
        private struct ActiveHold
        {
            public int lane; // physical
            public float startTime;
            public float endTime;
            public Transform body; // visual body (child of lane)
            public NoteView headView; // optional reference
            public bool isHolding;
            public bool headJudged;
            public bool tailJudged;
            public JudgmentType headJudgment; // store head result to reuse at tail
        }
        private readonly List<ActiveHold> _activeHolds = new();
        private readonly Queue<Transform> _holdBodyPool = new();
        private MaterialPropertyBlock _holdMpb; // for coloring hold body per-instance
        private static readonly int COLOR_PROP_ID = Shader.PropertyToID("_Color");
        private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");

        private struct ActivePipe
        {
            public float time;
            public Transform tr;
        }

        private readonly List<ActivePipe> _activePipes = new();
        private readonly Queue<Transform> _pipePool = new();
        private float _measureDuration = 1f; // seconds
        private List<float> _measureTimes;
        private int _nextMeasureIndex;

        // chord internal
        private struct ChordGroup
        {
            public float time; // adjusted by offset at spawn
            public int[] lanes; // logical lane indices
        }

        private struct ActiveChord
        {
            public float time;
            public int[] lanes; // physical lanes
            public LineRenderer lr;
        }

        private List<ChordGroup> _chordGroups;
        private int _nextChordIndex;
        private readonly List<ActiveChord> _activeChords = new();
        private readonly Queue<ActiveChord> _chordPool = new();

        // Debug one-shot flags
        private bool _chartDumped = false;
        private bool _mappingDumped = false;

        #region Unity

        private void Awake()
        {
            AutoFindLaneBases();
            PreparePool(initialPool);
            if (_holdMpb == null) _holdMpb = new MaterialPropertyBlock();
            // Decide if the serialized note position values differ from defaults; if so, use custom
            _useCustomNotePositions = !(Mathf.Approximately(noteStartZ, 150f) && Mathf.Approximately(noteHitZ, 0f) && Mathf.Approximately(noteEndZ, -150f) && Mathf.Approximately(noteLocalY, 0f));
            PrecomputeTimings();
            InitCursors();
            InitDefaultLaneSubset();
            PrecomputeMeasures();
            PrecomputeChords();
        }

        private void Update()
        {
            if (chartData == null) return;

            // Dump mapping once when Update starts, after potential external SetCenter7/SetEdge7
            if (debugLogs && !_mappingDumped)
            {
                _mappingDumped = true;
                var mapStr = (_logicalToPhysical != null && _logicalToPhysical.Length == _laneCount)
                    ? string.Join(",", _logicalToPhysical)
                    : "(identity or null)";
                Debug.Log($"[NotesMover] MappingDump lanes={_laneCount} identity={forceIdentityMapping} map={mapStr}");
                // Also show what MapLane returns for each logical
                var phys = new System.Text.StringBuilder();
                phys.Append("[ ");
                for (int l = 0; l < _laneCount; l++)
                {
                    phys.Append(MapLane(l));
                    if (l < _laneCount-1) phys.Append(",");
                }
                phys.Append(" ]");
                Debug.Log($"[NotesMover] MapLane(logical->physical) = {phys}");
            }

            float vt = CurrentTime();
            if (chartData.notes == null) return;

            SpawnLoop(vt);
            MoveActive(vt);
            MoveHolds(vt);

            if (enableMeasurePipes)
            {
                SpawnPipes(vt);
                MovePipes(vt);
            }

            if (enableChordLines)
            {
                SpawnChords(vt);
                MoveChords(vt);
            }

            activeCount = _active.Count;
        }

        private void DumpChartComposition()
        {
            if (chartData == null || chartData.notes == null) return;
            if (_chartDumped) return;
            _chartDumped = true;
            Debug.Log($"[NotesMover] Chart='{chartData.chartId}' lanes={chartData.lanes} bpm={chartData.bpm} offset={chartData.offset:F3} approach={approachTime}");
            for (int i = 0; i < chartData.notes.Length; i++)
            {
                var list = chartData.notes[i];
                int c = list?.Count ?? 0;
                float first = (c>0) ? list[0].time : -1f;
                float last  = (c>0) ? list[^1].time : -1f;
                Debug.Log($"[NotesMover] LogicalLane {i}: count={c} first={first:F3} last={last:F3}");
            }
        }

        private void OnValidate()
        {
            for (int i = 0; i < laneEnabledPreview.Length && i < _laneEnabled.Length; i++)
            {
                _laneEnabled[i] = laneEnabledPreview[i];
            }

            if (verticalPipeMode)
            {
                pipeStartLocal = new Vector3(0f, pipeTopY, 0f);
                pipeEndLocal = new Vector3(0f, pipeBottomY, 0f);
            }
        }

        #endregion

        #region LaneSubset / Mapping

        private void InitDefaultLaneSubset()
        {
            // default to Center7 visible subset
            SetCenter7();
        }

        // Public: configure mapping explicitly (for external timeline control)
        public void SetLogicalToPhysicalMap(int[] map)
        {
            _logicalToPhysical = map;
            if (map != null && map.Length == 7) _desiredMap7 = (int[])map.Clone();
        }

        private int MapLane(int logical)
        {
            // Prefer explicit mapping when available (e.g., 7-lane charts), regardless of forceIdentityMapping
            if (_logicalToPhysical != null && chartData != null && _logicalToPhysical.Length == _laneCount)
            {
                int p = _logicalToPhysical[logical];
                if (p >= 0 && p < laneBases.Length) return p;
                if (debugLogs) Debug.LogWarning($"[NotesMover] Physical lane out of range: logical={logical} -> {p}");
            }
            // Fallbacks: identity or invalid mapping
            if (forceIdentityMapping) return logical;
            return logical; // identity fallback
        }

        // Additional mapping helper for time-dependent mode (Center7/Edge7)
        private static readonly int[] MAP_CENTER7 = new int[] { 10, 9, 8, 7, 6, 5, 4 };
        private static readonly int[] MAP_EDGE7   = new int[] { 3, 2, 1, 0, 13, 12, 11 };
        private int MapLaneForMode(int logical, AutoStart.LaneMode mode)
        {
            if (_laneCount == 7)
            {
                var map = (mode == AutoStart.LaneMode.Center7) ? MAP_CENTER7 : MAP_EDGE7;
                if (logical >= 0 && logical < map.Length) return map[logical];
            }
            return MapLane(logical);
        }

        public void SetLaneSubset(int[] lanes)
        {
            for (int i = 0; i < 14; i++) _laneEnabled[i] = false;
            if (lanes != null)
            {
                foreach (var l in lanes)
                {
                    if (l >= 0 && l < 14) _laneEnabled[l] = true;
                }
            }
            useLaneSubset = true;
            SyncPreview();
            if (!keepSpawnedNotesVisibleOnModeChange)
            {
                ClearInactiveNotes();
            }
            else if (debugLogs)
            {
                Debug.Log("[NotesMover] Mode switch: keeping already-spawned notes visible across subsets");
            }
        }

        // Public API to enable all lanes (identity mapping)
        public void SetAllLanes()
        {
            for (int i = 0; i < 14; i++) _laneEnabled[i] = true;
            useLaneSubset = false;
            SyncPreview();
            if (debugLogs) Debug.Log("[NotesMover] SetAllLanes: all physical lanes enabled, subset disabled");
        }

        public void SetCenter7()
        {
            // Visible physical lanes (Center 7): 4..10
            SetLaneSubset(new int[] { 4, 5, 6, 7, 8, 9, 10 });
            // Prepare mapping for 7-lane charts
            var map7 = new int[] { 10, 9, 8, 7, 6, 5, 4 };
            _desiredMap7 = map7;
            if (_laneCount == 7) _logicalToPhysical = (int[])map7.Clone();
            if (debugLogs) Debug.Log($"[NotesMover] Mode=Center7 map=[{string.Join(",", _logicalToPhysical ?? new int[0])}] subset=[4,5,6,7,8,9,10]");
        }

        public void SetEdge7()
        {
            // Visible physical lanes (Edge 7): 11,12,13,0,1,2,3
            SetLaneSubset(new int[] { 11, 12, 13, 0, 1, 2, 3 });
            // Prepare mapping for 7-lane charts
            var map7 = new int[] { 3, 2, 1, 0, 13, 12, 11 };
            _desiredMap7 = map7;
            if (_laneCount == 7) _logicalToPhysical = (int[])map7.Clone();
            if (debugLogs) Debug.Log($"[NotesMover] Mode=Edge7 map=[{string.Join(",", _logicalToPhysical ?? new int[0])}] subset=[11,12,13,0,1,2,3]");
        }

        private void SyncPreview()
        {
            for (int i = 0; i < laneEnabledPreview.Length && i < _laneEnabled.Length; i++)
            {
                laneEnabledPreview[i] = _laneEnabled[i];
            }
        }

        private bool LaneActive(int lane)
        {
            return !useLaneSubset || (lane >= 0 && lane < 14 && _laneEnabled[lane]);
        }

        private void ClearInactiveNotes()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (!LaneActive(_active[i].lane)) ConsumeByIndex(i);
            }
        }

        #endregion

        #region Judge API

        public bool TryJudgeNearest(int lane, double songTime, bool consumeOnMiss, out JudgmentType judgment, out float delta, out NoteType noteType)
        {
            judgment = JudgmentType.None;
            delta = 0f;
            noteType = NoteType.Tap;

            if (!LaneActive(lane)) return false;

            int target = -1;
            float best = float.MaxValue;

            for (int i = 0; i < _active.Count; i++)
            {
                var n = _active[i];
                if (n.lane != lane) continue;

                float d = (float)(songTime - n.noteTime);
                float ad = Mathf.Abs(d);
                if (ad > missWindow) continue;

                if (ad < best)
                {
                    best = ad;
                    target = i;
                    delta = d;
                    noteType = n.view ? n.view.Type : NoteType.Tap;
                }
            }

            if (target < 0) return false;

            judgment = Classify(best);
            if (judgment == JudgmentType.None) return false;
            if (judgment == JudgmentType.Miss && !consumeOnMiss) return true;

            // If this is a hold head and judged (not Miss), mark holding
            if (noteType == NoteType.HoldStart && judgment != JudgmentType.Miss)
            {
                // Use start-time proximity to locate the corresponding ActiveHold
                int hi = FindActiveHoldIndexByStart(lane, nearTime: (float)songTime, window: missWindow);
                if (hi >= 0)
                {
                    var h = _activeHolds[hi];
                    h.isHolding = true;
                    h.headJudged = true;
                    h.headJudgment = judgment; // remember head judgment
                    _activeHolds[hi] = h;
                }
            }

            ConsumeByIndex(target);
            return true;
        }

        // New: Press API (explicit), calls TryJudgeNearest internally
        public bool TryPress(int lane, double songTime, out JudgmentType judgment, out float delta)
        {
            return TryJudgeNearest(lane, songTime, consumeOnMiss: true, out judgment, out delta, out _);
        }

        // New: Release API for long notes
        public bool TryRelease(int lane, double songTime, out JudgmentType judgment, out float delta)
        {
            judgment = JudgmentType.None;
            delta = 0f;
            if (!LaneActive(lane)) return false;

            int hi = FindActiveHoldIndexForLane(lane, nearTime: (float)songTime, window: 9999f); // find any ongoing hold on lane
            if (hi < 0) return false;
            var h = _activeHolds[hi];

            // Only consider holds that started (or assume head already judged)
            if (!h.headJudged) { /* optional: auto-judge head as Miss */ }

            float d = (float)(songTime - h.endTime);
            float ad = Mathf.Abs(d);
            if (ad > missWindow)
            {
                // Early or late release => Miss
                judgment = JudgmentType.Miss;
                delta = d;
                // End holding state
                h.isHolding = false;
                h.tailJudged = true;
                _activeHolds[hi] = h;
                // Consume tail virtual note if exists
                ConsumeExact(lane, h.endTime);
                return true;
            }

            // Within windows
            // Reuse head judgment when hold succeeded within window
            judgment = h.headJudged ? h.headJudgment : Classify(ad);
            delta = d;
            h.isHolding = false;
            h.tailJudged = true;
            _activeHolds[hi] = h;
            // Consume tail virtual note
            ConsumeExact(lane, h.endTime);
            return true;
        }

        // Called by AutoStart to auto-complete a hold when the player keeps holding through the tail.
        public bool TryAutoCompleteHold(int lane, double songTime, out JudgmentType judgment, out float delta)
        {
            judgment = JudgmentType.None;
            delta = 0f;
            int hi = FindActiveHoldIndexForLane(lane, nearTime: (float)songTime, window: missWindow);
            if (hi < 0) return false;
            var h = _activeHolds[hi];
            if (!h.headJudged || h.tailJudged) return false;

            // If tail time already passed within miss window, finalize using head judgment
            float d = (float)(songTime - h.endTime);
            float ad = Mathf.Abs(d);
            if (ad <= missWindow) {
                judgment = h.headJudgment;
                delta = d;
                h.isHolding = false;
                h.tailJudged = true;
                _activeHolds[hi] = h;
                ConsumeExact(lane, h.endTime);
                return true;
            }
            return false;
        }

        // Find the active hold on a lane near a given time (TAIL proximity)
        private int FindActiveHoldIndexForLane(int lane, float nearTime, float window)
        {
            int found = -1; float best = float.MaxValue;
            for (int i = 0; i < _activeHolds.Count; i++)
            {
                var h = _activeHolds[i];
                if (h.lane != lane) continue;
                if (h.tailJudged) continue;
                float ad = Mathf.Abs(h.endTime - nearTime);
                if (ad < best && ad <= Mathf.Max(window, 0.0001f))
                {
                    best = ad; found = i;
                }
                else if (window > 9000f)
                {
                    found = i; best = ad;
                }
            }
            return found;
        }

        // Find the active hold on a lane near a given time (HEAD proximity)
        private int FindActiveHoldIndexByStart(int lane, float nearTime, float window)
        {
            int found = -1; float best = float.MaxValue;
            for (int i = 0; i < _activeHolds.Count; i++)
            {
                var h = _activeHolds[i];
                if (h.lane != lane) continue;
                if (h.headJudged) continue; // only consider not-yet-judged heads
                float ad = Mathf.Abs(h.startTime - nearTime);
                if (ad < best && ad <= Mathf.Max(window, 0.0001f))
                {
                    best = ad; found = i;
                }
            }
            return found;
        }

        public JudgmentType Classify(float absDelta)
        {
            if (absDelta <= perfectWindow) return JudgmentType.Perfect;
            if (absDelta <= greatWindow) return JudgmentType.Great;
            if (absDelta <= goodWindow) return JudgmentType.Good;
            if (absDelta <= missWindow) return JudgmentType.Miss;
            return JudgmentType.None;
        }

        public void SnapshotActive(List<ActiveNoteSnapshot> buf)
        {
            buf.Clear();
            for (int i = 0; i < _active.Count; i++)
            {
                var n = _active[i];
                if (LaneActive(n.lane)) buf.Add(new ActiveNoteSnapshot { lane = n.lane, noteTime = n.noteTime });
            }
        }

        public bool ConsumeExact(int lane, float noteTime)
        {
            if (!LaneActive(lane)) return false;
            for (int i = 0; i < _active.Count; i++)
            {
                var n = _active[i];
                if (n.lane == lane && Mathf.Approximately(n.noteTime, noteTime))
                {
                    ConsumeByIndex(i);
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Setup

        public void SetChart(ChartData data)
        {
            chartData = data;
            if (chartData != null && chartData.approachRate > 0f)
            {
                approachTime = chartData.approachRate;
            }
            // 7レーン前提。マッピング/サブセットは外部のSetCenter7/SetEdge7で切替。ここでは強制しない。
            PrecomputeTimings();
            InitCursors();
            ClearAllActive();
            PrecomputeMeasures();
            PrecomputeChords();

            if (debugLogs) DumpChartComposition();
        }

        private void InitCursors()
        {
            if (chartData == null) return;

            _laneCount = Mathf.Clamp(chartData.lanes > 0 ? chartData.lanes : 14, 1, laneBases.Length);

            if (chartData.notes == null || chartData.notes.Length != _laneCount)
            {
                var arr = new List<NoteData>[_laneCount];
                for (int i = 0; i < _laneCount; i++) arr[i] = new List<NoteData>();
                chartData.notes = arr;
            }

            _nextIndexPerLane = new int[_laneCount];

            // If chart is 7-lane, adopt desired mapping if provided; otherwise default to Center7 mapping
            if (_laneCount == 7)
            {
                if (_desiredMap7 != null && _desiredMap7.Length == 7)
                {
                    _logicalToPhysical = (int[])_desiredMap7.Clone();
                }
                else if (_logicalToPhysical == null || _logicalToPhysical.Length != 7)
                {
                    _logicalToPhysical = new int[] { 10, 9, 8, 7, 6, 5, 4 };
                }
            }
            else
            {
                // Non-7-lane chart: ignore desired map
                _logicalToPhysical = null;
            }
        }

        private void PrecomputeTimings()
        {
            // Use the active local positions for timing computation
            var start = NoteStartLocal;
            var hit   = NoteHitLocal;
            var end   = NoteEndLocal;
            _mainDistance = Mathf.Abs(start.z - hit.z);
            if (approachTime <= 0.01f) approachTime = 0.01f;
            _speed = _mainDistance / approachTime;
            _postDuration = enablePostTravel ? Mathf.Abs(hit.z - end.z) / _speed : 0f;
        }

        #endregion

        #region Note Pool/Spawn/Move

        private void PreparePool(int count)
        {
            if (notePrefab == null) return;
            for (int i = 0; i < count; i++)
            {
                var v = Instantiate(notePrefab);
                v.gameObject.SetActive(false);
                _pool.Enqueue(v);
            }
        }

        private NoteView GetView()
        {
            if (_pool.Count == 0)
            {
                if (!expandPool || notePrefab == null) return null;
                PreparePool(1);
                if (_pool.Count == 0) return null;
            }

            var v = _pool.Dequeue();
            v.gameObject.SetActive(true);
            return v;
        }

        private void ConsumeByIndex(int idx)
        {
            var n = _active[idx];
            if (n.view != null)
            {
                n.view.ResetForPool();
                n.view.gameObject.SetActive(false);
                _pool.Enqueue(n.view);
            }

            int last = _active.Count - 1;
            _active[idx] = _active[last];
            _active.RemoveAt(last);
        }

        private void ClearAllActive()
        {
            for (int i = _active.Count - 1; i >= 0; i--) ConsumeByIndex(i);
            _active.Clear();
            ClearAllPipes();
            ClearAllChords();
            ClearAllHolds();
        }

        private void SpawnLoop(float vtime)
        {
            if (_nextIndexPerLane == null) return;

            for (int logical = 0; logical < _laneCount; logical++)
            {
                var list = chartData.notes[logical];
                if (list == null) { if (debugLogs) Debug.Log($"[NotesMover] logical {logical} has null list"); continue; }
                if (debugLogs && _nextIndexPerLane[logical] == 0)
                {
                    Debug.Log($"[NotesMover] SpawnScan logical={logical} listCount={list.Count}");
                }
                int idx = _nextIndexPerLane[logical];

                while (idx < list.Count)
                {
                    var nd = list[idx];
                    float nt = nd.time + (applyChartOffset ? chartData.offset : 0f);
                    float remain = nt - vtime;
                    if (remain > approachTime) break;

                    // Decide physical lane at this note's timestamp using mode switch timeline
                    int physical;
                    if (modeSwitchManager != null && _laneCount == 7)
                    {
                        var modeAtNote = modeSwitchManager.GetLaneModeAtTime(nt);
                        physical = MapLaneForMode(logical, modeAtNote);
                    }
                    else
                    {
                        physical = MapLane(logical);
                    }

                    // If the lane for this note is currently inactive (due to current subset), do not consume it.
                    // Wait until the subset changes (e.g., after a switch note) and try again in a later frame.
                    // However, when using time-based mode mapping (modeSwitchManager != null), allow spawn to make them visible in advance.
                    if (!LaneActive(physical) && modeSwitchManager == null) break;

                    if (nd.type == NoteType.HoldStart && nd.duration > 0f)
                    {
                        float endT = nt + nd.duration;
                        SpawnHold(physical, nt, endT);
                        SpawnOne(physical, nt, NoteType.HoldStart);
                        _active.Add(new ActiveNote { lane = physical, noteTime = endT, view = null });
                        spawnedCount++;
                        if (debugLogs) Debug.Log($"[NotesMover] Hold virtual tail queued lane={physical} endT={endT:F3}");
                    }
                    else
                    {
                        SpawnOne(physical, nt, nd.type);
                    }

                    idx++;
                }

                _nextIndexPerLane[logical] = idx;
            }
        }

        private void SpawnOne(int lane, float noteTime, NoteType type)
        {
            var baseT = laneBases[lane];
            if (baseT == null)
            {
                if (debugLogs) Debug.LogError($"[NotesMover] Lane base missing for physical lane {lane}");
                return;
            }

            var view = GetView();
            if (view == null)
            {
                if (debugLogs) Debug.LogError("[NotesMover] NoteView pool empty and cannot expand");
                return;
            }

            // parent first then style/position for predictable local scale
            view.transform.SetParent(baseT, false);
            view.Initialize(lane, noteTime, type, noteStyle);
            view.transform.localPosition = NoteStartLocal;

            _active.Add(new ActiveNote { lane = lane, noteTime = noteTime, view = view });
            spawnedCount++;
            if (debugLogs) Debug.Log($"[NotesMover] Spawn lane(Logical? no)={lane} time={noteTime:F3} type={type}");
        }

        private void MoveActive(float vtime)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var an = _active[i];
                float noteTime = an.noteTime;
                float dt = noteTime - vtime;

                Vector3 pos;
                if (dt > 0f)
                {
                    // START->HIT
                    float traveled = approachTime - dt;
                    float t = Mathf.Clamp01(traveled / approachTime);
                    pos = Vector3.Lerp(NoteStartLocal, NoteHitLocal, t);
                }
                else
                {
                    // optional post
                    if (!enablePostTravel || _postDuration <= 0f)
                    {
                        pos = NoteHitLocal;
                    }
                    else
                    {
                        float postElapsed = -dt;
                        if (postElapsed >= _postDuration)
                        {
                            pos = NoteEndLocal;
                        }
                        else
                        {
                            float t2 = Mathf.Clamp01(postElapsed / _postDuration);
                            pos = Vector3.Lerp(NoteHitLocal, NoteEndLocal, t2);
                        }
                    }
                }

                if (an.view != null) an.view.transform.localPosition = pos;

                if (enablePostTravel && _postDuration > 0f && vtime >= noteTime + _postDuration + 0.1f)
                {
                    ConsumeByIndex(i);
                }
            }
        }

        #endregion

        #region Measure Pipes (Simple parent)

        private void PrecomputeMeasures()
        {
            _measureTimes = null;
            _nextMeasureIndex = 0;
            if (!enableMeasurePipes || chartData == null) return;

            float bpm = chartData.bpm > 0 ? chartData.bpm : 120f;
            _measureDuration = (manualMeasureDuration > 0) ? manualMeasureDuration : 4f * (60f / bpm);

            float last = 0f;
            if (chartData.notes != null)
            {
                for (int l = 0; l < chartData.notes.Length; l++)
                {
                    var list = chartData.notes[l];
                    if (list != null && list.Count > 0)
                    {
                        float t = list[^1].time;
                        if (t > last) last = t;
                    }
                }
            }

            float total = last + _measureDuration;
            int count = Mathf.CeilToInt(total / _measureDuration) + 1;
            _measureTimes = new List<float>(count);
            for (int i = 0; i < count; i++) _measureTimes.Add(i * _measureDuration);
        }

        private void SpawnPipes(float vtime)
        {
            if (_measureTimes == null || pipePrefab == null) return;

            while (_nextMeasureIndex < _measureTimes.Count)
            {
                float mt = _measureTimes[_nextMeasureIndex] + (applyChartOffset ? chartData.offset : 0f);
                if (mt - vtime > approachTime) break;
                SpawnPipe(mt);
                _nextMeasureIndex++;
            }
        }

        private void SpawnPipe(float time)
        {
            if (verticalPipeMode)
            {
                pipeStartLocal = new Vector3(0f, pipeTopY, 0f);
                pipeEndLocal = new Vector3(0f, pipeBottomY, 0f);
            }

            Transform parent = pipeParent != null ? pipeParent : transform;
            Transform tr;

            if (_pipePool.Count > 0)
            {
                tr = _pipePool.Dequeue();
                tr.gameObject.SetActive(true);
            }
            else
            {
                var go = Instantiate(pipePrefab, parent, false);
                tr = go.transform;
            }

            tr.SetParent(parent, false);
            tr.localPosition = pipeStartLocal;
            _activePipes.Add(new ActivePipe { time = time, tr = tr });
        }

        private void MovePipes(float vtime)
        {
            if (verticalPipeMode)
            {
                pipeStartLocal = new Vector3(0f, pipeTopY, 0f);
                pipeEndLocal = new Vector3(0f, pipeBottomY, 0f);
            }

            for (int i = _activePipes.Count - 1; i >= 0; i--)
            {
                var ap = _activePipes[i];
                float remain = ap.time - vtime;
                float progress = 1f - Mathf.Clamp01(remain / approachTime);

                if (ap.tr != null) ap.tr.localPosition = Vector3.Lerp(pipeStartLocal, pipeEndLocal, progress);
                if (vtime >= ap.time + 0.1f) RecyclePipe(i);
            }
        }

        private void RecyclePipe(int index)
        {
            var ap = _activePipes[index];
            if (ap.tr != null)
            {
                ap.tr.gameObject.SetActive(false);
                _pipePool.Enqueue(ap.tr);
            }

            int last = _activePipes.Count - 1;
            _activePipes[index] = _activePipes[last];
            _activePipes.RemoveAt(last);
        }

        private void ClearAllPipes()
        {
            for (int i = _activePipes.Count - 1; i >= 0; i--) RecyclePipe(i);
            _activePipes.Clear();
            _nextMeasureIndex = 0;
        }

        #endregion

        #region Chord Lines

        private void PrecomputeChords()
        {
            _chordGroups = null;
            _nextChordIndex = 0;
            if (!enableChordLines || chartData == null || chartData.notes == null) return;

            var map = new List<(float time, int lane)>();
            for (int l = 0; l < chartData.notes.Length; l++)
            {
                var list = chartData.notes[l];
                if (list == null) continue;
                foreach (var nd in list) map.Add((nd.time, l)); // keep logical lanes
            }

            map.Sort((a, b) => a.time.CompareTo(b.time));

            var groups = new List<ChordGroup>();
            float eps = chordTimeEpsilon;
            int i = 0;
            while (i < map.Count)
            {
                float baseT = map[i].time;
                var lanesTemp = new List<int> { map[i].lane };
                float maxT = baseT;
                int j = i + 1;

                while (j < map.Count && Mathf.Abs(map[j].time - baseT) <= eps)
                {
                    lanesTemp.Add(map[j].lane);
                    if (map[j].time > maxT) maxT = map[j].time;
                    j++;
                }

                if (lanesTemp.Count > 1)
                {
                    lanesTemp.Sort();
                    groups.Add(new ChordGroup { time = maxT, lanes = lanesTemp.ToArray() });
                }

                i = j;
            }

            _chordGroups = groups;
        }

        private void SpawnChords(float vtime)
        {
            if (_chordGroups == null || chordLinePrefab == null) return;

            float lead = chordApproachTime > 0f ? chordApproachTime : approachTime;
            while (_nextChordIndex < _chordGroups.Count)
            {
                var cg = _chordGroups[_nextChordIndex];
                float ctime = cg.time + (applyChartOffset ? chartData.offset : 0f);
                if (ctime - vtime > lead) break;
                var mapped = new int[cg.lanes.Length];
                for (int k = 0; k < cg.lanes.Length; k++) mapped[k] = MapLane(cg.lanes[k]);
                var adj = new ActiveChord { time = ctime, lanes = mapped };
                SpawnChord(adj);
                _nextChordIndex++;
            }
        }

        private void SpawnChord(ActiveChord ac)
        {
            ActiveChord pooled;
            if (_chordPool.Count > 0)
            {
                pooled = _chordPool.Dequeue();
                pooled.lr.gameObject.SetActive(true);
            }
            else
            {
                var go = Instantiate(chordLinePrefab, transform, false);
                var lr = go.GetComponent<LineRenderer>();
                if (lr == null) lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                pooled = new ActiveChord { lr = lr };
            }

            pooled.time = ac.time;
            pooled.lanes = ac.lanes;

            if (pooled.lr != null)
            {
                pooled.lr.transform.SetParent(transform, false);
                pooled.lr.transform.localRotation = Quaternion.Euler(chordLineRotationEuler);
            }

            // positionCount is set in MoveChords (arc)
            _activeChords.Add(pooled);
        }

        private void MoveChords(float vtime)
        {
            float lead = chordApproachTime > 0f ? chordApproachTime : approachTime;
            for (int i = _activeChords.Count - 1; i >= 0; i--)
            {
                var ac = _activeChords[i];
                float dt = ac.time - vtime;
                float progress = 1f - Mathf.Clamp01(dt / lead);
                float curZ = Mathf.Lerp(chordStartZ, chordEndZ, progress);
                var lr = ac.lr;
                if (lr == null)
                {
                    RecycleChord(i);
                    continue;
                }

                // --- Arc computation (XY plane) without allocations ---
                Vector3 center = chordCenter ? chordCenter.position : transform.position;
                float radiusAccum = 0f;
                int valid = 0;
                float minAng = 0f, maxAng = 0f;
                float singleAng = 0f;
                bool firstSet = false;

                for (int p = 0; p < ac.lanes.Length; p++)
                {
                    int lane = ac.lanes[p];
                    if (lane < 0 || lane >= laneBases.Length) continue;

                    var lb = laneBases[lane];
                    if (lb == null) continue;

                    Vector3 wp = lb.position;
                    float dx = wp.x - center.x;
                    float dy = wp.y - center.y;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    // guard zero radius to avoid NaN
                    if (r <= 1e-6f) continue;

                    float ang = Mathf.Atan2(dy, dx);
                    if (!firstSet)
                    {
                        minAng = maxAng = singleAng = ang;
                        firstSet = true;
                    }
                    else
                    {
                        if (ang < minAng) minAng = ang;
                        if (ang > maxAng) maxAng = ang;
                    }

                    radiusAccum += r;
                    valid++;
                }

                if (valid < 2)
                {
                    if (valid == 1)
                    {
                        lr.positionCount = 1;
                        float r = radiusAccum / valid;
                        float x = center.x + Mathf.Cos(singleAng) * r;
                        float y = center.y + Mathf.Sin(singleAng) * r;
                        lr.SetPosition(0, new Vector3(x, y, curZ));
                    }
                    else
                    {
                        lr.positionCount = 0;
                    }

                    if (vtime >= ac.time + chordCleanupDelay) RecycleChord(i);
                    continue;
                }

                float startAng = minAng;
                float endAng = maxAng;
                float span = endAng - startAng;

                if (chordArcShortest && span > Mathf.PI)
                {
                    startAng = maxAng;
                    endAng = minAng + Mathf.PI * 2f;
                    span = endAng - startAng;
                }

                float radius = chordArcRadiusOverride > 0f ? chordArcRadiusOverride : (radiusAccum / valid);
                int segs = Mathf.Clamp(chordArcSegments, 2, 512);
                lr.positionCount = segs + 1;

                for (int s = 0; s <= segs; s++)
                {
                    float t = (float)s / segs;
                    float ang = startAng + span * t;
                    float x = center.x + Mathf.Cos(ang) * radius;
                    float y = center.y + Mathf.Sin(ang) * radius;
                    lr.SetPosition(s, new Vector3(x, y, curZ));
                }

                if (vtime >= ac.time + chordCleanupDelay) RecycleChord(i);
            }
        }

        private void RecycleChord(int idx)
        {
            var ac = _activeChords[idx];
            if (ac.lr != null)
            {
                ac.lr.gameObject.SetActive(false);
                _chordPool.Enqueue(ac);
            }

            int last = _activeChords.Count - 1;
            _activeChords[idx] = _activeChords[last];
            _activeChords.RemoveAt(last);
        }

        private void ClearAllChords()
        {
            for (int i = _activeChords.Count - 1; i >= 0; i--) RecycleChord(i);
            _activeChords.Clear();
            _nextChordIndex = 0;
        }

        #endregion

        #region Holds
        private void SpawnHold(int lane, float startTime, float endTime)
        {
            if (holdBodyPrefab == null) return;
            var baseT = laneBases[lane];
            if (baseT == null) return;

            Transform bodyTr;
            if (_holdBodyPool.Count > 0)
            {
                bodyTr = _holdBodyPool.Dequeue();
                bodyTr.gameObject.SetActive(true);
            }
            else
            {
                var go = Instantiate(holdBodyPrefab, baseT, false);
                bodyTr = go.transform;
            }
            bodyTr.SetParent(baseT, false);
            bodyTr.localPosition = NoteStartLocal;

            // Apply hold color to body (per-instance) if requested
            if (tintHoldBody && _holdMpb != null)
            {
                Renderer[] rends = bodyTr.GetComponentsInChildren<Renderer>(true);
                Color hc = (noteStyle != null) ? noteStyle.holdColor : new Color(1f, 0.8f, 0.2f, 1f);
                for (int i = 0; i < rends.Length; i++)
                {
                    var r = rends[i];
                    _holdMpb.Clear();
                    _holdMpb.SetColor(COLOR_PROP_ID, hc);
                    var mat = r.sharedMaterial;
                    if (mat != null && mat.HasProperty(BASE_COLOR_ID)) _holdMpb.SetColor(BASE_COLOR_ID, hc);
                    r.SetPropertyBlock(_holdMpb);
                }
            }

            var ah = new ActiveHold
            {
                lane = lane,
                startTime = startTime,
                endTime = endTime,
                body = bodyTr,
                headView = null
            };
            _activeHolds.Add(ah);
        }

        private void MoveHolds(float vtime)
        {
            for (int i = _activeHolds.Count - 1; i >= 0; i--)
            {
                var h = _activeHolds[i];
                var baseT = laneBases[h.lane];
                if (baseT == null || h.body == null)
                {
                    RecycleHold(i);
                    continue;
                }
                // Compute head Z based on start time
                float dtHead = h.startTime - vtime;
                float headZ;
                if (dtHead > 0f)
                {
                    float traveled = approachTime - dtHead;
                    float t = Mathf.Clamp01(traveled / approachTime);
                    headZ = Mathf.Lerp(NoteStartLocal.z, NoteHitLocal.z, t);
                }
                else
                {
                    headZ = NoteHitLocal.z;
                }

                // Compute tail Z based on end time
                float dtTail = h.endTime - vtime;
                float tailZ;
                if (dtTail > 0f)
                {
                    float traveled2 = approachTime - dtTail;
                    float t3 = Mathf.Clamp01(traveled2 / approachTime);
                    tailZ = Mathf.Lerp(NoteStartLocal.z, NoteHitLocal.z, t3);
                }
                else
                {
                    tailZ = NoteHitLocal.z;
                }

                // Determine body position and scale along Z (simple: stretch from head to tail around center)
                float zA = headZ;
                float zB = tailZ;
                float midZ = 0.5f * (zA + zB);
                float length = Mathf.Max(Mathf.Abs(zA - zB), holdMinLengthZ);
                var lp = h.body.localPosition;
                lp.x = NoteStartLocal.x;
                lp.y = NoteStartLocal.y;
                lp.z = midZ;
                h.body.localPosition = lp;
                var ls = h.body.localScale;
                if (ls == Vector3.zero) ls = Vector3.one;
                ls.x = holdBodyWidthX;
                ls.y = holdBodyHeightY;
                ls.z = length;
                h.body.localScale = ls;

                // Ensure hold body mesh is oriented along Z (scale.z used as length)
                var lr = h.body.GetComponent<LineRenderer>();
                if (lr != null) {
                    // if a line is used, position two points at head/tail in local space Z
                    lr.positionCount = 2;
                    lr.useWorldSpace = false;
                    lr.SetPosition(0, new Vector3(0f, 0f, zA - midZ));
                    lr.SetPosition(1, new Vector3(0f, 0f, zB - midZ));
                }
                
                // Cleanup when tail time passed a bit
                if (vtime >= h.endTime + 0.05f)
                {
                    RecycleHold(i);
                }
            }
        }

        private void RecycleHold(int index)
        {
            var h = _activeHolds[index];
            if (h.body != null)
            {
                h.body.gameObject.SetActive(false);
                _holdBodyPool.Enqueue(h.body);
            }
            int last = _activeHolds.Count - 1;
            _activeHolds[index] = _activeHolds[last];
            _activeHolds.RemoveAt(last);
        }

        private void ClearAllHolds()
        {
            for (int i = _activeHolds.Count - 1; i >= 0; i--) RecycleHold(i);
            _activeHolds.Clear();
        }
        #endregion

        #region Helpers

        private void AutoFindLaneBases()
        {
            for (int i = 0; i < laneBases.Length; i++)
            {
                if (laneBases[i] != null) continue;
                var go = GameObject.Find(i.ToString());
                if (go != null) laneBases[i] = go.transform;
            }
        }

        #endregion

        [Header("Timing Sync")]
        [SerializeField] private bool applyChartOffset = true;

        public float MissWindow => missWindow;

        private float CurrentTime()
        {
            if (visualTimeDriver == null) return 0f;
            double t = visualTimeDriver.VisualTime;
            if (t < 0) t = 0;
            // Do not subtract chart offset here; it is applied at spawn-time to event timestamps.
            return (float)t;
        }
    }
}