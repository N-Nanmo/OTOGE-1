using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Globalization;
using System;
using UnityEditor;
using System.Runtime.CompilerServices;
using UnityEditor.PackageManager;
using UnityEngine.Rendering;

public class Main : MonoBehaviour
{
    //定数
    private const int TicksPerMeasure = 3600;                           //1小節あたりのTick数
    private const int BeatsPerMeasure = 4;                              //1小節あたりの拍数(4/4)
    private const int TicksPerBeat = TicksPerMeasure / BeatsPerMeasure;    //1拍あたりのTick数
    private const double DefaultBPM = 120.0;

    //BMS レーン変換(0=皿...7=7鍵)
    //1P: 11 12 13 14 15 17 18 19 / 16=皿
    private static readonly Dictionary<int, int> ChannelToLane = new() {
        {16, 0}, //皿
        {11, 1},
        {12, 2},
        {13, 3},
        {14, 4},
        {15, 5},
        {18, 6},
        {19, 7},
        {17, 8}
    };

    private static readonly Dictionary<int, float> LaneToX = new() {
        {0, -7.786f},
        {1, -6.991f},
        {2, -6.489f},
        {3, -5.991f},
        {4, -5.484f},
        {5, -4.983f},
        {6, -4.476f},
        {7, -3.962f},
        {8, -7.786f}
    };

    public enum ScrollSpeedMode {
        ConstantSpeed,                  //一定速度でスクロール
        VisibleTime,                    //可視秒数指定よるスクロール
        VisibleBeats                    //可視ビート数指定によるスクロール
    }

    //public変数
    [Header("Chart / Audio")]
    public TextAsset charttext;         //譜面テキストの参照
    public AudioSource musicSource;     //曲の参照
    public double AudioLoadTime = 0.1;   //曲のロードにかかる時間(秒)
    public float userOffsetSec = 0.0f; //ユーザーオフセット(秒)

    [Header("Note Prefabs")]
    public GameObject notePrefab_Scratch;   //皿(レーン0)のPrefab
    public GameObject notePrefab_Odd;  //レーン1, 3, 5, 7のPrefab
    public GameObject notePrefab_Even;  //レーン2, 4, 6のPrefab

    [Header("Layout")]
    public float scrollSpeed = 6.0f;    //ノーツのスクロール速度(仮)
    public float hitLineY = -3.3f;      //ヒットラインのY座標(仮)
    public float spownTopY = 4.0f;      //ノーツが生成されるY座標(仮)

    [Header("Timings")]
    public float perfectTime = 0.01667f;//Perfect判定の時間(60fps,約1フレーム)
    public float greatTime = 0.03333f;  //Great判定の時間(60fps,約2フレーム)
    public float goodTime = 0.11667f;   //Good判定の時間(60fps,約7フレーム)
    public float missTime = 0.15000f;   //Miss判定の時間(60fps,約9フレーム)

    [Header("Input")]
    public KeyCode[] laneKeys;          //各レーンのキー割り当て(0=皿...7=7鍵)
    public bool KeepsCombo = true;

    [Header("Score")]
    public int scoreMax = 1000000;          //最大スコア
    public float scoreGreatFactor = 0.95f;  //Great時のスコア係数
    public float scoreGoodFactor = 0.80f;   //Good時のスコア係数

    [Header("Scroll Speed Mode")]
    public ScrollSpeedMode speedMode = ScrollSpeedMode.ConstantSpeed;   //スクロール速度モード
    [Tooltip("可視秒数指定モードVisibleTimeの時のみ使用")]
    public float visibleTime = 2.0f;    //可視秒数指定モードの可視秒数
    [Tooltip("可視ビート数指定モードVisibleBeatsの時のみ使用")]
    public float visibleBeats = 4.0f;   //可視ビート数指定モードの可視ビート数
    public float hiSpeed = 1.0f;        //HiSpeed倍率

    [Header("WAV")]
    [Tooltip("#WAVxxで指定されたWAVファイルをResourceからロード")]
    public bool AutoLoadWavFromResource = true;
    [Tooltip("同時発音用SFXAusioSourceプールの数")]
    public int SFXPoolSize = 16;
    [Tooltip("Wavファイル未ロード時に警告")]
    public bool warningMissingWav = true;

