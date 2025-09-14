using UnityEngine;

[CreateAssetMenu(menuName = "Rhythm/ThemePalette")]
public class ThemePalette : ScriptableObject {
    public Color tapColor = new(0.9f, 0.6f, 1f);
    public Color holdColor = new(0.4f, 0.8f, 1f);
    public Color slideColor = new(1f, 0.7f, 0.3f);
    public Color guideColor = new(1f, 1f, 1f, 0.4f);
    public Gradient backgroundGradient;
    public Color glowPrimary = new(0.9f, 0.4f, 1f);
    public Color glowSecondary = new(0.2f, 0.6f, 1f);
}
