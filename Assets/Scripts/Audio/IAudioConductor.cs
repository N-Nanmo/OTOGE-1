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

        event Action<int> OnBeat;//îè
        event Action<int> OnBar;//è¨êﬂ

        void Preopare(float bpm, AudioClip clip, float offset);
        void Play();
        void Stop();
        void SetBpm(float bpm);
    }
}