    //データ構造
    //time(秒), lane, type(0=Tap,1=longNoteStart,2=longNoteEnd)
    public readonly struct NoteData {
        public readonly int tick;
        public readonly float time;
        public readonly int lane;
        public readonly int type;
        public readonly int wavId;
        public NoteData(int tick, float time, int lane, int type, int wavId) {
            this.tick = tick;
            this.time = time;
            this.lane = lane;
            this.type = type;
            this.wavId = wavId;
        }
    }

    private class NoteView {
        public GameObject obj;    //ノーツのGameObject
        public SpriteRenderer sr;
        public NoteData data;
        public bool judged;
    }
    private struct BPMSegment {
        public int startTick;
        public double startTime; //秒
        public double bpm;
    }

    private struct BPMChange {
        public int tick;
        public double BPM;
    }

    //private変数
    private List<NoteData> notes;       //ノーツのリスト(time昇順)
    private List<NoteView> noteViews;   //ノーツの表示オブジェクトのリスト
    private double dspMusicStartTime;   //曲が始まった時間
    private bool musicStarted;          //曲が始まったか

    private int combo;
    private int maxCombo;
    private long score;
    private int baseScorePerNote;

    private readonly Dictionary<int, double> judgeCounts = new();
    //Wavファイル
    private readonly Dictionary<int, AudioClip> wavClips = new();
    private readonly Dictionary<int, string> wavNames = new();
    //SFX発音用プール
    private readonly List<AudioSource> sfxPool = new();
    private int sfxPoolCounter = 0;

    private double frameDeltaAvg, frameDeltaMax;
    private int frameSampleCount;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Application.targetFrameRate = 60;
        if (!musicSource) musicSource = GetComponent<AudioSource>();

