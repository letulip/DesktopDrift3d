using DesktopDrift.Core;
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

        [Header("C2 (chase, heading follows velocity)")]
        public CameraParams c2 = new CameraParams();
    }
}
