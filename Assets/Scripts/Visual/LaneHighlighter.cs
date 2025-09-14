using UnityEngine;

namespace RhythmGame.Visual {
    /// <summary>
    /// �~��/�~���󃁃b�V��(1��)�ɑ΂��ă��[�����̔������x���V�F�[�_�֓n���A����������R���|�[�l���g�B
    /// Shader: Custom/URP/LaneHighlight
    /// </summary>
    [ExecuteAlways]
    public class LaneHighlighter : MonoBehaviour {
        [Header("Target")]
        [SerializeField] private Renderer targetRenderer;

        [Header("Lane Settings")]
        [SerializeField, Range(1,32)] private int laneCount = 14;
        [Tooltip("�C���f�b�N�X�����𔽓]���� (Shader _InvertDirection)")]
        [SerializeField] private bool invertDirectionFlag = true;
        [Tooltip("���[����̉�]�I�t�Z�b�g(0-1 �����)�BShader _AngleOffset")]
        [SerializeField, Range(0f,1f)] private float angleOffset = 0f;
        [Tooltip("���[���ԍ��S�̂��V�t�g(�P��:���[����)�B��/���ŉ�]�BShader _LaneShift")]
        [SerializeField, Range(-16f,16f)] private float laneShift = -0.5f;
        [Tooltip("���[�����E�t�F�U�[(0..0.2 ���x)�BShader _EdgeFeather")]
        [SerializeField, Range(0f,0.2f)] private float edgeFeather = 0f;

        [Header("Highlight Dynamics")]
        [SerializeField] private float decayPerSecond = 3f;
        [SerializeField] private Color highlightColor = Color.white;
        [SerializeField, Range(0f,10f)] private float emissionStrength = 2f;
        [SerializeField] private bool useUnscaledTime = false;

        // �����o�b�t�@
        private float[] _intensity;
        private MaterialPropertyBlock _mpb;

        // Shader property IDs
        private static readonly int ID_LaneHighlight     = Shader.PropertyToID("_LaneHighlight");
        private static readonly int ID_LaneCount         = Shader.PropertyToID("_LaneCount");
        private static readonly int ID_HighlightColor    = Shader.PropertyToID("_HighlightColor");
        private static readonly int ID_EmissionStrength  = Shader.PropertyToID("_EmissionStrength");
        private static readonly int ID_AngleOffset       = Shader.PropertyToID("_AngleOffset");
        private static readonly int ID_LaneShift         = Shader.PropertyToID("_LaneShift");
        private static readonly int ID_InvertDirection   = Shader.PropertyToID("_InvertDirection");
        private static readonly int ID_EdgeFeather       = Shader.PropertyToID("_EdgeFeather");

        public bool InvertDirection => invertDirectionFlag;

        private void OnEnable() {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            EnsureBuffers();
            Apply(true);
        }

        private void OnValidate() {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            laneCount = Mathf.Clamp(laneCount, 1, 32);
            EnsureBuffers();
            // �����z��T�C�Y���قȂ�Ƃ��̂ݍĊm��
            if (_intensity.Length != laneCount) {
                var old = _intensity;
                _intensity = new float[laneCount];
                if (old != null) {
                    int copy = Mathf.Min(old.Length, _intensity.Length);
                    for (int i = 0; i < copy; i++) _intensity[i] = old[i];
                }
            }
            Apply(true);
        }

        private void EnsureBuffers() {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (_intensity == null || _intensity.Length == 0) _intensity = new float[laneCount];
        }

        private void Update() {
            if (_intensity == null) return;
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt <= 0f) return;
            bool dirty = false;
            float decay = decayPerSecond * dt;
            for (int i = 0; i < _intensity.Length; i++) {
                if (_intensity[i] > 0f) {
                    float v = _intensity[i] - decay;
                    if (v < 0f) v = 0f;
                    if (Mathf.Abs(v - _intensity[i]) > 0.0001f) { _intensity[i] = v; dirty = true; }
                }
            }
            if (dirty) Apply(false);
        }

        /// <summary>�w�背�[�������x�œ_�� (0..1)�B������苭���ꍇ�̂ݏ㏑���B</summary>
        public void Pulse(int lane, float strength = 1f) {
            if (_intensity == null || lane < 0 || lane >= laneCount) return;
            EnsureBuffers();
            strength = Mathf.Clamp01(strength);
            if (strength > _intensity[lane]) { _intensity[lane] = strength; Apply(false); }
        }

        /// <summary>�S���[�����N���A�B</summary>
        public void ClearAll() {
            if (_intensity == null) return;
            for (int i = 0; i < _intensity.Length; i++) _intensity[i] = 0f;
            Apply(true);
        }

        private void Apply(bool force) {
            if (targetRenderer == null) return;
            EnsureBuffers();
            targetRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloatArray(ID_LaneHighlight, _intensity);
            _mpb.SetInt(ID_LaneCount, laneCount);
            _mpb.SetColor(ID_HighlightColor, highlightColor);
            _mpb.SetFloat(ID_EmissionStrength, emissionStrength);
            _mpb.SetFloat(ID_AngleOffset, angleOffset);
            _mpb.SetFloat(ID_LaneShift, laneShift);
            _mpb.SetFloat(ID_EdgeFeather, edgeFeather);
            _mpb.SetFloat(ID_InvertDirection, invertDirectionFlag ? 1f : 0f);
            targetRenderer.SetPropertyBlock(_mpb);
        }

        // Public setters for runtime adjustments
        public void SetLaneShift(float shift) { laneShift = shift; Apply(false); }
        public void SetAngleOffset(float offset01) { angleOffset = Mathf.Repeat(offset01, 1f); Apply(false); }
        public void SetInvert(bool inv) { invertDirectionFlag = inv; Apply(false); }
        public void SetEdgeFeather(float feather) { edgeFeather = Mathf.Clamp(feather, 0f, 0.2f); Apply(false); }

        public int LaneCount => laneCount;
        public float[] RawIntensity => _intensity;
    }
}
