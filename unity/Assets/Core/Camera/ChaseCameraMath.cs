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
        public static CamState Reset(in CarKinematics k, in CameraParams p)
        {
            double yaw = Wrap(k.Angle);
            return Pose(k, p, yaw, yaw, SpeedNorm(k, CoreMath.Hypot(k.Vx, k.Vy)));
        }

        public static CamState Step(in CamState s, in CarKinematics k, in CameraParams p, double dt)
        {
            double speed = CoreMath.Hypot(k.Vx, k.Vy);
            double velYaw = VelocityYaw(k, s.YawTarget);
            // Below MinSpeedForVelocityYaw the target blends (short arc) back to the car axis: no spinning at a stop.
            double w = p.MinSpeedForVelocityYaw > 0 ? Clamp(speed / p.MinSpeedForVelocityYaw, 0, 1) : 1;
            double yawTarget = Wrap(k.Angle + w * Wrap(velYaw - k.Angle));

            double delta = Wrap(yawTarget - s.Yaw);
            double gain = 1 - Math.Exp(-dt / p.YawLag);
            double maxStep = p.MaxYawRateDeg * Math.PI / 180 * dt;
            double step = Clamp(delta * gain, -maxStep, maxStep);
            double yaw = Wrap(s.Yaw + step);

            return Pose(k, p, yaw, yawTarget, SpeedNorm(k, speed));
        }

        static double SpeedNorm(in CarKinematics k, double speed)
        {
            return k.MaxSpeed > 0 ? Clamp(speed / k.MaxSpeed, 0, 1) : 0;
        }

        static CamState Pose(in CarKinematics k, CameraParams p, double yaw, double yawTarget, double speedNorm)
        {
            double ahead = p.LookAhead * speedNorm;
            return new CamState
            {
                Yaw = yaw,
                YawTarget = yawTarget,
                PosX = k.X - Math.Cos(yaw) * p.Distance,
                PosY = k.Y - Math.Sin(yaw) * p.Distance,
                PosH = p.Height,
                LookX = k.X + Math.Cos(yawTarget) * ahead,
                LookY = k.Y + Math.Sin(yawTarget) * ahead,
                LookH = 0,
            };
        }

        static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
