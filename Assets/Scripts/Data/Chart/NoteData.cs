using UnityEngine;
using System;
    
namespace RhythmGame.Data.Chart {
    [Serializable]
    public struct NoteData {
        public float time; //�ȊJ�n����̎���(�b)
        public int laneIndex;    //���[���ԍ�
        public NoteType type; //�m�[�c�̎��
        public float duration; //Hold�m�[�c�̒���(�b)
    }

}
