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
    //�萔
    private const int TicksPerMeasure = 3600;                           //1���߂������Tick��
    private const int BeatsPerMeasure = 4;                              //1���߂�����̔���(4/4)
    private const int TicksPerBeat = TicksPerMeasure / BeatsPerMeasure;    //1���������Tick��
    private const double DefaultBPM = 120.0;

    //BMS ���[���ϊ�(0=�M...7=7��)
    //1P: 11 12 13 14 15 17 18 19 / 16=�M
    private static readonly Dictionary<int, int> ChannelToLane = new() {
        {16, 0}, //�M
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
        ConstantSpeed,                  //��葬�x�ŃX�N���[��
        VisibleTime,                    //���b���w����X�N���[��
        VisibleBeats                    //���r�[�g���w��ɂ��X�N���[��
    }

    //public�ϐ�
    [Header("Chart / Audio")]
    public TextAsset charttext;         //���ʃe�L�X�g�̎Q��
    public AudioSource musicSource;     //�Ȃ̎Q��
    public double AudioLoadTime = 0.1;   //�Ȃ̃��[�h�ɂ����鎞��(�b)
    public float userOffsetSec = 0.0f; //���[�U�[�I�t�Z�b�g(�b)

    [Header("Note Prefabs")]
    public GameObject notePrefab_Scratch;   //�M(���[��0)��Prefab
    public GameObject notePrefab_Odd;  //���[��1, 3, 5, 7��Prefab
    public GameObject notePrefab_Even;  //���[��2, 4, 6��Prefab

    [Header("Layout")]
    public float scrollSpeed = 6.0f;    //�m�[�c�̃X�N���[�����x(��)
    public float hitLineY = -3.3f;      //�q�b�g���C����Y���W(��)
    public float spownTopY = 4.0f;      //�m�[�c�����������Y���W(��)

    [Header("Timings")]
    public float perfectTime = 0.01667f;//Perfect����̎���(60fps,��1�t���[��)
    public float greatTime = 0.03333f;  //Great����̎���(60fps,��2�t���[��)
    public float goodTime = 0.11667f;   //Good����̎���(60fps,��7�t���[��)
    public float missTime = 0.15000f;   //Miss����̎���(60fps,��9�t���[��)

    [Header("Input")]
    public KeyCode[] laneKeys;          //�e���[���̃L�[���蓖��(0=�M...7=7��)
    public bool KeepsCombo = true;

    [Header("Score")]
    public int scoreMax = 1000000;          //�ő�X�R�A
    public float scoreGreatFactor = 0.95f;  //Great���̃X�R�A�W��
    public float scoreGoodFactor = 0.80f;   //Good���̃X�R�A�W��

    [Header("Scroll Speed Mode")]
    public ScrollSpeedMode speedMode = ScrollSpeedMode.ConstantSpeed;   //�X�N���[�����x���[�h
    [Tooltip("���b���w�胂�[�hVisibleTime�̎��̂ݎg�p")]
    public float visibleTime = 2.0f;    //���b���w�胂�[�h�̉��b��
    [Tooltip("���r�[�g���w�胂�[�hVisibleBeats�̎��̂ݎg�p")]
    public float visibleBeats = 4.0f;   //���r�[�g���w�胂�[�h�̉��r�[�g��
    public float hiSpeed = 1.0f;        //HiSpeed�{��

    [Header("WAV")]
    [Tooltip("#WAVxx�Ŏw�肳�ꂽWAV�t�@�C����Resource���烍�[�h")]
    public bool AutoLoadWavFromResource = true;
    [Tooltip("���������pSFXAusioSource�v�[���̐�")]
    public int SFXPoolSize = 16;
    [Tooltip("Wav�t�@�C�������[�h���Ɍx��")]
    public bool warningMissingWav = true;

    //�f�[�^�\��
    //time(�b), lane, type(0=Tap,1=longNoteStart,2=longNoteEnd)
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
        public GameObject obj;    //�m�[�c��GameObject
        public SpriteRenderer sr;
        public NoteData data;
        public bool judged;
    }
    private struct BPMSegment {
        public int startTick;
        public double startTime; //�b
        public double bpm;
    }

    private struct BPMChange {
        public int tick;
        public double BPM;
    }

    //private�ϐ�
    private List<NoteData> notes;       //�m�[�c�̃��X�g(time����)
    private List<NoteView> noteViews;   //�m�[�c�̕\���I�u�W�F�N�g�̃��X�g
    private double dspMusicStartTime;   //�Ȃ��n�܂�������
    private bool musicStarted;          //�Ȃ��n�܂�����

    private int combo;
    private int maxCombo;
    private long score;
    private int baseScorePerNote;

    private readonly Dictionary<int, double> judgeCounts = new();
    //Wav�t�@�C��
    private readonly Dictionary<int, AudioClip> wavClips = new();
    private readonly Dictionary<int, string> wavNames = new();
    //SFX�����p�v�[��
    private readonly List<AudioSource> sfxPool = new();
    private int sfxPoolCounter = 0;

    private double frameDeltaAvg, frameDeltaMax;
    private int frameSampleCount;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Application.targetFrameRate = 60;
        if (!musicSource) musicSource = GetComponent<AudioSource>();

        //�L�[���蓖�Ă̐��`�F�b�N
        int needKeys = 8;
        if(laneKeys.Length != needKeys) {
            Debug.LogError("���[�����ƃL�[���蓖�Ă̐�����v���Ȃ�");
        }
        //SFX�v�[��������
        InitSfxPool();
        //���ʉ��
        notes = ParseBMSAndBuildNotes(charttext);
        baseScorePerNote = (notes.Count > 0) ? (scoreMax / notes.Count) : 0;
        //�m�[�c����
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
            $"��avg:{frameDeltaAvg * 1000:0.00}ms ��max:{frameDeltaMax * 1000:0.00}ms FPS~{1.0 / Time.unscaledDeltaTime:0.0}");
    }

    //����BPM(�Ȉ�)
    private double GetCurrentBPM(double songTime) => currentBpmCache;
    private double currentBpmCache = DefaultBPM;

    //�X�N���[�����x�Z�o
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

    //�m�[�c�ʒu�X�V
    private void UpdateNotePositions(double songTime) {
        float v = GetScrollSpeed(songTime);
        float songTimeF = (float)songTime;

        foreach(var nv in noteViews) {
            if (nv.judged) continue;
            //�c��̎���
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

    //����
    private double GetSongTime() => musicStarted ? AudioSettings.dspTime - dspMusicStartTime : 0.0;
    private void ScheduleMusic() {
        dspMusicStartTime = AudioSettings.dspTime + AudioLoadTime;
        musicSource.PlayScheduled(dspMusicStartTime);
        musicStarted = true;
    }

    //�m�[�c����
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
                Debug.LogError("�m�[�c��Prefab���w�肳��Ă��Ȃ�");
            }

            var obj = Instantiate(prefab, transform);
            obj.name = $"Note_L{nt.lane}_t{nt.time}";//�b���͏����_�ȉ�3���܂�
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

    //SFX audioSource�v�[��
    private void InitSfxPool() {
        for(int i=0; i<SFXPoolSize; i++) {
            var temp = gameObject.AddComponent<AudioSource>();
            temp.playOnAwake = false;   //�Đ��J�n���Ɏ����ōĐ����Ȃ�
            temp.loop = false;          //���[�v���Ȃ�
            temp.spatialBlend = 0.0f;   //�������l�����Ȃ�
            sfxPool.Add(temp);
        }
    }

    //���ʉ��
    private List<NoteData> ParseBMSAndBuildNotes(TextAsset text) {
        var result = new List<NoteData>();
        if (!text) {
            Debug.LogError("���ʃe�L�X�g���w�肳��Ă��Ȃ�");
            return result;
        }

        var lines = text.text.Replace("\r\n", "\n").Split("\n");

        double baseBPM = DefaultBPM;
        double offsetSec = 0.0;

        //�g��BPM�}�b�v
        var extBPMMap = new Dictionary<string, double>();

        //���߂̒�����
        var measureFactors = new Dictionary<int, double>();

        //BPM�ω�
        var BPMChanges = new List<(int tick, double BPM)>();

        //channel data
        var channelData = new List<(int measure, int channel, string data)>();

        //��s�����
        for(int i=0; i<lines.Length; i++) {
            var raw = lines[i].Trim();

            if (raw.Length == 0 || raw.StartsWith("*") || raw.StartsWith("//") || raw.StartsWith("#;")) continue;

            if(raw.StartsWith("#", StringComparison.Ordinal)) {
                //WAV��`���(#WAVxx <name>)
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
                                else if (warningMissingWav) Debug.LogWarning($"WAV�t�@�C����������Ȃ�: {namePart} (code = {codePart}");
                            }
                        }
                    }
                }

                //baseBPM���
                if(raw.StartsWith("#BPM ", StringComparison.OrdinalIgnoreCase)){
                    //5�����ڈȍ~��؂�o���ċ󔒕����Ȃ��Ŏ擾
                    var val = raw.Substring(5).Trim();

                    //val��double�ɕϊ��ł���0���傫���Ȃ�baseBPM�ɐݒ�
                    if (double.TryParse(val,NumberStyles.Float, CultureInfo.InvariantCulture, out var BPMVal) && BPMVal > 0) {
                        baseBPM = BPMVal;
                    }
                    continue;
                }

                //offset���
                if(raw.StartsWith("#OFFSET ", StringComparison.OrdinalIgnoreCase)) {
                    //������󔒕����ŕ���
                    var parts = raw.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

                    //2�ڂ̗v�f��double�ɕϊ��ł���Ȃ�offsetSec�ɐݒ�
                    if(parts.Length >= 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetVal)) {
                        offsetSec = offsetVal;
                    }
                    continue;
                }

                //�g��BPM���(#BPMxx yy)
                if(raw.StartsWith("#BPM", StringComparison.OrdinalIgnoreCase) && raw.Length >= 7) {
                    var tag = raw.Substring(0, 6);      //#BPMxx
                    var n1 = raw.Substring(4, 2);       //xx
                    var n2 = raw.Substring(6).Trim();   //yy

                    //n1��int�ɕϊ��ł��An2��double�ɕϊ��ł��A�ǂ����0���傫���Ȃ�extBPMMap�ɓo�^
                    if (double.TryParse(n2, NumberStyles.Float, CultureInfo.InvariantCulture, out var BPMVal) && BPMVal > 0) {
                        extBPMMap[n1] = BPMVal;
                    }
                    continue;
                }

                //���ʃf�[�^���(#mmmyy:DDDD...)
                if (raw.Length > 7 && char.IsDigit(raw[1]) && char.IsDigit(raw[2]) && char.IsDigit(raw[3]) && raw[6] == ':')  {
                    int measure = (raw[1] - '0') * 100 + (raw[2] - '0') * 10 + (raw[3] - '0');
                    string channel = raw.Substring(4, 2);
                    string data = raw[(raw.IndexOf(':') + 1)..].Trim();

                    //���ߒ��ύX
                    if(channel == "02") {
                        if(double.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out var factor) && factor > 0) {
                            measureFactors[measure] = factor;
                        }
                        continue;
                    }

                    //BPM�ω�
                    if(channel == "03") {
                        channelData.Add((measure, 3, data));
                    }



                    //�m�[�c�f�[�^(1P)
                    if(channel is "11" or "12" or "13" or "14" or "15" or "16" or "17" or "18" or "19") {
                        int channelNum = int.Parse(channel, NumberStyles.Integer, CultureInfo.InvariantCulture);
                        channelData.Add((measure, channelNum, data));
                        continue;
                    }
                }
            }
        }

        //���ߊJ�ntick�̌v�Z
        int maxMeasure = 0;
        foreach(var (measure, _, _) in channelData) {
            if (measure > maxMeasure) maxMeasure = measure;
        }
        foreach(var m in measureFactors.Keys) {
            if (m > maxMeasure) maxMeasure = m;
        }

        var measureStartTick = new int[maxMeasure + 2]; //maxMeasure+1�܂Ŏg���̂�+2�Ŋm��(�ݐϘa�g�p�̂���)
        //���ߒ��ύX����������tick�v�Z(�ݐϘa)
        for(int i=0; i<=maxMeasure; i++) {
            double factor = 1.0;
            if (measureFactors.TryGetValue(i, out var f)) factor = f;
            int length = (int)Math.Round(TicksPerMeasure * factor);
            measureStartTick[i+1] = measureStartTick[i] + length;
        }

        //�m�[�c��BPM�ω�tick�̌v�Z
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
                //�f�[�^�ЂƂ̊J�n�ʒu���v�Z(���߂̊J�nTick + data���̈ʒu���犄��o����TIck)
                int tick = measureStartTick[measure] + (int)Math.Round(sliceTick * i);

                //BPM�ω�
                if(channel == 3) {
                    //�g��BPM�w��
                    if (extBPMMap.TryGetValue(token, out var exbpm) && exbpm > 0) {
                        BPMChangeTemp.Add(new BPMChange {tick = tick, BPM = exbpm });
                    }
                    continue;
                }
                if(channel == 8) {
                    //����BPM�w��
                    if(int .TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hb) && hb > 0) {
                        var temp = new BPMChange {
                            tick = tick,
                            BPM = hb
                        };
                        BPMChangeTemp.Add(temp);
                    }
                    continue;
                }

                //�m�[�c
                if (ChannelToLane.TryGetValue(channel, out var lane)) {
                    int wavId = Base36PairToint(token);
                    if(!wavClips.ContainsKey(wavId) && warningMissingWav && !wavNames.ContainsKey(wavId)) {
                        Debug.LogWarning($"WAV��`��������Ȃ�: code={token} (measure={measure}, channel={channel}, tick={tick})");
                    }
                    noteTemp.Add((tick, lane, 0, wavId));
                }
            }
        }
        //BPM�ω��𓝍�,����
        BPMChangeTemp.Add(new BPMChange { tick = 0, BPM = baseBPM });
        //tick�����Ƀ\�[�g
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
                //��tick������̕b��
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
            currentBpmCache = segments[^1].bpm;//�Ō��BPM������BPM�Ƃ��ăL���b�V��
        }

        //tivk����time�ւ̕ϊ�
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

    //2������36�i���ł��邩
    private static bool IsBase36Pair(string s) =>
        s.Length == 2 &&
        IsBase36(s[0]) &&
        IsBase36(s[1]);

    //2������36�i����int�ɕϊ�
    private static int Base36PairToint(string s) =>
        (Base36CharToInt(s[0]) * 36) + Base36CharToInt(s[1]);

    //36�i��������int�ɕϊ�
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