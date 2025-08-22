using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class Main : MonoBehaviour
{
    //データ構造
    //time(秒), lane, type(0=Tap,1=longNoteStart,2=longNoteEnd)
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

    public TextAsset charttext;         //譜面テキストの参照
    public AudioSource musicSource;     //曲の参照

    public GameObject notePrefab_Red;   //皿(レーン0)のPrefab
    public GameObject notePrefab_Grey;  //レーン1, 3, 5, 7のPrefab
    public GameObject notePrefab_Blue;  //レーン2, 4, 6のPrefab

    public float scrollSpeed = 6.0f;    //ノーツのスクロール速度(仮)
    public float hitLineY = -3.3f;      //ヒットラインのY座標(仮)
    public float spownTopY = 4.0f;      //ノーツが生成されるY座標(仮)

    public float perfectTime = 0.01667f; //Perfect判定の時間(60fps,約1フレーム)
    public float greatTime = 0.03333f;   //Great判定の時間(60fps,約2フレーム)
    public float goodTime = 0.11667f;    //Good判定の時間(60fps,約7フレーム)

    private List<NoteData> notes;       //ノーツのリスト(time昇順)
    private double SongStartTime;       //曲が始まった時間
    private bool SongStarted;           //曲が始まったか

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
