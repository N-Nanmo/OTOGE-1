using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RhythmGame.Timing {
    //�\���p�̎��Ԃ𐶐�����TimeDriver
    public class VisualTimeDriver : MonoBehaviour {
        [Header("Source(Time Logic")]
        [SerializeField] private AudioConductor conductor;

        [Header("Scaling")]
        [Tooltip("���o�\���Ɏg�p���鎞�Ԃ̔{��")]
        [SerializeField] private float timeScale = 1.0f;

        [Header("Smoothing")]
        [Tooltip("0=�����ɒǏ]")]
        [Range(0f, 0.5f)]
        [SerializeField] private float smoothing = 0.15f;

        [Tooltip("�⊮���ǂꂭ�炢�x�ꂽ��Ǐ]���邩(�b)")]
        [SerializeField] private float hardSnapThreshold = 0.35f;

        [Tooltip("�⊮1frame������̍ő�ω���(�b)(0=������)")]
        [SerializeField] private float maxCatchupPerFrame = 0f;

        [Header("Options")]
        [Tooltip("true: Time.unscaledDeltaTime / false: Time,deltaTime")]
        [SerializeField] private bool useUnscaledDeltaTime = true;

        [Tooltip("�t�����̕ω��������邩")]
        [SerializeField] private bool allowReverse = true;

        [Header("pause / Resume Behaviour")]
        [Tooltip("Pause�������ɕK�����X�i�b�v�������邩")]
        [SerializeField] private bool snapOnPause = true;
        [Tooltip("Pause��������ɉ�Frame�����Ǐ]���邩(0=���Ȃ�)")]
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
                double factor = 1.0 - System.Math.Exp(-lambda * dt);//1 - e^(-�Ƀ�t)�����̋z������
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
                //��������
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