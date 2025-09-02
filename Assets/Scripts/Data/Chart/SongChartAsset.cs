using UnityEngine;

namespace RhythmGame.Data.Chart {

    [CreateAssetMenu(menuName = "RhythmGame/Chart/SongChartAsset")]
    public class SongChartAsset : ScriptableObject {
        public AudioClip audioClip; //曲のオーディオクリップ
        public TextAsset jsonChart; //JSON形式のチャートデータ
        [HideInInspector] public ChartData runtimeChart; //実行時に使用するチャートデータ
    }
}
