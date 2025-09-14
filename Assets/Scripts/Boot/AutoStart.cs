using UnityEngine;
using RhythmGame.Timing;
using RhythmGame.Data.Chart;
using RhythmGame.Chart;
using RhythmGame.Notes;
using RhythmGame.Visual;
using System; // for Action
using System.Collections.Generic;
using RhythmGame.Special;

namespace RhythmGame.Boot {
    public class AutoStart : MonoBehaviour {
        public enum LaneMode { Center7, Edge7 }

        [Header("References")] [SerializeField] private AudioConductor conductor;
        [SerializeField] private string absoluteChartPath;
        [SerializeField] private ChartData chartData;
        [SerializeField] private SongChartAsset songChartAsset;
        [SerializeField] private bool playOnStart = true;
        [SerializeField, Tooltip("ゲーム開始(音/ノーツ生成)を遅らせる秒数")] private double startDelaySeconds = 2.0;
        [SerializeField] private LaneHighlighter laneHighlighter;
        [SerializeField, Tooltip("切替ノーツマネージャ(初期モード判定に使用)")] private ModeSwitchNoteManager modeSwitchManager;

        [Header("Lane Mode")] [SerializeField] private LaneMode laneMode = LaneMode.Center7;

        [Header("Input / Judge Settings")] 
        [SerializeField] private KeyCode[] sevenLaneKeys = new KeyCode[7] { KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Space, KeyCode.J, KeyCode.K, KeyCode.L };
        [SerializeField] private bool consumeOnMiss = true;
        [SerializeField] private bool logJudgment = true;
        [SerializeField, Tooltip("キー押下時の発光強度 0..1")] private float keyPulseStrength = 1f;
        [SerializeField, Tooltip("自動ミス判定を有効にする")] private bool autoMiss = true;

        [Header("Judge Counters (readonly)")]
        [SerializeField] private int perfectCount;
        [SerializeField] private int greatCount;
        [SerializeField] private int goodCount;
        [SerializeField] private int missCount;
        [SerializeField] private int currentCombo;
        [SerializeField] private int maxCombo;

        [Header("Debug")] [SerializeField] private bool debugLogs = false;

        // Events
        public event Action<NotesMover.JudgmentType,int,float,int> OnJudged;
        public event Action<LaneMode> OnLaneModeChanged;

        private NotesMover notesMover;
        private int[] currentLaneIndices = new int[7];
        private Dictionary<KeyCode,int> _keyToLane;
        private System.Collections.Generic.List<NotesMover.ActiveNoteSnapshot> _snapshotBuffer;
        private HashSet<int> _heldLanes; // lanes currently holding a long note

        private static readonly int[] CENTER_MAP = new int[]{10,9,8,7,6,5,4};
        private static readonly int[] EDGE_MAP   = new int[]{3,2,1,0,13,12,11};

        private void Start() {
            notesMover = FindFirstObjectByType<NotesMover>();
            if (modeSwitchManager == null) modeSwitchManager = FindFirstObjectByType<ModeSwitchNoteManager>();
            if (laneHighlighter == null) laneHighlighter = FindFirstObjectByType<LaneHighlighter>();
            if (notesMover != null) notesMover.enabled = false;
            _heldLanes = new HashSet<int>();

            LoadChart();

            // Decide initial lane mode BEFORE applying mapping, using switch-note presence
            if (modeSwitchManager != null) {
                var suggested = modeSwitchManager.SuggestInitialLaneMode(laneMode);
                if (laneMode != suggested) {
                    laneMode = suggested;
                    if (debugLogs) Debug.Log($"[AutoStart] Initial laneMode overridden by ModeSwitchNoteManager -> {laneMode}");
                }
            }

            ApplyLaneMode();
            if (notesMover != null) notesMover.SetChart(chartData);

            // Ensure AudioClip comes from the standard SongChartAsset
            if (conductor != null && songChartAsset != null && songChartAsset.audioClip != null && conductor.Source != null) {
                if (conductor.Source.clip != songChartAsset.audioClip) {
                    conductor.Source.clip = songChartAsset.audioClip;
                    if (debugLogs) Debug.Log($"[AutoStart] Assigned AudioClip from SongChartAsset '{songChartAsset.name}'");
                }
            }

            if (playOnStart && conductor != null) {
                if (startDelaySeconds < 0) startDelaySeconds = 0;
                conductor.OnPlaybackBegan += HandlePlaybackBegan;
                conductor.PlayScheduledAfter(startDelaySeconds);
                Debug.Log($"[AutoStart] Scheduled playback after {startDelaySeconds:F2}s (Mode={laneMode}, lanes={chartData?.lanes})");
            }
        }
        private void OnDestroy() { if (conductor != null) conductor.OnPlaybackBegan -= HandlePlaybackBegan; }
        private void Update() {
            if (notesMover == null || !notesMover.enabled || conductor == null) return;
            HandleLaneInput();
            if (autoMiss) AutoMissCheck();
            if (Input.GetKeyDown(KeyCode.M)) { ToggleLaneMode(); }

            // Auto-complete holds at tail while still holding
            if (_heldLanes != null && _heldLanes.Count > 0) {
                double songTime = conductor.SongTime;
                var lanes = new List<int>(_heldLanes);
                for (int i = 0; i < lanes.Count; i++) {
                    int lane = lanes[i];
                    if (notesMover.TryAutoCompleteHold(lane, songTime, out var j, out var d)) {
                        Accumulate(j);
                        if (logJudgment) Debug.Log($"[Judge] HoldEnd Lane {lane} {j} Δ={d:+0.000;-0.000} (auto-complete)");
                        OnJudged?.Invoke(j, lane, d, currentCombo);
                        _heldLanes.Remove(lane);
                    }
                }
            }
        }

