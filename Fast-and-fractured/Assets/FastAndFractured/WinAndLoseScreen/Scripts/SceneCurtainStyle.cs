using UnityEngine;

namespace FastAndFractured
{
    /// <summary>Look of the <see cref="SceneCurtain"/> (Resources/SceneCurtainStyle).</summary>
    [CreateAssetMenu(menuName = "Fast And Fractured/Scene Curtain Style", fileName = "SceneCurtainStyle")]
    public sealed class SceneCurtainStyle : ScriptableObject
    {
        [Tooltip("Logo shown where the two halves meet.")]
        public Texture2D logo;

        [Tooltip("Part of the logo texture that holds the logo (normalized, origin bottom-left).")]
        public Rect logoUv = new Rect(0f, 0f, 1f, 1f);

        public Color leftColor = new Color32(0xE5, 0x6B, 0x6F, 0xFF);   // pause menu "home" red
        public Color rightColor = new Color32(0x5B, 0xC3, 0xD9, 0xFF);  // pause menu "play" blue
        public Color seamColor = new Color32(0xF7, 0xE6, 0x6E, 0xFF);   // chalky yellow button frame
    }
}
