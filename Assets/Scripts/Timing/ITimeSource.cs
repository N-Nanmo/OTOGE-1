using UnityEngine;

namespace RhythmGame.Timing {
    public interface ITimeSource {
        double CurrentTime { get; }
        bool IsReady { get; }
    }
}
