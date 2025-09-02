using UnityEngine;
using System;

namespace Rhythmgame.audio {
    [DisallowMultipleComponent]
    public class Conductor : MonoBehaviour {
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private float bpm = 120f;
        [SerializeField] private float startDelay = 0.05f;//安定用ディレイ
        [SerializeField, Tooltip("グローバルな譜面オフセット(秒)遅らせる")] private float globaloffset = 0f;

        private double dspSongStart;
        private bool started;
        private int lastEmittedBeat = -1;
        private int beatsPerBar = 4;

        public event Action<int> OnBeat;
        public event Action<int> OnBar;
    }
}