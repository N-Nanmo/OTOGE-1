using UnityEngine;
using System;

namespace RhythmGame.Timing {
    public interface IAudioConductor {
        float CurrentBpm { get; }
        float songTime { get; }
        float RawSongTime { get; }
        bool Started { get; }
        float SecondsPerBeat { get; }
        float BeatFloat { get; }

        event Action<int> OnBeat;//��
        event Action<int> OnBar;//����

        void Preopare(float bpm, AudioClip clip, float offset);
        void Play();
        void Stop();
        void SetBpm(float bpm);
    }
}