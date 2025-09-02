using UnityEngine;

namespace RhythmGame.Timing {
    //楽曲の進行時間を管理するクラス
    //DSP時間使用
    public class AudioConductor : MonoBehaviour {
        [Header("Audio Source")]
        [SerializeField] private AudioSource audioSource;

        [Header("Schedule Setting")]
        [Tooltip("再生予約時間(秒)")]
        [SerializeField] private double scheduleLoadTime = 0.15;

        [Header("Debug(readonly")]
        [SerializeField] private double dspStartTime = -1;
        [SerializeField] private bool started = false;

        [Header("Pause (readonly")]
        [SerializeField] private bool paused = false;
        [SerializeField] private double pausedAccumulated = 0;
        [SerializeField] private double pauseStartDsp = -1;

        public bool Started => started && audioSource != null;
        public bool IsPaused => paused;
        //曲経過秒
        public double SongTime
        {
            get
            {
                if (!started) return 0;
                double now = AudioSettings.dspTime;
                double totalPaused = pausedAccumulated;
                if(paused && pauseStartDsp >= 0) {
                    totalPaused += now - pauseStartDsp;
                }
                double time = now - dspStartTime - totalPaused;
                if (time < 0) time = 0;
                return time;
            }
        }

        public AudioSource Source => audioSource;
        public event System.Action<bool> OnPauseChanged;

        private void Reset() {
            if(audioSource == null) {
                audioSource = GetComponent<AudioSource>();
            }
        }

        public void PlayScheduled() {
            if(audioSource == null) {
                Debug.LogError("[audioConductor] Audiosource isn't set");
                return;
            }
            if(audioSource.clip == null) {
                Debug.LogError("[audioConductor] Audiosource.clip isn't set");
                return;
            }
            if (started) return;

            dspStartTime = AudioSettings.dspTime + scheduleLoadTime;
            audioSource.PlayScheduled(dspStartTime);
            started = true;
        }

        public void StopImmediate() {
            if(audioSource != null && audioSource.isPlaying) {
                audioSource.Stop();
            }
            started = false;
            dspStartTime = -1;
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