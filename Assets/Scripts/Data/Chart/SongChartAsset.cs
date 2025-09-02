using UnityEngine;

namespace RhythmGame.Data.Chart {

    [CreateAssetMenu(menuName = "RhythmGame/Chart/SongChartAsset")]
    public class SongChartAsset : ScriptableObject {
        public AudioClip audioClip; //�Ȃ̃I�[�f�B�I�N���b�v
        public TextAsset jsonChart; //JSON�`���̃`���[�g�f�[�^
        [HideInInspector] public ChartData runtimeChart; //���s���Ɏg�p����`���[�g�f�[�^
    }
}
