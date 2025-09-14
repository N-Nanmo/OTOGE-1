using UnityEngine;
using System.Collections.Generic;
using RhythmGame.Data.Chart;

namespace RhythmGame.Chart {
    [System.Serializable]
    public class ChartJsonModel {
        // Top-level (7-lane schema)
        public string name;
        public string songId;
        public int maxBlock;   // for 7-lane charts, usually 7
        public int lanes;      // optional
        public float offset;   // seconds or milliseconds
        public float songOffset;
        public float BPM;
        public float bpm;
        public float approachTime;

        public List<NoteJson> notes;   // top-level notes array
        public List<BeatJson> beats;   // optional pulse groups

        [System.Serializable]
        public class NoteJson {
            public int LPB;
            public int num;
            public int block;
            public string type;
            public int typeCode;
            public float time;
            public int lane;
            public float duration;
            // IMPORTANT: break recursive graph by using a child type without its own 'notes' field
            public List<NoteChildJson> notes; // child notes (e.g., hold tail)
        }

        [System.Serializable]
        public class NoteChildJson {
            public int LPB;
            public int num;
            public int block;
            public string type;
            public int typeCode;
            public float time;
            public int lane;
            public float duration;
            // no further nesting to avoid recursion depth
        }

        [System.Serializable]
        public class BeatJson {
            public int LPB;
            public List<NoteJson> notes;
        }
    }

    public static class ChartJsonParser {
        private const int SevenLane = 7;

        private static int DecideLaneCount(ChartJsonModel src) {
            if (src == null) return SevenLane;
            if (src.lanes > 0) return SevenLane;
            if (src.maxBlock > 0) return SevenLane;
            return SevenLane;
        }

        // Scale all offsets to 1/10 of their original meaning (after ms->sec conversion)
        private static float NormalizeOffset(float off) {
            float seconds = (off > 10f) ? off * 0.001f : off; // ms -> sec if needed
            return seconds * 0.1f; // apply 1/10 scaling as requested
        }

        public static void ApplyTo(ChartJsonModel src, ChartData dst) {
            if (src == null || dst == null) return;
            dst.chartId = !string.IsNullOrEmpty(src.name) ? src.name : src.songId;
            float bpmA = src.BPM > 0f ? src.BPM : 0f;
            float bpmB = src.bpm > 0f ? src.bpm : 0f;
            dst.bpm = (bpmA > 0f ? bpmA : (bpmB > 0f ? bpmB : 120f));
            float offA = NormalizeOffset(src.offset);
            float offB = NormalizeOffset(src.songOffset);
            dst.offset = (offA != 0f) ? offA : offB;
            if (src.approachTime > 0f) dst.approachRate = src.approachTime;

            int laneCount = DecideLaneCount(src);
            dst.lanes = laneCount;

            if (dst.notes == null || dst.notes.Length != laneCount)
                dst.notes = new List<NoteData>[laneCount];
            for (int i = 0; i < laneCount; i++) {
                if (dst.notes[i] == null) dst.notes[i] = new List<NoteData>(); else dst.notes[i].Clear();
            }

            if (src.beats != null) {
                foreach (var beat in src.beats) {
                    if (beat == null || beat.notes == null) continue;
                    int beatLpb = beat.LPB <= 0 ? 4 : beat.LPB;
                    foreach (var n in beat.notes) AddNote(dst, n, beatLpb, laneCount);
                }
            }
            if (src.notes != null) {
                foreach (var n in src.notes) AddNote(dst, n, n != null && n.LPB > 0 ? n.LPB : 4, laneCount);
            }

            for (int i = 0; i < laneCount; i++) dst.notes[i].Sort((a, b) => a.time.CompareTo(b.time));
        }

        private static void AddNote(ChartData dst, ChartJsonModel.NoteJson n, int defaultLpb, int laneCount) {
            if (n == null) return;

            // For our 7-lane schema, JSON uses 'block' as the logical lane. Unity's JsonUtility sets
            // missing int fields to 0, so relying on 'lane' when the field is absent would wrongly map to 0.
            // Therefore, for 7-lane charts, prefer 'block'. For other schemas, fallback to lane then block.
            int lane = -1;
            if (laneCount == SevenLane) {
                lane = n.block;
            } else {
                if (n.lane >= 0) lane = n.lane; else if (n.block >= 0) lane = n.block;
            }

            if (lane < 0 || lane >= laneCount) return;

            float startTime;
            if (n.LPB > 0) {
                int lpb = n.LPB > 0 ? n.LPB : defaultLpb;
                if (lpb <= 0) lpb = 4;
                startTime = CalcTime(n.num, lpb, dst.bpm);
            } else {
                startTime = Mathf.Max(0f, n.time);
            }

            float duration = Mathf.Max(0f, n.duration);
            if (duration <= 0f && n.notes != null && n.notes.Count > 0) {
                var tail = n.notes[n.notes.Count - 1];
                float endTime;
                if (tail.LPB > 0) {
                    int lpb2 = tail.LPB > 0 ? tail.LPB : (n.LPB > 0 ? n.LPB : defaultLpb);
                    if (lpb2 <= 0) lpb2 = 4;
                    endTime = CalcTime(tail.num, lpb2, dst.bpm);
                } else {
                    endTime = Mathf.Max(0f, tail.time);
                }
                duration = Mathf.Max(0f, endTime - startTime);
            }

            NoteType type = ResolveType(n.type, n.typeCode, duration > 0f);
            dst.notes[lane].Add(new NoteData { time = startTime, laneIndex = lane, type = type, duration = duration });
        }

        private static float CalcTime(int pulseIndex, int lpb, float bpm) {
            return (60f / bpm) * (pulseIndex / (float)lpb);
        }

        private static NoteType ResolveType(string typeStr, int code, bool hasDuration) {
            if (!string.IsNullOrEmpty(typeStr)) {
                var t = ParseTypeString(typeStr);
                if (t != NoteType.Tap) return t;
            }
            if (code != 0) {
                var t = ParseTypeCode(code);
                if (t == NoteType.Tap && hasDuration) return NoteType.HoldStart;
                return t;
            }
            if (hasDuration) return NoteType.HoldStart;
            return NoteType.Tap;
        }

        private static NoteType ParseTypeCode(int code) {
            return code switch {
                1 => NoteType.Tap,
                0 => NoteType.Tap,
                2 => NoteType.HoldStart,
                3 => NoteType.HoldEnd,
                4 => NoteType.Flick,
                _ => NoteType.Tap
            };
        }

        private static NoteType ParseTypeString(string s) {
            switch ((s ?? string.Empty).ToLower()) {
                case "tap": return NoteType.Tap;
                case "holdstart": return NoteType.HoldStart;
                case "holdend": return NoteType.HoldEnd;
                case "noteend": return NoteType.NoteEnd;
                case "flick": return NoteType.Flick;
                default:
                    if (int.TryParse(s, out var num)) return ParseTypeCode(num);
                    return NoteType.Tap;
            }
        }
    }
}
