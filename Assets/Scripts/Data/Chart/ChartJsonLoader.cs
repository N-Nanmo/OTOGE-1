using UnityEngine;
using System.IO;
using RhythmGame.Data.Chart;

namespace RhythmGame.Chart {
    public static class ChartJsonLoader {
        public static bool LoadJsonText(string json, ChartData target) {
            try {
                var model = JsonUtility.FromJson<ChartJsonModel>(json);
                if (model == null) return false;
                ChartJsonParser.ApplyTo(model, target);
                return true;
            }
            catch (System.Exception e) {
                Debug.LogError($"[ChartJsonLoader] Parse error: {e}");
                return false;
            }
        }

        public static bool LoadFromResource(string resourcePath, ChartData target) {
            var textAsset = Resources.Load<TextAsset> (resourcePath);
            if (!textAsset) {
                Debug.LogError($"[ChartJsonLoadr] Resource not found: {resourcePath}");
                return false;
            }
            return LoadJsonText(textAsset.text, target);
        }

        public static bool LoadFromFile(string absolutePath, ChartData target) {
            if (!File.Exists(absolutePath)) {
                Debug.LogError($"[ChartJsonLLoader] File not found: {absolutePath}");
                return false;
            }
            var json = File.ReadAllText(absolutePath);
            return LoadJsonText(json, target);
        }
    }
}
