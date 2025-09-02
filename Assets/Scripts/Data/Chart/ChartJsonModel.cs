using UnityEngine;
using System.Collections.Generic;
using RhythmGame.Data.Chart;

namespace RhythmGame.Chart {
    [System.Serializable]
    public class ChartJsonModel {
        public string chartId;
        public float offset;
        public float bpm;
        public List<NoteJson> notes;

        [System.Serializable]
        public class NoteJson {
            public float time;
            public int lane;
            public string type;
            public float length;
        }
    }

    public static class ChartJsonParser {
        public static void ApplyTo(ChartJsonModel src, ChartData dst) {
            dst.chartId = src.chartId;
            dst.bpm = src.bpm;
            dst.offset = src.offset;
            dst.notes.Clear();
            foreach (var n in src.notes) {
                var ev = new NoteData {
                    time = n.time,
                    laneIndex = n.lane,
                    type = ParseType(n.type),
                    duration = n.length
                };
                dst.notes.Add(ev);
            }
            dst.notes.Sort((a, b) => a.time.CompareTo(b.time));
        }

        public static NoteType ParseType(string s) {
            return s.ToLower() switch {
                "tap" => NoteType.Tap,
                "holdstart" => NoteType.HoldStart,
                "holdend" => NoteType.HoldEnd,
                "flick" => NoteType.Flick,
                _ => NoteType.Tap
            };
        }
    }
}