        //キー割り当ての数チェック
        int needKeys = 8;
        if(laneKeys.Length != needKeys) {
            Debug.LogError("レーン数とキー割り当ての数が一致しない");
        }
        //SFXプール初期化
        InitSfxPool();
        //譜面解析
        notes = ParseBMSAndBuildNotes(charttext);
        baseScorePerNote = (notes.Count > 0) ? (scoreMax / notes.Count) : 0;
        //ノーツ生成
        InstansitiateAllNote();
        ScheduleMusic();
    }

    // Update is called once per frame
    void Update()
    {
        double d = Time.unscaledDeltaTime;
        frameSampleCount++;
        frameDeltaAvg += (d - frameDeltaAvg) / frameSampleCount;
        if (d > frameDeltaMax) frameDeltaMax = d;

        if (!musicStarted) return;
        double songTime = GetSongTime();
        UpdateNotePositions(songTime);

    }

    void OnGUI() {
        GUI.Label(new Rect(10, 10, 320, 60),
            $"Δavg:{frameDeltaAvg * 1000:0.00}ms Δmax:{frameDeltaMax * 1000:0.00}ms FPS~{1.0 / Time.unscaledDeltaTime:0.0}");
    }

    //現在BPM(簡易)
    private double GetCurrentBPM(double songTime) => currentBpmCache;
    private double currentBpmCache = DefaultBPM;

    //スクロール速度算出
    private float GetScrollSpeed(double songTime) {
        switch (speedMode) {
            case ScrollSpeedMode.ConstantSpeed:
                return scrollSpeed;
            case ScrollSpeedMode.VisibleTime:
                return Mathf.Abs(spownTopY - hitLineY) / Mathf.Max(0.1f, visibleTime);
            case ScrollSpeedMode.VisibleBeats: {
                double BPMEff = GetCurrentBPM(songTime) * Math.Max(0.01, hiSpeed);
                return (float)(Mathf.Abs(spownTopY - hitLineY) * BPMEff / (visibleBeats * 60.0));
            }
        }
        return scrollSpeed;
    }

    //ノーツ位置更新
    private void UpdateNotePositions(double songTime) {
        float v = GetScrollSpeed(songTime);
        float songTimeF = (float)songTime;

        foreach(var nv in noteViews) {
            if (nv.judged) continue;
            //残りの時間
            float remainingTime = nv.data.time - songTimeF;

            float y;
            if(speedMode == ScrollSpeedMode.VisibleTime) {
                if(remainingTime > visibleTime) {
                    y = spownTopY;
                } else {
                    y = hitLineY + remainingTime * v;
                }
            }else if(speedMode == ScrollSpeedMode.VisibleBeats){
                double BPMEff = GetCurrentBPM(songTime) * Math.Max(0.01, hiSpeed);
                double remainingBeats = remainingTime * (BPMEff / 60.0);
                if(remainingBeats > visibleBeats) {
                    y = spownTopY;
                } else {
                    float unitsPerBeat = Mathf.Abs(spownTopY - hitLineY) / visibleBeats;
                    y = hitLineY + (float)remainingBeats * unitsPerBeat;
                }
            } else {
                y = hitLineY + remainingTime * v;
            }

            var p = nv.obj.transform.position;
            p.x = LaneToX[nv.data.lane];
            p.y = y;
            nv.obj.transform.position = p;
        }
    }

    //時間
    private double GetSongTime() => musicStarted ? AudioSettings.dspTime - dspMusicStartTime : 0.0;
    private void ScheduleMusic() {
        dspMusicStartTime = AudioSettings.dspTime + AudioLoadTime;
        musicSource.PlayScheduled(dspMusicStartTime);
        musicStarted = true;
    }

    //ノーツ生成
    private void InstansitiateAllNote() {
        noteViews = new List<NoteView>(notes.Count);
        GameObject prefab;
        foreach(var nt in notes) {
            if(nt.lane == 0) {
                prefab = notePrefab_Scratch;
            } else {
                if(nt.lane % 2 == 0 && nt.lane != 17) {
                    prefab = notePrefab_Even;
                } else {
                    prefab = notePrefab_Odd;
                }
            }

            if (!prefab) {
                Debug.LogError("ノーツのPrefabが指定されていない");
            }

            var obj = Instantiate(prefab, transform);
            obj.name = $"Note_L{nt.lane}_t{nt.time}";//秒数は小数点以下3桁まで
            var sr = obj.GetComponent<SpriteRenderer>();
            obj.transform.position = new Vector3(LaneToX[nt.lane], spownTopY, -1f);
            noteViews.Add(new NoteView {
                data = nt,
                obj = obj,
                sr = sr,
                judged = false
            });
        }
    }

    //SFX audioSourceプール
    private void InitSfxPool() {
        for(int i=0; i<SFXPoolSize; i++) {
            var temp = gameObject.AddComponent<AudioSource>();
            temp.playOnAwake = false;   //再生開始時に自動で再生しない
            temp.loop = false;          //ループしない
            temp.spatialBlend = 0.0f;   //距離を考慮しない
            sfxPool.Add(temp);
        }
    }

    //譜面解析
    private List<NoteData> ParseBMSAndBuildNotes(TextAsset text) {
        var result = new List<NoteData>();
        if (!text) {
            Debug.LogError("譜面テキストが指定されていない");
            return result;
        }

        var lines = text.text.Replace("\r\n", "\n").Split("\n");

        double baseBPM = DefaultBPM;
        double offsetSec = 0.0;

        //拡張BPMマップ
        var extBPMMap = new Dictionary<string, double>();

        //小節の長さ可変
        var measureFactors = new Dictionary<int, double>();

        //BPM変化
        var BPMChanges = new List<(int tick, double BPM)>();

        //channel data
        var channelData = new List<(int measure, int channel, string data)>();

        //一行ずつ解析
        for(int i=0; i<lines.Length; i++) {
            var raw = lines[i].Trim();

            if (raw.Length == 0 || raw.StartsWith("*") || raw.StartsWith("//") || raw.StartsWith("#;")) continue;

            if(raw.StartsWith("#", StringComparison.Ordinal)) {
                //WAV定義解析(#WAVxx <name>)
                if (raw.Length >= 7 && raw.StartsWith("#WAV", StringComparison.OrdinalIgnoreCase)) {
                    string codePart = raw.Substring(4, 2); //xx
                    if (IsBase36Pair(codePart)) {
                        var namePart = raw.Substring(6).Trim(); //<name>
                        if(namePart.Length > 0) {
                            int id = Base36PairToint(codePart);
                            wavNames[id] = namePart;
                            if (AutoLoadWavFromResource) {
                                var clip = Resources.Load<AudioClip>(namePart);
                                if (clip) wavClips[id] = clip;
                                else if (warningMissingWav) Debug.LogWarning($"WAVファイルが見つからない: {namePart} (code = {codePart}");
                            }
                        }
                    }
                }

                //baseBPM解析
                if(raw.StartsWith("#BPM ", StringComparison.OrdinalIgnoreCase)){
                    //5文字目以降を切り出して空白文字なしで取得
                    var val = raw.Substring(5).Trim();

                    //valがdoubleに変換できて0より大きいならbaseBPMに設定
                    if (double.TryParse(val,NumberStyles.Float, CultureInfo.InvariantCulture, out var BPMVal) && BPMVal > 0) {
                        baseBPM = BPMVal;
                    }
                    continue;
                }

                //offset解析
                if(raw.StartsWith("#OFFSET ", StringComparison.OrdinalIgnoreCase)) {
                    //あらゆる空白文字で分割
                    var parts = raw.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

                    //2つ目の要素がdoubleに変換できるならoffsetSecに設定
                    if(parts.Length >= 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetVal)) {
                        offsetSec = offsetVal;
                    }
                    continue;
                }

                //拡張BPM解析(#BPMxx yy)
                if(raw.StartsWith("#BPM", StringComparison.OrdinalIgnoreCase) && raw.Length >= 7) {
                    var tag = raw.Substring(0, 6);      //#BPMxx
                    var n1 = raw.Substring(4, 2);       //xx
                    var n2 = raw.Substring(6).Trim();   //yy

                    //n1がintに変換でき、n2がdoubleに変換でき、どちらも0より大きいならextBPMMapに登録
                    if (double.TryParse(n2, NumberStyles.Float, CultureInfo.InvariantCulture, out var BPMVal) && BPMVal > 0) {
                        extBPMMap[n1] = BPMVal;
                    }
                    continue;
                }

                //譜面データ解析(#mmmyy:DDDD...)
                if (raw.Length > 7 && char.IsDigit(raw[1]) && char.IsDigit(raw[2]) && char.IsDigit(raw[3]) && raw[6] == ':')  {
                    int measure = (raw[1] - '0') * 100 + (raw[2] - '0') * 10 + (raw[3] - '0');
                    string channel = raw.Substring(4, 2);
                    string data = raw[(raw.IndexOf(':') + 1)..].Trim();

                    //小節長変更
                    if(channel == "02") {
                        if(double.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out var factor) && factor > 0) {
                            measureFactors[measure] = factor;
                        }
                        continue;
                    }

                    //BPM変化
                    if(channel == "03") {
                        channelData.Add((measure, 3, data));
                    }



                    //ノーツデータ(1P)
                    if(channel is "11" or "12" or "13" or "14" or "15" or "16" or "17" or "18" or "19") {
                        int channelNum = int.Parse(channel, NumberStyles.Integer, CultureInfo.InvariantCulture);
                        channelData.Add((measure, channelNum, data));
                        continue;
                    }
                }
            }
        }

        //小節開始tickの計算
        int maxMeasure = 0;
        foreach(var (measure, _, _) in channelData) {
            if (measure > maxMeasure) maxMeasure = measure;
        }
        foreach(var m in measureFactors.Keys) {
            if (m > maxMeasure) maxMeasure = m;
        }

        var measureStartTick = new int[maxMeasure + 2]; //maxMeasure+1まで使うので+2で確保(累積和使用のため)
        //小節長変更を加味したtick計算(累積和)
        for(int i=0; i<=maxMeasure; i++) {
            double factor = 1.0;
            if (measureFactors.TryGetValue(i, out var f)) factor = f;
            int length = (int)Math.Round(TicksPerMeasure * factor);
            measureStartTick[i+1] = measureStartTick[i] + length;
        }

        //ノーツとBPM変化tickの計算
        var noteTemp = new List<(int tick, int lane, int type, int wavId)>();
        var BPMChangeTemp = new List<BPMChange>();

        foreach(var (measure, channel, data) in channelData) {
            if (string.IsNullOrEmpty(data) || data.Length % 2 == 1) continue;
            int sliceCount = data.Length / 2;
            double factor = 1.0;
            if (measureFactors.TryGetValue(measure, out var fac)) factor = fac;
            int measureTickLength = (int)Math.Round(TicksPerMeasure * factor);
            double sliceTick = (double)measureTickLength / sliceCount;

            for(int i=0; i<sliceCount; i++) {
                string token = data.Substring(i * 2, 2);
                if (token == "00") continue;
                //データひとつの開始位置を計算(小節の開始Tick + data内の位置から割り出したTIck)
                int tick = measureStartTick[measure] + (int)Math.Round(sliceTick * i);

                //BPM変化
                if(channel == 3) {
                    //拡張BPM指定
                    if (extBPMMap.TryGetValue(token, out var exbpm) && exbpm > 0) {
                        BPMChangeTemp.Add(new BPMChange {tick = tick, BPM = exbpm });
                    }
                    continue;
                }
                if(channel == 8) {
                    //直接BPM指定
                    if(int .TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hb) && hb > 0) {
                        var temp = new BPMChange {
                            tick = tick,
                            BPM = hb
                        };
                        BPMChangeTemp.Add(temp);
                    }
                    continue;
                }

                //ノーツ
                if (ChannelToLane.TryGetValue(channel, out var lane)) {
                    int wavId = Base36PairToint(token);
                    if(!wavClips.ContainsKey(wavId) && warningMissingWav && !wavNames.ContainsKey(wavId)) {
                        Debug.LogWarning($"WAV定義が見つからない: code={token} (measure={measure}, channel={channel}, tick={tick})");
                    }
                    noteTemp.Add((tick, lane, 0, wavId));
                }
            }
        }
        //BPM変化を統合,整理
        BPMChangeTemp.Add(new BPMChange { tick = 0, BPM = baseBPM });
        //tick昇順にソート
        BPMChangeTemp.Sort((a, b) => a.tick.CompareTo(b.tick));

        var segments = new List<BPMSegment>();
        if(BPMChangeTemp.Count != 0) {
            segments.Add(new BPMSegment {
                startTick = BPMChangeTemp[0].tick,
                startTime = 0.0,
                bpm = BPMChangeTemp[0].BPM
            });
            double accumTIme = 0.0;
            for (int i = 1; i < BPMChangeTemp.Count; i++) {
                var prev = BPMChangeTemp[i - 1];
                var cur = BPMChangeTemp[i];
                int deltaTick = cur.tick - prev.tick;
                if (deltaTick < 0) continue;
                //一tickあたりの秒数
                double secPerTick = 60.0 / (prev.BPM * TicksPerBeat);
                accumTIme += deltaTick * secPerTick;
                segments.Add(new BPMSegment {
                    startTick = cur.tick,
                    startTime = accumTIme,
                    bpm = cur.BPM
                });
            }
        }
        if(segments.Count > 0) {
            currentBpmCache = segments[^1].bpm;//最後のBPMを現在BPMとしてキャッシュ
        }

        //tivkからtimeへの変換
        foreach(var (tick, lane, type, wavId) in noteTemp) {
            double sec;
            var seg = segments[0];
            for(int i=segments.Count-1; i>=0; i--) {
                if (tick >= segments[i].startTick) {
                    seg = segments[i];
                    break;
                }
            }
            int deltaTick = tick - seg.startTick;
            double secPerTick = 60.0 / (seg.bpm * TicksPerBeat);
            sec = seg.startTime + deltaTick * secPerTick;
            result.Add(new NoteData(tick, (float)sec, lane, type, wavId));
        }

        result.Sort((a, b) => a.time.CompareTo(b.time));
        return result;
    }

    //2文字が36進数であるか
    private static bool IsBase36Pair(string s) =>
        s.Length == 2 &&
        IsBase36(s[0]) &&
        IsBase36(s[1]);

    //2文字の36進数をintに変換
    private static int Base36PairToint(string s) =>
        (Base36CharToInt(s[0]) * 36) + Base36CharToInt(s[1]);

    //36進数文字をintに変換
    private static int Base36CharToInt(char c) =>
        c >= '0' && c <= '9' ? c - '0' :
        c >= 'A' && c <= 'Z' ? c - 'A' + 10 :
        c >= 'a' && c <= 'z' ? c - 'a' + 10 :
        throw new ArgumentException($"Invalid base36 character: {c}");

    private static bool IsBase36(char c) =>
        (c >= '0' && c <= '9') ||
        (c >= 'A' && c <= 'Z') ||
        (c >= 'a' && c <= 'z');

}