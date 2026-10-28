using UnityEngine;

namespace DesktopDrift.Game
{
    [DefaultExecutionOrder(100)]
    public sealed class CameraRig : MonoBehaviour
    {
        public enum CameraMode { C1, C2 }

        public static CameraMode StartMode = CameraMode.C1;

        [SerializeField] CameraPreset preset;
        [SerializeField] Camera cam;
        [SerializeField] CarView carView;
        [SerializeField] RaceLoop loop;

        public CameraMode Mode { get; set; }

        void Awake() { Mode = StartMode; }

        void LateUpdate()
        {
            if (preset == null || cam == null || carView == null) return;
            // C2 is a stub until step 6: it behaves like C1.
            float d = preset.c1Distance * (Application.isMobilePlatform ? preset.mobileDistanceScale : 1f);
            C1Pose(carView.transform.position, preset.c1PitchDeg, d, preset.c1ScreenOffset, preset.fov, out var pos, out var rot);
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = preset.fov;
        }

        public static void C1Pose(Vector3 car, float pitchDeg, float distance, float screenOffset, float fovDeg,
            out Vector3 pos, out Quaternion rot)
        {
            rot = Quaternion.Euler(pitchDeg, 0f, 0f);
            float frameH = 2f * distance * Mathf.Tan(fovDeg * 0.5f * Mathf.Deg2Rad);
            pos = car - rot * Vector3.forward * distance + rot * Vector3.up * (screenOffset * frameH);
        }
    }
}
