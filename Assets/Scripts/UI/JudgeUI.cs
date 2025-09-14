using UnityEngine;
using UnityEngine.UI; // For Text
#if TMP_PRESENT
using TMPro;
#endif
using RhythmGame.Boot;
using RhythmGame.Notes;

namespace RhythmGame.UI {
    /// <summary>
    /// 判定/コンボ/各種カウンタを表示する簡易UI。
    /// AutoStart(OnJudged)イベントを購読して更新。
    /// TextMeshPro を使いたい場合は TMP_PRESENT シンボルを定義し、対応フィールドに割り当ててください。
    /// </summary>
    public class JudgeUI : MonoBehaviour {
        [Header("References")]
        [SerializeField] private AutoStart autoStart; // シーン上の AutoStart (null なら自動探索)

        [Header("Text (UnityEngine.UI)")]
        [SerializeField] private Text perfectText;
        [SerializeField] private Text greatText;
        [SerializeField] private Text goodText;
        [SerializeField] private Text missText;
        [SerializeField] private Text comboText;
        [SerializeField] private Text lastJudgeText;

#if TMP_PRESENT
        [Header("Optional (TMP)")]
        [SerializeField] private TMP_Text perfectTMP;
        [SerializeField] private TMP_Text greatTMP;
        [SerializeField] private TMP_Text goodTMP;
        [SerializeField] private TMP_Text missTMP;
        [SerializeField] private TMP_Text comboTMP;
        [SerializeField] private TMP_Text lastJudgeTMP;
#endif

        [Header("Display Settings")]
        [SerializeField] private bool showZeroCombo = false; // 0 で空表示
        [SerializeField] private float lastJudgeFadeSeconds = 0.6f;
        [SerializeField] private Color perfectColor = new Color(1f, 0.95f, 0.4f);
        [SerializeField] private Color greatColor = new Color(0.6f, 0.9f, 1f);
        [SerializeField] private Color goodColor = new Color(0.7f, 1f, 0.7f);
        [SerializeField] private Color missColor = new Color(1f, 0.4f, 0.4f);
        [SerializeField] private Color defaultLastColor = Color.white;

        private float _lastJudgeTimer = -1f;
        private Color _lastJudgeBaseColor;

        private void Awake() {
            if (autoStart == null) autoStart = FindFirstObjectByType<AutoStart>();
            CacheInitialColor();
            RefreshAll();
        }

        private void OnEnable() {
            if (autoStart == null) autoStart = FindFirstObjectByType<AutoStart>();
            if (autoStart != null) autoStart.OnJudged += HandleJudged;
        }

        private void OnDisable() {
            if (autoStart != null) autoStart.OnJudged -= HandleJudged;
        }

        private void Update() {
            if (_lastJudgeTimer >= 0f && lastJudgeFadeSeconds > 0f) {
                _lastJudgeTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_lastJudgeTimer / lastJudgeFadeSeconds);
                float alpha = 1f - t;
                SetLastAlpha(alpha);
                if (t >= 1f) _lastJudgeTimer = -1f;
            }
        }

        private void HandleJudged(NotesMover.JudgmentType j, int lane, float delta, int combo) {
            // カウンタ更新 (AutoStart 側がすでに内部値更新)
            RefreshCounts();
            UpdateCombo(combo);
            ShowLastJudge(j, delta);
        }

        private void RefreshAll() {
            RefreshCounts();
            UpdateCombo(autoStart != null ? autoStart.CurrentCombo : 0);
            ClearLastJudge();
        }

        private void RefreshCounts() {
            if (autoStart == null) return;
            SetText(perfectText, $"Perfect: {autoStart.PerfectCount}");
            SetText(greatText,   $"Great: {autoStart.GreatCount}");
            SetText(goodText,    $"Good: {autoStart.GoodCount}");
            SetText(missText,    $"Miss: {autoStart.MissCount}");
#if TMP_PRESENT
            SetTMP(perfectTMP, $"Perfect: {autoStart.PerfectCount}");
            SetTMP(greatTMP,   $"Great: {autoStart.GreatCount}");
            SetTMP(goodTMP,    $"Good: {autoStart.GoodCount}");
            SetTMP(missTMP,    $"Miss: {autoStart.MissCount}");
#endif
        }

        private void UpdateCombo(int combo) {
            string text = (combo <= 0 && !showZeroCombo) ? "" : $"{combo} Combo";
            SetText(comboText, text);
#if TMP_PRESENT
            SetTMP(comboTMP, text);
#endif
        }

        private void ShowLastJudge(NotesMover.JudgmentType j, float delta) {
            string label = j.ToString();
            string deltaStr = $"Δ{delta:+0.000;-0.000}";
            string final = $"{label}  {deltaStr}";
            Color c = defaultLastColor;
            switch (j) {
                case NotesMover.JudgmentType.Perfect: c = perfectColor; break;
                case NotesMover.JudgmentType.Great:   c = greatColor; break;
                case NotesMover.JudgmentType.Good:    c = goodColor; break;
                case NotesMover.JudgmentType.Miss:    c = missColor; break;
            }
            SetText(lastJudgeText, final, c);
#if TMP_PRESENT
            SetTMP(lastJudgeTMP, final, c);
#endif
            _lastJudgeTimer = 0f;
            _lastJudgeBaseColor = c;
        }

        private void ClearLastJudge() {
            SetText(lastJudgeText, "");
#if TMP_PRESENT
            SetTMP(lastJudgeTMP, "");
#endif
            _lastJudgeTimer = -1f;
        }

        private void CacheInitialColor() {
            if (lastJudgeText != null) _lastJudgeBaseColor = lastJudgeText.color; else _lastJudgeBaseColor = defaultLastColor;
        }

        private void SetLastAlpha(float a) {
            if (lastJudgeText != null) {
                var c = _lastJudgeBaseColor; c.a = a; lastJudgeText.color = c;
            }
#if TMP_PRESENT
            if (lastJudgeTMP != null) { var c2 = _lastJudgeBaseColor; c2.a = a; lastJudgeTMP.color = c2; }
#endif
        }

        private static void SetText(Text t, string s, Color? col = null) { if (t == null) return; t.text = s; if (col.HasValue) t.color = col.Value; }
#if TMP_PRESENT
        private static void SetTMP(TMP_Text t, string s, Color? col = null) { if (t == null) return; t.text = s; if (col.HasValue) t.color = col.Value; }
#endif
    }
}
