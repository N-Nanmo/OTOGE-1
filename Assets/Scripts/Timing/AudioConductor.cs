using UnityEngine;

namespace RhythmGame.Timing{
    //楽曲の進行時間を管理するクラス
    //DSP時間使用
    public class AudioConductor : MonoBehaviour{
        [Header("Audio Source")]
        [SerializeField] private AudioSource audioSource;

        [Header("Schedule Setting")]
        [Tooltip("再生予約時間(秒)")]
        [SerializeField] private double scheduleLoadTime = 3;

        [Header("Debug(readonly")]
        [SerializeField] private double dspStartTime = -1;
        [SerializeField] private bool started = false;            // 予約済み
        [SerializeField] private bool playbackBegan = false;       // 実際に再生が開始した(=dspStartTime 経過)

        [Header("Pause (readonly")]
        [SerializeField] private bool paused = false;
        [SerializeField] private double pausedAccumulated = 0;
        [SerializeField] private double pauseStartDsp = -1;

        public bool Started => started && audioSource != null;
        public bool PlaybackBegan => playbackBegan; // 実際に音が鳴り始めたか
        public bool IsPaused => paused;

        public double SongTime {
            get {
                if (!started) return 0;
                double now = AudioSettings.dspTime;
                double totalPaused = pausedAccumulated;
                if(paused && pauseStartDsp >= 0) totalPaused += now - pauseStartDsp;
                double time = now - dspStartTime - totalPaused;
                if (time < 0) time = 0;
                return time;
            }
        }

        public AudioSource Source => audioSource;
        public event System.Action<bool> OnPauseChanged;
        public event System.Action OnScheduled;      // 予約が行われた直後
        public event System.Action OnPlaybackBegan;  // dspStartTime に到達し実際に再生が始まったタイミング

        private void Reset() {
            if(audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private void Update() {
            if (started && !playbackBegan) {
                if (AudioSettings.dspTime >= dspStartTime) {
                    playbackBegan = true;
                    OnPlaybackBegan?.Invoke();
                }
            }
        }

        /// <summary>
        /// 既存 scheduleLoadTime を用いて予約再生。
        /// </summary>
        public void PlayScheduled() {
            InternalPlayScheduled(scheduleLoadTime);
        }

        /// <summary>
        /// 外部から任意の遅延秒指定で予約再生。
        /// </summary>
        public void PlayScheduledAfter(double delaySeconds) {
            if (delaySeconds < 0) delaySeconds = 0;
            InternalPlayScheduled(delaySeconds);
        }

        private void InternalPlayScheduled(double delay) {
            if(audioSource == null) {
                Debug.LogError("[AudioConductor] AudioSource isn't set");
                return;
            }
            if(audioSource.clip == null) {
                Debug.LogError("[AudioConductor] AudioSource.clip isn't set");
                return;
            }
            if (started) return;

            dspStartTime = AudioSettings.dspTime + delay;
            audioSource.PlayScheduled(dspStartTime);
            started = true;
            playbackBegan = false;
            OnScheduled?.Invoke();
        }

        public void StopImmediate() {
            if(audioSource != null && audioSource.isPlaying) audioSource.Stop();
            started = false;
            playbackBegan = false;
            dspStartTime = -1;
            paused = false;
            pausedAccumulated = 0;
            pauseStartDsp = -1;
        }

        public void Pause(bool pause) {
            if (!started) return;

            if (paused == pause) return;

            if (pause) {
                if (!audioSource.isPlaying) {
                    Debug.LogWarning("[AudioConductor] AudioSource is not playing but paused state is true. Unpausing.");
                }
                audioSource.Pause();
                pauseStartDsp = AudioSettings.dspTime;
                paused = true;
            } else {
                double now = AudioSettings.dspTime;
                if (pauseStartDsp >= 0){
                    double delta = now - pauseStartDsp;
                    if(delta > 0) pausedAccumulated += delta;
                }
                pauseStartDsp = -1;
                audioSource.UnPause();
                paused = false;
            }
            OnPauseChanged?.Invoke(paused);
        }

        public void TogglePause() {
            Pause(!paused);
        }
    }
}