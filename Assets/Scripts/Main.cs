using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class Main : MonoBehaviour
{
    //�f�[�^�\��
    //time(�b), lane, type(0=Tap,1=longNoteStart,2=longNoteEnd)
    public readonly struct NoteData {
        public readonly float time;
        public readonly int lane;
        public readonly int type;
        public NoteData(float time, int lane, int type) {
            this.time = time;
            this.lane = lane;
            this.type = type;
        }
    }

    public TextAsset charttext;         //���ʃe�L�X�g�̎Q��
    public AudioSource musicSource;     //�Ȃ̎Q��

    public GameObject notePrefab_Red;   //�M(���[��0)��Prefab
    public GameObject notePrefab_Grey;  //���[��1, 3, 5, 7��Prefab
    public GameObject notePrefab_Blue;  //���[��2, 4, 6��Prefab

    public float scrollSpeed = 6.0f;    //�m�[�c�̃X�N���[�����x(��)
    public float hitLineY = -3.3f;      //�q�b�g���C����Y���W(��)
    public float spownTopY = 4.0f;      //�m�[�c�����������Y���W(��)

    public float perfectTime = 0.01667f; //Perfect����̎���(60fps,��1�t���[��)
    public float greatTime = 0.03333f;   //Great����̎���(60fps,��2�t���[��)
    public float goodTime = 0.11667f;    //Good����̎���(60fps,��7�t���[��)

    private List<NoteData> notes;       //�m�[�c�̃��X�g(time����)
    private double SongStartTime;       //�Ȃ��n�܂�������
    private bool SongStarted;           //�Ȃ��n�܂�����

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private List<NoteData> NotesLoader(TextAsset charttext) {
        var list = new List<NoteData>();
        if (!charttext) return list;

        var lines = charttext.text.Split("\n");
        foreach(var raw in lines) {
            var line = raw.Trim();
        }
    }
}
