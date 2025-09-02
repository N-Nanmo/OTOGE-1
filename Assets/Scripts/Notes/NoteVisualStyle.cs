using UnityEngine;

namespace RhythmGame.Notes
{
    [CreateAssetMenu(menuName = "RhythmGame/Notes/NoteVisualStyle")]
    public class  NoteVisualStyle : ScriptableObject {
        public Color baseColor = Color.cyan;
        public Color holdColor = new Color(1f, 0.8f, 0.2f);
        public Vector3 baseScale = new Vector3(0.6f, 0.2f, 0.6f);
        public Material material = null;
    }
}
