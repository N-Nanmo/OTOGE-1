using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace RhythmGame.Data.Chart {
    //JSONから読み込む用
    [Serializable]
    public class ChartData {
        public float offset; //曲のオフセット(秒)
        public float bpm; //曲のBPM
        public int lanes = 14; //レーン数(デフォルト14)
        public float approachRate = 2.0f; //ノーツの出現速度
        public string chartId;

        public List<NoteData>[] notes = new List<NoteData>[14]; //ノーツデータのリスト(常に非null)

        public void Sort() {
            if (notes == null) return;
            int len = notes.Length;
            for(int i=0; i<len; i++) {
                if (notes[i] == null) continue;
                notes[i].Sort((a, b) => a.time.CompareTo(b.time));
            }
        }
    }
}
