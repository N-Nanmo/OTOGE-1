using UnityEngine;
using RhythmGame.Boot;

namespace RhythmGame.CameraSystem {
    /// <summary>
    /// AutoStart の LaneMode 変更イベントを受けて、所定のカメラ姿勢へスムーズ遷移する簡易コントローラ。
    /// 使い方:
    /// 1. シーンのカメラにアタッチ
    /// 2. AutoStart 参照を設定 (null なら自動検索)
    /// 3. Center7 / Edge7 用の目標 Transform (またはオフセット値) を設定
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class LaneModeCameraController : MonoBehaviour {
        [Header("References")] [SerializeField] private AutoStart autoStart;

        [Header("Targets (use either Transform targets OR offset values)")]
        [SerializeField] private Transform center7Target;
        [SerializeField] private Transform edge7Target;

        [Header("Fallback Offsets (used if target Transform is null)")]
        [SerializeField] private Vector3 centerPosition = new(0f, 0f, -7.5f);
        [SerializeField] private Vector3 centerEuler    = new(15f,   0f,  0f);
        [SerializeField] private Vector3 edgePosition   = new(0f, 0f, -7.5f);
        [SerializeField] private Vector3 edgeEuler      = new(-15f,  0f, 180f);

        [Header("Transition")]
        [SerializeField, Tooltip("補間にかける時間(秒)")] private float blendTime = 0.7f;
        [SerializeField, Tooltip("進行のスムージング曲線 (0..1)")] private AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);

        private Vector3 _startPos; private Quaternion _startRot; private Vector3 _targetPos; private Quaternion _targetRot;
        private float _t; private bool _blending;

        private void Awake(){ if(autoStart==null) autoStart = FindFirstObjectByType<AutoStart>(); }
        private void OnEnable(){ if(autoStart!=null) autoStart.OnLaneModeChanged += HandleModeChanged; InitializeFirstPose(); }
        private void OnDisable(){ if(autoStart!=null) autoStart.OnLaneModeChanged -= HandleModeChanged; }

        private void Update(){ if(_blending){ _t += Time.deltaTime / Mathf.Max(0.0001f, blendTime); float p = Mathf.Clamp01(_t); if(ease!=null) p = ease.Evaluate(p); transform.position = Vector3.Lerp(_startPos, _targetPos, p); transform.rotation = Quaternion.Slerp(_startRot, _targetRot, p); if(p>=1f) _blending=false; } }

        private void InitializeFirstPose(){ if(autoStart==null) return; ApplyModeImmediate(autoStart.CurrentLaneMode); }

        private void HandleModeChanged(AutoStart.LaneMode mode){ BeginBlendTo(mode); }

        private void BeginBlendTo(AutoStart.LaneMode mode){
            _startPos = transform.position; _startRot = transform.rotation; (_targetPos, _targetRot) = GetPose(mode); _t = 0f; _blending = true;
        }

        private void ApplyModeImmediate(AutoStart.LaneMode mode){ (_targetPos,_targetRot)=GetPose(mode); transform.SetPositionAndRotation(_targetPos,_targetRot); _blending=false; _t=1f; }

        private (Vector3, Quaternion) GetPose(AutoStart.LaneMode mode){
            if(mode == AutoStart.LaneMode.Center7){ if(center7Target!=null) return (center7Target.position, center7Target.rotation); return (centerPosition, Quaternion.Euler(centerEuler)); }
            else { if(edge7Target!=null) return (edge7Target.position, edge7Target.rotation); return (edgePosition, Quaternion.Euler(edgeEuler)); }
        }

        // Public API for manual triggering
        public void ForceReapply(){ if(autoStart!=null) ApplyModeImmediate(autoStart.CurrentLaneMode); }

        // New: blend with extra Z rotation delta (degrees) applied on top of target
        public void BeginBlendToWithExtraZ(AutoStart.LaneMode mode, float extraZDegrees){
            _startPos = transform.position; _startRot = transform.rotation; (Vector3 pos, Quaternion rot) = GetPose(mode); _targetPos = pos; _targetRot = rot * Quaternion.Euler(0f, 0f, extraZDegrees); _t = 0f; _blending = true;
        }

        // New: apply immediately with extra Z rotation
        public void ApplyImmediateWithExtraZ(AutoStart.LaneMode mode, float extraZDegrees){ (Vector3 pos, Quaternion rot) = GetPose(mode); transform.SetPositionAndRotation(pos, rot * Quaternion.Euler(0f,0f,extraZDegrees)); _blending=false; _t=1f; }
    }
}