        private void LoadChart() {
            bool loaded = false;
            if (songChartAsset != null && songChartAsset.jsonChart != null) {
                if (chartData == null) chartData = new ChartData();
                loaded = ChartJsonLoader.LoadJsonText(songChartAsset.jsonChart.text, chartData);
                if (loaded) {
                    songChartAsset.runtimeChart = chartData;
                    Debug.Log($"[AutoStart] Loaded chart from SongChartAsset '{songChartAsset.name}' ({songChartAsset.jsonChart.text.Length} chars), lanes={chartData.lanes}");
                } else {
                    Debug.LogError("[AutoStart] Failed to load chart from SongChartAsset");
                }
            }
            if (!loaded) {
                if (!string.IsNullOrEmpty(absoluteChartPath) && chartData != null) {
                    if (ChartJsonLoader.LoadFromFile(absoluteChartPath, chartData)) {
                        Debug.Log("[AutoStart] ChartData loaded from file path");
                        loaded = true;
                    } else {
                        Debug.LogError("[AutoStart] ChartData file load failed");
                    }
                } else {
                    Debug.LogError("[AutoStart] ChartData path or instance missing");
                }
            }
            if (!loaded) Debug.LogWarning("[AutoStart] No chart loaded.");
        }

        private void HandlePlaybackBegan() {
            if (notesMover != null && !notesMover.enabled) {
                notesMover.enabled = true;
                Debug.Log("[AutoStart] NotesMover enabled exactly at playback start");
            }
        }

        private void HandleLaneInput() {
            double songTime = conductor.SongTime;
            for (int i = 0; i < sevenLaneKeys.Length; i++) {
                var key = sevenLaneKeys[i];
                if (key == KeyCode.None) continue;
                if (Input.GetKeyDown(key)) {
                    int lane = MapKeyToLane(key, i);
                    if (laneHighlighter != null) laneHighlighter.Pulse(lane, keyPulseStrength);
                    // Inline judge to get noteType for hold tracking
                    if (notesMover.TryJudgeNearest(lane, songTime, consumeOnMiss, out var j, out var d, out var type)) {
                        if (j != NotesMover.JudgmentType.None) {
                            Accumulate(j);
                            if (logJudgment) Debug.Log($"[Judge] Lane {lane} {j} Δ={d:+0.000;-0.000} Type={type} Combo={currentCombo}");
                            OnJudged?.Invoke(j, lane, d, currentCombo);
                            if (type == NoteType.HoldStart && j != NotesMover.JudgmentType.Miss) _heldLanes.Add(lane);
                        }
                    } else if (logJudgment) {
                        Debug.Log($"[Judge] Lane {lane} : No note in window");
                    }
                }
                if (Input.GetKeyUp(key)) {
                    int lane = MapKeyToLane(key, i);
                    if (_heldLanes != null && _heldLanes.Contains(lane)) {
                        if (notesMover.TryRelease(lane, songTime, out var j, out var d)) {
                            if (j != NotesMover.JudgmentType.None) {
                                Accumulate(j);
                                if (logJudgment) Debug.Log($"[Judge] HoldEnd Lane {lane} {j} Δ={d:+0.000;-0.000} (release)");
                                OnJudged?.Invoke(j, lane, d, currentCombo);
                            }
                        }
                        _heldLanes.Remove(lane);
                    }
                }
            }
        }

        private int MapKeyToLane(KeyCode key, int defaultIndex) {
            if (_keyToLane != null && _keyToLane.TryGetValue(key, out var lane)) return lane;
            if (defaultIndex >= 0 && defaultIndex < currentLaneIndices.Length) return currentLaneIndices[defaultIndex];
            return currentLaneIndices.Length > 0 ? currentLaneIndices[0] : 0;
        }

