using UnityEngine;

namespace DesktopDrift.Game
{
    [CreateAssetMenu(menuName = "DesktopDrift/Camera Preset")]
    public sealed class CameraPreset : ScriptableObject
    {
        [Header("C1 (high, fixed heading)")]
        public float c1PitchDeg = 60f;
        public float c1Distance = 0.9f;
        public float c1ScreenOffset = 0.10f;
        public float mobileDistanceScale = 1f / 0.65f;
        public float fov = 60f;
        // C2 parameters are added in step 6.
    }
}
