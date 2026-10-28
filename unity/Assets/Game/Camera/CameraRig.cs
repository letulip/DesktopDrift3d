using DesktopDrift.Core;
using UnityEngine;

namespace DesktopDrift.Game
{
    [DefaultExecutionOrder(100)]
    public sealed class CameraRig : MonoBehaviour
    {
        public enum CameraMode { C1, C2 }

        public static CameraMode StartMode = CameraMode.C2;

        [SerializeField] CameraPreset preset;
        [SerializeField] Camera cam;
        [SerializeField] CarView carView;
        [SerializeField] RaceLoop loop;

        public CameraMode Mode { get; set; }

        // C2 state on the fixed-step grid; LateUpdate interpolates prev -> cur like CarView.
        CamState prev, cur;
        bool hasC2;
        float steppedAt; // Time.fixedTime of the last step: a fixed step without one means paused/finished

        void Awake()
        {
            // Dev hook for scripts/capture.sh: the headless editor inherits the env; devices have no such variable.
            var env = System.Environment.GetEnvironmentVariable("DD_CAMERA_MODE");
            if (env == "C1") Mode = CameraMode.C1;
            else if (env == "C2") Mode = CameraMode.C2;
            else Mode = StartMode;
        }

        void OnEnable()
        {
            if (loop == null) return;
            loop.OnReset += HandleReset;
            loop.OnStepped += HandleStepped;
        }

        void OnDisable()
        {
            if (loop == null) return;
            loop.OnReset -= HandleReset;
            loop.OnStepped -= HandleStepped;
        }

        void HandleReset()
        {
            if (preset == null) return;
            cur = prev = ChaseCameraMath.Reset(Kin(), preset.c2);
            hasC2 = true;
            steppedAt = Time.fixedTime;
        }

        void HandleStepped()
        {
            if (!hasC2) return;
            prev = cur;
            cur = ChaseCameraMath.Step(cur, Kin(), preset.c2, RaceLoop.FixedDt);
            steppedAt = Time.fixedTime;
        }

        CarKinematics Kin()
        {
            var c = loop.State.Car;
            return new CarKinematics { X = c.X, Y = c.Y, Angle = c.Angle, Vx = c.Vx, Vy = c.Vy, MaxSpeed = loop.Car.Drive.MaxSpeed };
        }

        void LateUpdate()
        {
            if (preset == null || cam == null || carView == null) return;
            if (Mode == CameraMode.C2 && hasC2)
            {
                // Hold the last state while paused / finished (see CarView.LateUpdate).
                bool stale = Time.fixedTime - steppedAt > 0.5f * Time.fixedDeltaTime;
                float alpha = stale ? 1f : Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
                Vector3 pos = Vector3.Lerp(W(prev.PosX, prev.PosY, prev.PosH), W(cur.PosX, cur.PosY, cur.PosH), alpha);
                Vector3 look = Vector3.Lerp(W(prev.LookX, prev.LookY, prev.LookH), W(cur.LookX, cur.LookY, cur.LookH), alpha);
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
                cam.fieldOfView = preset.fov;
                return;
            }
            // C1, and C2 before the first race reset.
            float d = preset.c1Distance * (Application.isMobilePlatform ? preset.mobileDistanceScale : 1f);
            C1Pose(carView.transform.position, preset.c1PitchDeg, d, preset.c1ScreenOffset, preset.fov, out var p, out var rot);
            cam.transform.SetPositionAndRotation(p, rot);
            cam.fieldOfView = preset.fov;
        }

        static Vector3 W(double x, double y, double h) =>
            WorldMapping.ToWorld(x, y) + Vector3.up * ((float)h * WorldMapping.UnitsToMeters);

        public static void C1Pose(Vector3 car, float pitchDeg, float distance, float screenOffset, float fovDeg,
            out Vector3 pos, out Quaternion rot)
        {
            rot = Quaternion.Euler(pitchDeg, 0f, 0f);
            float frameH = 2f * distance * Mathf.Tan(fovDeg * 0.5f * Mathf.Deg2Rad);
            pos = car - rot * Vector3.forward * distance + rot * Vector3.up * (screenOffset * frameH);
        }
    }
}
