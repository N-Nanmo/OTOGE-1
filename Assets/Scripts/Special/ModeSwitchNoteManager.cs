using System.Collections.Generic;
using UnityEngine;
using RhythmGame.Timing;
using RhythmGame.Data.Chart;
using RhythmGame.Boot;
using RhythmGame.CameraSystem;
using RhythmGame.Chart;
using RhythmGame.Notes;

namespace RhythmGame.Special {
    /// <summary>
    /// Edge7/Center7 を切り替える専用ノーツ(レーン無し)の生成・移動・判定・ゲームオーバー処理。
    /// JSON仕様は通常ノーツと同じ(ChartJsonModel)だが、type=1:LeftShift(+180Z), type=2:RightShift(-180Z) を意味する。
    /// </summary>
    public class ModeSwitchNoteManager : MonoBehaviour {
        [Header("References")]
        [SerializeField] private AudioConductor conductor;
        [SerializeField] private AutoStart autoStart;
        [SerializeField] private LaneModeCameraController cameraController;
        [SerializeField] private NotesMover notesMover;
        [SerializeField, Tooltip("モード切替用チャート(JSON)")] private SongChartAsset modeSwitchChart;

        [Header("Visual (Pipe)")]
        [SerializeField] private GameObject modePipePrefab; // fallback
        [SerializeField, Tooltip("type=1(LeftShift) 用")] private GameObject leftShiftPipePrefab;
        [SerializeField, Tooltip("type=2(RightShift) 用")] private GameObject rightShiftPipePrefab;
        [SerializeField] private Transform pipeParent;
        [SerializeField] private Vector3 pipeStartLocal = new Vector3(0f, 150f, 0f);
        [SerializeField] private Vector3 pipeEndLocal   = new Vector3(0f, -150f, 0f);
        [SerializeField, Tooltip("出現から判定位置までのリード時間(s)")] private float approachTime = 2.0f;

        [Header("Judge")]
        [SerializeField, Tooltip("判定ウィンドウ(|Δ|)")] private float judgeWindow = 0.12f;
        [SerializeField, Tooltip("KeyDownだけでなく押しっぱなし(GetKey)でも判定を通す")] private bool acceptHeldKey = true;
        [SerializeField, Tooltip("Miss時にゲームオーバーにする")] private bool stopOnMiss = true;

        [Header("Initial Lane Mode (Suggestion)")]
        [SerializeField, Tooltip("切替ノーツが存在する場合の初期LaneMode")] private AutoStart.LaneMode initialModeWhenHasEvents = AutoStart.LaneMode.Edge7;
        [SerializeField, Tooltip("切替ノーツが存在しない場合の初期LaneMode")] private AutoStart.LaneMode initialModeWhenNoEvents = AutoStart.LaneMode.Center7;

        [Header("Game Over UI")] [SerializeField] private GameObject gameOverUI;
        [Header("Debug")] [SerializeField] private bool debugLogs = false;

        private struct ModeEvent { public float time; public int type; public bool done; }
        private List<ModeEvent> _events;
        private int _nextIdx;

        private struct ActivePipe { public float time; public Transform tr; public bool consumed; }
        private readonly List<ActivePipe> _activePipes = new List<ActivePipe>();

        private bool _initialized;
        private bool _gameOver;

        private void Awake(){
            if(conductor==null) conductor = FindFirstObjectByType<AudioConductor>();
            if(autoStart==null) autoStart = FindFirstObjectByType<AutoStart>();
            if(cameraController==null) cameraController = FindFirstObjectByType<LaneModeCameraController>();
            if(notesMover==null) notesMover = FindFirstObjectByType<NotesMover>();
        }

        private void OnEnable(){
            Initialize();
        }

        private void Initialize(){
            if (_initialized) return;
            _events = LoadEvents(modeSwitchChart);
            _events?.Sort((a,b)=>a.time.CompareTo(b.time));
            _nextIdx = 0;
            if (gameOverUI != null) gameOverUI.SetActive(false);
            _initialized = true;
        }

