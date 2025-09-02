using UnityEngine;
using System;
    
namespace RhythmGame.Data.Chart {
    [Serializable]
    public struct NoteData {
        public float time; //曲開始からの時間(秒)
        public int laneIndex;    //レーン番号
        public NoteType type; //ノーツの種類
        public float duration; //Holdノーツの長さ(秒)
    }

}
