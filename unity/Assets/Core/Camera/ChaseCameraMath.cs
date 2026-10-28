using System;

namespace DesktopDrift.Core
{
    /// <summary>
    /// SPEC-02 §6 (D-08): C2 chase camera. The heading follows the VELOCITY vector (where the car goes),
    /// not the car axis, with exponential lag and a yaw-rate cap. In a drift the car axis deviates from the
    /// camera heading by the slip angle, so the player sees the car's side and its path. Presentation only.
    /// </summary>
    public static class ChaseCameraMath
    {
        const double TwoPi = 2 * Math.PI;

        /// <summary>Angle into (-π, π]; odd-symmetric (Wrap(-a) == -Wrap(a)) away from ±π.</summary>
        public static double Wrap(double a)
        {
            double r = Math.IEEERemainder(a, TwoPi);
            return r <= -Math.PI ? r + TwoPi : r;
        }

        /// <summary>Direction of the velocity vector, or <paramref name="fallback"/> when the car stands still.</summary>
        public static double VelocityYaw(in CarKinematics k, double fallback)
        {
            return CoreMath.Hypot(k.Vx, k.Vy) > 1e-9 ? Math.Atan2(k.Vy, k.Vx) : fallback;
        }

        /// <summary>Camera snapped behind the car axis, no lag.</summary>
        public static CamState Reset(in CarKinematics k, in CameraParams p) => Reset(k, p, CamPhase.Normal);

        /// <summary>Camera snapped behind the car axis, no lag, no drift offset; distance scaled by the phase.</summary>
        public static CamState Reset(in CarKinematics k, in CameraParams p, in CamPhase ph)
        {
            double yaw = Wrap(k.Angle);
            double speedNorm = SpeedNorm(k, CoreMath.Hypot(k.Vx, k.Vy));
            double ahead = p.LookAhead * speedNorm;
            return Pose(k, p, yaw, yaw, speedNorm, Scale(p, ph), 0,
                k.X + Math.Cos(yaw) * ahead, k.Y + Math.Sin(yaw) * ahead);
        }

        public static CamState Step(in CamState s, in CarKinematics k, in CameraParams p, double dt) =>
            Step(s, k, p, CamPhase.Normal, dt);

        /// <summary>
        /// SPEC-02 §8 (C3): C2 plus FOV by speed, sideways offset toward the inside of the arc, distance scale
        /// (phone, countdown fly-in) and a post-finish orbit.
        /// </summary>
        public static CamState Step(in CamState s, in CarKinematics k, in CameraParams p, in CamPhase ph, double dt)
        {
            double speed = CoreMath.Hypot(k.Vx, k.Vy);
            double speedNorm = SpeedNorm(k, speed);
            double lagGain = 1 - Math.Exp(-dt / p.YawLag);
            double yaw, yawTarget, lateralTarget, lookX, lookY;

            if (ph.Orbit)
            {
                // Orbit spins the current heading at a constant rate (no snap to the lagged target); the drift
                // offset and the look-ahead ease out with the yaw lag, so the finish has no visible jump.
                yaw = Wrap(s.Yaw + p.OrbitRateDeg * Math.PI / 180 * dt);
                yawTarget = yaw;
                lateralTarget = 0;
                double keep = 1 - lagGain;
                lookX = k.X + (s.LookX - k.X) * keep;
                lookY = k.Y + (s.LookY - k.Y) * keep;
            }
            else
            {
                double velYaw = VelocityYaw(k, s.YawTarget);
                // Below MinSpeedForVelocityYaw the target blends (short arc) back to the car axis: no spinning at a stop.
                double w = p.MinSpeedForVelocityYaw > 0 ? Clamp(speed / p.MinSpeedForVelocityYaw, 0, 1) : 1;
                yawTarget = Wrap(k.Angle + w * Wrap(velYaw - k.Angle));

                double delta = Wrap(yawTarget - s.Yaw);
                double maxStep = p.MaxYawRateDeg * Math.PI / 180 * dt;
                double step = Clamp(delta * lagGain, -maxStep, maxStep);
                yaw = Wrap(s.Yaw + step);

                // Side velocity in car axes, side = (-sin a, cos a) as in physics; the camera shifts against it.
                double vS = -k.Vx * Math.Sin(k.Angle) + k.Vy * Math.Cos(k.Angle);
                lateralTarget = p.DriftSlipRef > 0 ? -Clamp(vS / p.DriftSlipRef, -1, 1) * p.DriftOffset : 0;

                double ahead = p.LookAhead * speedNorm;
                lookX = k.X + Math.Cos(yawTarget) * ahead;
                lookY = k.Y + Math.Sin(yawTarget) * ahead;
            }

            // The offset lags like the yaw: a slip that flips in one step does not jump the camera.
            double lateral = s.Lateral + (lateralTarget - s.Lateral) * lagGain;
            return Pose(k, p, yaw, yawTarget, speedNorm, Scale(p, ph), lateral, lookX, lookY);
        }

        static double Scale(in CameraParams p, in CamPhase ph)
        {
            double x = Clamp(ph.Intro01, 0, 1);
            double smooth = x * x * (3 - 2 * x);
            return ph.DistanceScale * (1 + (p.IntroScale - 1) * (1 - smooth));
        }

        static double SpeedNorm(in CarKinematics k, double speed)
        {
            return k.MaxSpeed > 0 ? Clamp(speed / k.MaxSpeed, 0, 1) : 0;
        }

        static CamState Pose(in CarKinematics k, CameraParams p, double yaw, double yawTarget, double speedNorm,
            double scale, double lateral, double lookX, double lookY)
        {
            double d = p.Distance * scale;
            return new CamState
            {
                Yaw = yaw,
                YawTarget = yawTarget,
                PosX = k.X - Math.Cos(yaw) * d + -Math.Sin(yaw) * lateral,
                PosY = k.Y - Math.Sin(yaw) * d + Math.Cos(yaw) * lateral,
                PosH = p.Height * scale,
                LookX = lookX,
                LookY = lookY,
                LookH = 0,
                Fov = p.FovMin + (p.FovMax - p.FovMin) * speedNorm,
                Lateral = lateral,
            };
        }

        static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
