using RhythmGame.Data.Chart;
using UnityEngine;

public class ChartJsonRefCheck : MonoBehaviour
{
    [SerializeField] private SongChartAsset chartAsset;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(chartAsset == null) {
            Debug.LogWarning("[ChartJsonRefCheck] chartAsset is not assigned.");
            return;
        }
        if(chartAsset.jsonChart == null) {
            Debug.LogWarning("[ChartJsonRefCheck] jsonChart is not assigned in chartAsset: " + chartAsset.name);
        } else {
            Debug.Log($"[ChartJsonRefCheck] JSON length = {chartAsset.jsonChart.text.Length} chars");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