        private void Update(){
            if(!_initialized || _gameOver || conductor==null) return; double songTime = conductor.SongTime;
            SpawnLoop((float)songTime);
            MovePipes((float)songTime);
            HandleJudge((float)songTime);
            AutoMissCheck((float)songTime);
        }

        private List<ModeEvent> LoadEvents(SongChartAsset asset){
            if (asset == null || asset.jsonChart == null || string.IsNullOrEmpty(asset.jsonChart.text)) return new List<ModeEvent>(0);
            var model = JsonUtility.FromJson<ChartJsonModel>(asset.jsonChart.text);
            if (model == null) return new List<ModeEvent>(0);
            float bpm = (model.BPM > 0 ? model.BPM : (model.bpm > 0 ? model.bpm : 120f));
            // offset または songOffset を採用（msなら秒換算後に 1/10）
            float offA = NormalizeOffset(model.offset);
            float offB = NormalizeOffset(model.songOffset);
            float offset = (offA != 0f) ? offA : offB;
            var list = new List<ModeEvent>(64);
            if (model.notes != null){ foreach(var n in model.notes){ AddEventFromNote(list, n, bpm, offset); } }
            if (model.beats != null){ foreach(var b in model.beats){ if (b?.notes == null) continue; int lpb = b.LPB <= 0 ? 4 : b.LPB; foreach(var n in b.notes){ AddEventFromNote(list, n, bpm, offset, lpb); } } }
            return list;
        }

        private static float NormalizeOffset(float off){ float seconds = (off > 10f) ? off * 0.001f : off; return seconds * 0.1f; }
        private static float CalcTime(int num, int lpb, float bpm){ if (lpb<=0) lpb=4; return (60f/bpm) * (num/(float)lpb); }
        private static int ParseTypeInt(string s){ int v=0; int.TryParse(s, out v); return v; }

        private void AddEventFromNote(List<ModeEvent> dst, ChartJsonModel.NoteJson n, float bpm, float offset, int defaultLpb = 4){ if (n==null) return; float t = (n.LPB>0 || defaultLpb>0) ? CalcTime(n.num, n.LPB>0?n.LPB:defaultLpb, bpm) : Mathf.Max(0f, n.time); t += offset; int type = n.typeCode != 0 ? n.typeCode : ParseTypeInt(n.type); dst.Add(new ModeEvent{ time=t, type=type, done=false }); }

        private void SpawnLoop(float songTime){ if (_events == null) return; while(_nextIdx < _events.Count){ var ev = _events[_nextIdx]; float lead = ev.time - songTime; if (lead > approachTime) break; SpawnPipe(ev.time, ev.type); _nextIdx++; } }

        private void SpawnPipe(float time, int type){
            GameObject prefab = null;
            if (type == 1 && leftShiftPipePrefab != null) prefab = leftShiftPipePrefab;
            else if (type == 2 && rightShiftPipePrefab != null) prefab = rightShiftPipePrefab;
            else prefab = modePipePrefab;
            if (prefab == null) return;
            Transform parent = pipeParent != null ? pipeParent : transform;
            Transform tr = Instantiate(prefab, parent, false).transform;
            tr.localPosition = pipeStartLocal;
            _activePipes.Add(new ActivePipe{ time=time, tr=tr, consumed=false });
        }

        private void MovePipes(float songTime){ for(int i=_activePipes.Count-1;i>=0;i--){ var ap=_activePipes[i]; float remain = ap.time - songTime; float progress = 1f - Mathf.Clamp01(remain / Mathf.Max(0.0001f, approachTime)); if (ap.tr!=null) ap.tr.localPosition = Vector3.Lerp(pipeStartLocal, pipeEndLocal, progress); if (songTime >= ap.time + 0.1f){ if(ap.tr!=null) Destroy(ap.tr.gameObject); _activePipes.RemoveAt(i); } } }

