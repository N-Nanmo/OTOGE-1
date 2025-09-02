using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace RhythmGame.Data.Chart {
    //JSONから読み込む用
    [Serializable]
    public class ChartData {
        public string songId; //曲のID
        public float offset; //曲のオフセット(秒)
        public float bpm; //曲のBPM
        public int lanes = 7; //レーン数
        public float approachRate = 2.0f; //ノーツの出現速度
        public string chartId;

        public List<NoteData> notes; //ノーツデータのリスト

        public void Sort() {
            notes.Sort((a, b) => a.time.CompareTo(b.time));
        }
    }
}
