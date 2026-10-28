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
        CamPhase ph = CamPhase.Normal; // C3: countdown fly-in, post-finish orbit, phone distance
        float shakeT; // s left of the cone-hit shake

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
            loop.OnEvent += HandleEvent;
        }

        void OnDisable()
        {
            if (loop == null) return;
            loop.OnReset -= HandleReset;
            loop.OnStepped -= HandleStepped;
            loop.OnEvent -= HandleEvent;
        }

        void HandleReset()
        {
            if (preset == null) return;
            ph = new CamPhase
            {
                Intro01 = 0,
                Orbit = false,
                DistanceScale = Application.isMobilePlatform ? preset.c2.MobileDistanceScale : 1,
            };
            shakeT = 0f;
            cur = prev = ChaseCameraMath.Reset(Kin(), preset.c2, ph);
            hasC2 = true;
            steppedAt = Time.fixedTime;
        }

        void HandleStepped()
        {
            if (!hasC2) return;
            // Fly-in time on the step grid (deterministic), not Time.time.
            double intro = preset.c2.IntroSeconds > 0 ? loop.StepCount * RaceLoop.FixedDt / preset.c2.IntroSeconds : 1;
            ph.Intro01 = intro < 1 ? intro : 1;
            StepCam();
        }

        void StepCam()
        {
            prev = cur;
            cur = ChaseCameraMath.Step(cur, Kin(), preset.c2, ph, RaceLoop.FixedDt);
            steppedAt = Time.fixedTime;
        }

        void HandleEvent(RaceEvent e)
        {
            if (preset == null) return;
            if (e.Kind == RaceEvent.EventKind.Finished) ph.Orbit = true;
            else if (e.Kind == RaceEvent.EventKind.ConeHit && preset.c2.ShakeAmp > 0) shakeT = (float)preset.c2.ShakeSeconds;
        }

        void FixedUpdate()
        {
            // After the finish the race no longer steps: the rig steps the orbit itself, once per fixed step
            // (the finishing step itself was already stepped through OnStepped).
            if (!hasC2 || !ph.Orbit || loop == null || loop.Running || steppedAt == Time.fixedTime) return;
            StepCam();
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
                cam.fieldOfView = (float)(prev.Fov + (cur.Fov - prev.Fov) * alpha);
                Shake();
                return;
            }
            // C1, and C2 before the first race reset.
            float d = preset.c1Distance * (Application.isMobilePlatform ? preset.mobileDistanceScale : 1f);
            C1Pose(carView.transform.position, preset.c1PitchDeg, d, preset.c1ScreenOffset, preset.fov, out var p, out var rot);
            cam.transform.SetPositionAndRotation(p, rot);
            cam.fieldOfView = preset.fov;
            Shake();
        }

        // Cone-hit shake: cosmetic, wall-clock time, camera-local offset that fades out over ShakeSeconds.
        void Shake()
        {
            if (shakeT <= 0f) return;
            shakeT -= Time.deltaTime;
            if (shakeT <= 0f || preset.c2.ShakeSeconds <= 0) return;
            float k = shakeT / (float)preset.c2.ShakeSeconds;
            float a = (float)preset.c2.ShakeAmp * WorldMapping.UnitsToMeters * k;
            float t = Time.time;
            cam.transform.position += cam.transform.rotation * new Vector3(Mathf.Sin(t * 41f) * a, Mathf.Cos(t * 53f) * a * 0.5f, 0f);
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
