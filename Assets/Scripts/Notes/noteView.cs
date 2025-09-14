using UnityEngine;
using RhythmGame.Data.Chart;

namespace RhythmGame.Notes {
    /// <summary>
    /// 1�m�[�g�̕\��/�X�^�C���K�p�B�v�[���^�p�O��̌y�ʃN���X�B
    /// </summary>
    public class NoteView : MonoBehaviour {
        [Header("Runtime (readonly)")]
        [SerializeField] private int laneIndex;
        [SerializeField] private float noteTime; // �o��(���o)/����(�b)
        [SerializeField] private NoteType noteType;

        [Header("Style")]
        [SerializeField] private NoteVisualStyle visualStyle;
        [SerializeField] private MeshRenderer meshRenderer; // Mesh�n
        [SerializeField] private SpriteRenderer spriteRenderer; // Sprite�n(�����)

        private static readonly int COLOR_PROP_ID = Shader.PropertyToID("_Color");
        private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
        private static readonly int SURFACE_PROP_ID = Shader.PropertyToID("_Surface"); // URP Lit surface (0=Opaque)
        private MaterialPropertyBlock _mpb;

        public int LaneIndex => laneIndex;
        public float NoteTime => noteTime;
        public NoteType Type => noteType;

        private void Awake() {
            if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>(true);
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            EnableRenderer(true);
        }

        private void EnableRenderer(bool enabled){
            if (meshRenderer != null) meshRenderer.enabled = enabled;
            if (spriteRenderer != null) spriteRenderer.enabled = enabled;
        }

        /// <summary>
        /// �v�[�����畜�A���ăf�[�^�ƌ����ڂ�ݒ�B
        /// </summary>
        public void Initialize(int lane, float time, NoteType type, NoteVisualStyle style) {
            laneIndex = lane;
            noteTime = time;
            noteType = type;
            visualStyle = style;
            ApplyStyle();
        }

        /// <summary>
        /// �K�v�ɉ����ď�Ԃ��N���A(�v�[���߂��O�ɌĂ΂��)�B
        /// </summary>
        public void ResetForPool() {
            // ���͓��ɖ���
        }

        private void ApplyStyle() {
            // Renderer�̎擾
            if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>(true);
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            EnableRenderer(true);

            Color c;
            Vector3 scale;
            var baseMat = meshRenderer ? meshRenderer.sharedMaterial : (spriteRenderer ? spriteRenderer.sharedMaterial : null);

            if (visualStyle == null) {
                // �t�H�[���o�b�N: ���ɂȂ�Œ��
                c = (noteType == NoteType.HoldStart || noteType == NoteType.HoldEnd) ? new Color(1f,0.8f,0.2f,1f) : new Color(0f,1f,1f,1f);
                scale = new Vector3(0.6f, 0.2f, 0.6f);
                if (baseMat == null) baseMat = CreateFallbackMaterial();
                AssignMaterial(baseMat);
            } else {
                c = (noteType == NoteType.HoldStart || noteType == NoteType.HoldEnd) ? visualStyle.holdColor : visualStyle.baseColor;
                c.a = 1f; // �s�����ɌŒ�
                scale = visualStyle.baseScale;
                if (visualStyle.material != null) AssignMaterial(visualStyle.material);
                else if (baseMat == null) AssignMaterial(CreateFallbackMaterial());
            }

            // PropertyBlock �ŐF�K�p (MeshRenderer�D��)�BsharedMaterial �̐F�͕ύX���Ȃ��B
            if (meshRenderer != null){
                _mpb ??= new MaterialPropertyBlock();
                _mpb.SetColor(COLOR_PROP_ID, c);
                if (baseMat != null && baseMat.HasProperty(BASE_COLOR_ID)) _mpb.SetColor(BASE_COLOR_ID, c);
                meshRenderer.SetPropertyBlock(_mpb);
            }
            // SpriteRenderer �̐F(�C���X�^���X��)
            if (spriteRenderer != null){
                spriteRenderer.color = c;
            }

            // �X�P�[���K�p
            transform.localScale = scale;
        }

        private void AssignMaterial(Material m){
            if (meshRenderer != null) meshRenderer.sharedMaterial = m;
            if (spriteRenderer != null) spriteRenderer.sharedMaterial = m;
        }

        private static Material CreateFallbackMaterial(){
            // URP Lit �D��A�Ȃ���� Standard
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            return new Material(sh);
        }
    }
}