        private void HandleJudge(float songTime){ if (_events == null || _events.Count==0) return; // 最も近い未処理イベントを探す
            int target = -1; float best = float.MaxValue; for(int i=0;i<_events.Count;i++){ if(_events[i].done) continue; float d = songTime - _events[i].time; float ad = Mathf.Abs(d); if (ad <= judgeWindow && ad < best){ best = ad; target = i; } }
            if (target < 0) return; var e = _events[target];
            // Shift入力を判定（KeyDown もしくは acceptHeldKey が true の場合は GetKey でもOK）
            bool left = Input.GetKeyDown(KeyCode.LeftShift) || (acceptHeldKey && Input.GetKey(KeyCode.LeftShift));
            bool right = Input.GetKeyDown(KeyCode.RightShift) || (acceptHeldKey && Input.GetKey(KeyCode.RightShift));
            if (debugLogs) Debug.Log($"[ModeSwitchNoteManager] Judge t={songTime:F3} ev={e.time:F3} type={e.type} L={left} R={right}");
            if ((e.type==1 && left) || (e.type==2 && right)) { // success
                ApplyModeSwitch(e.type); e.done = true; _events[target] = e; CleanupPipeNearTime(e.time); }
        }

        private void AutoMissCheck(float songTime){ if (_events == null) return; for(int i=0;i<_events.Count;i++){ if(_events[i].done) continue; float late = songTime - _events[i].time; if (late > judgeWindow){ if (stopOnMiss) HandleGameOver(); var e=_events[i]; e.done=true; _events[i]=e; CleanupPipeNearTime(e.time); break; } } }

        private void ApplyModeSwitch(int type){
            if (autoStart == null) return;
            var current = autoStart.CurrentLaneMode;
            var next = current == AutoStart.LaneMode.Center7 ? AutoStart.LaneMode.Edge7 : AutoStart.LaneMode.Center7;
            float extraZ = (type==1) ? -180f : 180f;
            if (cameraController != null) cameraController.BeginBlendToWithExtraZ(next, extraZ);
            autoStart.SetLaneMode(next)
                ; }

        private void HandleGameOver(){ if (_gameOver) return; _gameOver = true; if (conductor != null) conductor.StopImmediate(); if (notesMover != null) notesMover.enabled = false; enabled = false; if (gameOverUI != null) gameOverUI.SetActive(true); Debug.Log("[ModeSwitchNoteManager] Game Over (mode-switch miss)"); }

        private void CleanupPipeNearTime(float time){ const float eps = 0.05f; for(int i=_activePipes.Count-1;i>=0;i--){ var ap=_activePipes[i]; if (Mathf.Abs(ap.time - time) <= eps){ if(ap.tr!=null) Destroy(ap.tr.gameObject); _activePipes.RemoveAt(i); break; } } }

        // Public API: initial mode suggestion based on presence of events
        public bool HasSwitchEvents => _events != null && _events.Count > 0;
        public AutoStart.LaneMode SuggestInitialLaneMode(AutoStart.LaneMode fallback){ return HasSwitchEvents ? initialModeWhenHasEvents : initialModeWhenNoEvents; }

        // Return the lane mode at a given visual time 't' based on past switch events
        public AutoStart.LaneMode GetLaneModeAtTime(float t){
            // Determine baseline initial mode
            var mode = HasSwitchEvents ? initialModeWhenHasEvents : initialModeWhenNoEvents;
            if (_events == null || _events.Count == 0) return mode;
            // Apply all switches up to time t
            for(int i=0;i<_events.Count;i++){
                var e = _events[i];
                if (e.time > t) break;
                // toggle on each event regardless of type (they represent a switch)
                mode = (mode == AutoStart.LaneMode.Center7) ? AutoStart.LaneMode.Edge7 : AutoStart.LaneMode.Center7;
            }
            return mode;
        }
    }
}
