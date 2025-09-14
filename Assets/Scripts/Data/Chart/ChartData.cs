using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace RhythmGame.Data.Chart {
    //JSON����ǂݍ��ޗp
    [Serializable]
    public class ChartData {
        public float offset; //�Ȃ̃I�t�Z�b�g(�b)
        public float bpm; //�Ȃ�BPM
        public int lanes = 14; //���[����(�f�t�H���g14)
        public float approachRate = 2.0f; //�m�[�c�̏o�����x
        public string chartId;

        public List<NoteData>[] notes = new List<NoteData>[14]; //�m�[�c�f�[�^�̃��X�g(��ɔ�null)

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
