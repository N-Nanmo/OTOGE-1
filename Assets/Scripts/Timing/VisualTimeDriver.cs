using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RhythmGame.Timing {
    //•\Ž¦—p‚ÌŽžŠÔ‚ð¶¬‚·‚éTimeDriver
    public class VisualTimeDriver : MonoBehaviour {
        [Header("Source(Time Logic")]
        [SerializeField] private AudioConductor conductor;

        [Header("Scaling")]
        [Tooltip("Ž‹Šo•\Ž¦‚ÉŽg—p‚·‚éŽžŠÔ‚Ì”{—¦")]
        [SerializeField] private float timeScale = 1.0f;

        [Header("Smoothing")]
        [Tooltip("0=‘¦À‚É’Ç]")]
        [Range(0f, 0.5f)]
        [SerializeField] private float smoothing = 0.15f;

        [Tooltip("•âŠ®‚ª‚Ç‚ê‚­‚ç‚¢’x‚ê‚½‚ç’Ç]‚·‚é‚©(•b)")]
        [SerializeField] private float hardSnapThreshold = 0.35f;

        [Tooltip("•âŠ®1frame‚ ‚½‚è‚ÌÅ‘å•Ï‰»—Ê(•b)(0=–³§ŒÀ)")]
        [SerializeField] private float maxCatchupPerFrame = 0f;

        [Header("Options")]
        [Tooltip("true: Time.unscaledDeltaTime / false: Time,deltaTime")]
        [SerializeField] private bool useUnscaledDeltaTime = true;

        [Tooltip("‹t•ûŒü‚Ì•Ï‰»‚ð‹–‰Â‚·‚é‚©")]
        [SerializeField] private bool allowReverse = true;

        [Header("pause / Resume Behaviour")]
        [Tooltip("Pause“¯ŠúŽž‚É•K‚¸‘¦ƒXƒiƒbƒv“¯Šú‚·‚é‚©")]
        [SerializeField] private bool snapOnPause = true;
        [Tooltip("Pause‰ðœ’¼Œã‚É‰½Frame‚ª‘¦’Ç]‚·‚é‚©(0=‚µ‚È‚¢)")]
        [SerializeField] private int resumeBoostFrames = 0;

        [Header("Debug Keys")]
        [SerializeField] private bool enableDebugKeys = true;

        [SerializeField] private KeyCode slowerKey = KeyCode.LeftBracket; // '['
        [SerializeField] private KeyCode fasterKey = KeyCode.RightBracket; // ']'
        [SerializeField] private KeyCode pauseToggleKey = KeyCode.P;
        [SerializeField] private KeyCode snapKey = KeyCode.BackQuote; // '`'

        [Header("Runtime (Readonly")]
        [SerializeField] private double visualTime = 0;
        [SerializeField] private int resumeBoostCounter = 0;
        [SerializeField] private bool wasPausedLastFrame = false;

        public double VisualTime => visualTime;
        public float TimeScale => timeScale;

        private double _lastSourceTime = 0;

        private void Awake() {
            if (conductor == null) {
                conductor = Object.FindFirstObjectByType<AudioConductor>();
            }
        }

        private void Update() {
            if(conductor == null || !conductor.Started) {
                visualTime = 0;
                wasPausedLastFrame = false;
                return;
            }

            HandleDebugKeys();

            bool isPaused = conductor.IsPaused;

            if(wasPausedLastFrame && !isPaused) {
                if (snapOnPause) {
                    visualTime = conductor.SongTime * timeScale;
                }
                if(resumeBoostFrames > 0) {
                    resumeBoostCounter = resumeBoostFrames;
                }
            }

            if (isPaused) {
                wasPausedLastFrame = true;
                return;
            }

            if(resumeBoostCounter > 0) {
                visualTime = conductor.SongTime * timeScale;
                resumeBoostCounter--;
                wasPausedLastFrame = false;
                return;
            }

            double sourceTime = conductor.SongTime;

            float effectiveScale = (!allowReverse && timeScale < 0f) ? 0f : timeScale;

            double target = sourceTime * effectiveScale;

            double diff = target - visualTime;

            if(Mathf.Abs((float) diff) > hardSnapThreshold) {
                visualTime = target;
                _lastSourceTime = sourceTime;
                return;
            }

            if(smoothing <= 0f) {
                visualTime = target;
            } else {
                float dt = useUnscaledDeltaTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float lambda = Mathf.Max(0.0001f, smoothing * 60f);
                double factor = 1.0 - System.Math.Exp(-lambda * dt);//1 - e^(-ƒÉƒ¢t)·•ª‚Ì‹zŽûŠ„‡
                double step = diff * factor;

                if(maxCatchupPerFrame > 0) {
                    double limit = maxCatchupPerFrame;
                    if (step > limit) step = limit;
                    else if (step < -limit) step = -limit;
                }

                visualTime += step;
            }
            _lastSourceTime = sourceTime;
        }

        private void HandleDebugKeys() {
            if (!enableDebugKeys) return;

            if (Input.GetKeyDown(pauseToggleKey)) {
                conductor?.TogglePause();
            }
            if(Input.GetKeyDown(snapKey) && conductor != null) {
                //‹­§“¯Šú
                visualTime = conductor.SongTime * timeScale;
            }
            if (Input.GetKeyDown(slowerKey)) {
                timeScale *= 0.5f;
                if (Mathf.Abs(timeScale) < 0.01f) timeScale = 0.01f * Mathf.Sign(timeScale);
            }
            if (Input.GetKeyDown(fasterKey)) {
                timeScale *= 2f;
                if (Mathf.Abs(timeScale) > 8f) timeScale = 8f * Mathf.Sign(timeScale);
            }
        }

        public void SetTimeScale(float newScale) {
            timeScale = newScale;
        }

        public void ForceSnap() {
            if(conductor != null) {
                visualTime = conductor.SongTime * timeScale;
            }
        }
    }
}