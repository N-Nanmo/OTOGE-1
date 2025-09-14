using UnityEngine;
using RhythmGame.Data.Chart;

namespace RhythmGame.Notes {
    /// <summary>
    /// 1個ノートの表示/スタイル適用。プール運用前提の軽量クラス。
    /// </summary>
    public class NoteView : MonoBehaviour {
        [Header("Runtime (readonly)")]
        [SerializeField] private int laneIndex;
        [SerializeField] private float noteTime; // 出現(視覚)/判定(秒)
        [SerializeField] private NoteType noteType;

        [Header("Style")]
        [SerializeField] private NoteVisualStyle visualStyle;
        [SerializeField] private MeshRenderer meshRenderer; // Mesh系
        [SerializeField] private SpriteRenderer spriteRenderer; // Sprite系(あれば)

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
        /// プールから復帰してデータと見た目を設定。
        /// </summary>
        public void Initialize(int lane, float time, NoteType type, NoteVisualStyle style) {
            laneIndex = lane;
            noteTime = time;
            noteType = type;
            visualStyle = style;
            ApplyStyle();
        }

        /// <summary>
        /// 必要に応じて状態をクリア(プール戻し前に呼ばれる)。
        /// </summary>
        public void ResetForPool() {
            // 今は特に無し
        }

        private void ApplyStyle() {
            // Rendererの取得
            if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>(true);
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            EnableRenderer(true);

            Color c;
            Vector3 scale;
            var baseMat = meshRenderer ? meshRenderer.sharedMaterial : (spriteRenderer ? spriteRenderer.sharedMaterial : null);

            if (visualStyle == null) {
                // フォールバック: 可視になる最低限
                c = (noteType == NoteType.HoldStart || noteType == NoteType.HoldEnd) ? new Color(1f,0.8f,0.2f,1f) : new Color(0f,1f,1f,1f);
                scale = new Vector3(0.6f, 0.2f, 0.6f);
                if (baseMat == null) baseMat = CreateFallbackMaterial();
                AssignMaterial(baseMat);
            } else {
                c = (noteType == NoteType.HoldStart || noteType == NoteType.HoldEnd) ? visualStyle.holdColor : visualStyle.baseColor;
                c.a = 1f; // 不透明に固定
                scale = visualStyle.baseScale;
                if (visualStyle.material != null) AssignMaterial(visualStyle.material);
                else if (baseMat == null) AssignMaterial(CreateFallbackMaterial());
            }

            // PropertyBlock で色適用 (MeshRenderer優先)。sharedMaterial の色は変更しない。
            if (meshRenderer != null){
                _mpb ??= new MaterialPropertyBlock();
                _mpb.SetColor(COLOR_PROP_ID, c);
                if (baseMat != null && baseMat.HasProperty(BASE_COLOR_ID)) _mpb.SetColor(BASE_COLOR_ID, c);
                meshRenderer.SetPropertyBlock(_mpb);
            }
            // SpriteRenderer の色(インスタンス毎)
            if (spriteRenderer != null){
                spriteRenderer.color = c;
            }

            // スケール適用
            transform.localScale = scale;
        }

        private void AssignMaterial(Material m){
            if (meshRenderer != null) meshRenderer.sharedMaterial = m;
            if (spriteRenderer != null) spriteRenderer.sharedMaterial = m;
        }

        private static Material CreateFallbackMaterial(){
            // URP Lit 優先、なければ Standard
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            return new Material(sh);
        }
    }
}
