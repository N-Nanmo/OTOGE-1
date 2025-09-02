using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace RhythmGame.Data.Chart {
    //JSON����ǂݍ��ޗp
    [Serializable]
    public class ChartData {
        public string songId; //�Ȃ�ID
        public float offset; //�Ȃ̃I�t�Z�b�g(�b)
        public float bpm; //�Ȃ�BPM
        public int lanes = 7; //���[����
        public float approachRate = 2.0f; //�m�[�c�̏o�����x
        public string chartId;

        public List<NoteData> notes; //�m�[�c�f�[�^�̃��X�g

        public void Sort() {
            notes.Sort((a, b) => a.time.CompareTo(b.time));
        }
    }
}