        private bool TryJudgeLane(int lane, double songTime) {
            if (notesMover == null) return false;
            if (notesMover.TryJudgeNearest(lane, songTime, consumeOnMiss, out var judgment, out var delta, out var noteType)) {
                if (judgment != NotesMover.JudgmentType.None) {
                    Accumulate(judgment);
                    if (logJudgment) Debug.Log($"[Judge] Lane {lane} {judgment} Δ={delta:+0.000;-0.000} Type={noteType} Combo={currentCombo}");
                    OnJudged?.Invoke(judgment, lane, delta, currentCombo);
                    return true;
                }
            }
            return false;
        }

        private void AutoMissCheck(){
            double songTime = conductor.SongTime;
            if (_snapshotBuffer == null) _snapshotBuffer = new System.Collections.Generic.List<NotesMover.ActiveNoteSnapshot>(64);
            notesMover.SnapshotActive(_snapshotBuffer);
            float missWin = notesMover.MissWindow;
            foreach(var n in _snapshotBuffer){
                float late = (float)(songTime - n.noteTime);
                if (late > missWin){
                    if (notesMover.ConsumeExact(n.lane, n.noteTime)){
                        Accumulate(NotesMover.JudgmentType.Miss);
                        if (logJudgment) Debug.Log($"[AutoMiss] Lane {n.lane} time={n.noteTime:F3} late={late:F3}");
                        OnJudged?.Invoke(NotesMover.JudgmentType.Miss, n.lane, late, currentCombo);
                        if (_heldLanes != null) _heldLanes.Remove(n.lane);
                    }
                }
            }
        }

        private void Accumulate(NotesMover.JudgmentType j) {
            switch (j) {
                case NotesMover.JudgmentType.Perfect: perfectCount++; IncreaseCombo(); break;
                case NotesMover.JudgmentType.Great:   greatCount++; IncreaseCombo(); break;
                case NotesMover.JudgmentType.Good:    goodCount++;  IncreaseCombo(); break;
                case NotesMover.JudgmentType.Miss:    missCount++;  ResetCombo(); break;
            }
        }
        private void IncreaseCombo(){ currentCombo++; if(currentCombo>maxCombo) maxCombo=currentCombo; }
        private void ResetCombo(){ currentCombo=0; }

        private void ApplyLaneMode() {
            if (notesMover == null) return;
            if (chartData != null && chartData.lanes == 7) {
                if (laneMode == LaneMode.Center7) {
                    currentLaneIndices = (int[])CENTER_MAP.Clone();
                    BuildKeyToLane(CENTER_MAP);
                    notesMover.SetCenter7();
                    if (debugLogs) Debug.Log("[AutoStart] LaneMode=Center7 A,S,D,Space,J,K,L -> 10,9,8,7,6,5,4");
                }
                else {
                    currentLaneIndices = (int[])EDGE_MAP.Clone();
                    BuildKeyToLane(EDGE_MAP);
                    notesMover.SetEdge7();
                    if (debugLogs) Debug.Log("[AutoStart] LaneMode=Edge7 A,S,D,Space,J,K,L -> 3,2,1,0,13,12,11");
                }
            } else {
                currentLaneIndices = new int[]{0,1,2,3,4,5,6};
                BuildKeyToLane(currentLaneIndices);
                notesMover.SetAllLanes();
                if (debugLogs) Debug.Log("[AutoStart] LaneMode=AllLanes (14)");
            }
            OnLaneModeChanged?.Invoke(laneMode);
        }

        private void BuildKeyToLane(int[] laneMap) {
            if (sevenLaneKeys == null || laneMap == null) return;
            if (_keyToLane == null) _keyToLane = new Dictionary<KeyCode, int>(sevenLaneKeys.Length);
            else _keyToLane.Clear();
            int count = Mathf.Min(sevenLaneKeys.Length, laneMap.Length);
            for (int i = 0; i < count; i++) {
                var key = sevenLaneKeys[i];
                if (key != KeyCode.None) _keyToLane[key] = laneMap[i];
            }
        }

        public void ToggleLaneMode() { laneMode = laneMode == LaneMode.Center7 ? LaneMode.Edge7 : LaneMode.Center7; ApplyLaneMode(); Debug.Log($"[AutoStart] Switched LaneMode -> {laneMode}"); }
        public void SetLaneMode(LaneMode mode) { laneMode = mode; ApplyLaneMode(); }
        public LaneMode CurrentLaneMode => laneMode;

        public void ResetJudgeCounters() { perfectCount = greatCount = goodCount = missCount = 0; currentCombo=0; maxCombo=0; }
        public int PerfectCount => perfectCount; public int GreatCount => greatCount; public int GoodCount => goodCount; public int MissCount => missCount; public int CurrentCombo => currentCombo; public int MaxCombo => maxCombo;
    }
}
