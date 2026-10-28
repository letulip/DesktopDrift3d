// SPEC-02 §8: C3 camera polish – FOV by speed, drift offset, mobile distance, countdown fly-in, orbit.
using System;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    public class ChaseCameraC3Tests
    {
        private const double Dt = 1.0 / 60;
        private const double MaxSpeed = 470;
        private const double Deg = Math.PI / 180;

        private static CarKinematics Kin(double x, double y, double angle, double speed, double velAngle)
        {
            return new CarKinematics
            {
                X = x, Y = y, Angle = angle,
                Vx = Math.Cos(velAngle) * speed, Vy = Math.Sin(velAngle) * speed,
                MaxSpeed = MaxSpeed,
            };
        }

        private static void Move(ref CarKinematics k)
        {
            k.X += k.Vx * Dt;
            k.Y += k.Vy * Dt;
        }

        private static double Plan(CamState s, CarKinematics k) =>
            Math.Sqrt((s.PosX - k.X) * (s.PosX - k.X) + (s.PosY - k.Y) * (s.PosY - k.Y));

        // Offset of the eye from the no-drift line car - dir(yaw) * D, projected on sideYaw = (-sin yaw, cos yaw).
        private static double SideOffset(CamState s, CarKinematics k, double d)
        {
            double bx = k.X - Math.Cos(s.Yaw) * d, by = k.Y - Math.Sin(s.Yaw) * d;
            return (s.PosX - bx) * -Math.Sin(s.Yaw) + (s.PosY - by) * Math.Cos(s.Yaw);
        }

        // The C2 formula before C3, kept here as the reference.
        private static double RefYaw(CamState s, CarKinematics k, CameraParams p, out double yawTarget)
        {
            double speed = Math.Sqrt(k.Vx * k.Vx + k.Vy * k.Vy);
            double velYaw = ChaseCameraMath.VelocityYaw(k, s.YawTarget);
            double w = Math.Min(1, speed / p.MinSpeedForVelocityYaw);
            yawTarget = ChaseCameraMath.Wrap(k.Angle + w * ChaseCameraMath.Wrap(velYaw - k.Angle));
            double gain = 1 - Math.Exp(-Dt / p.YawLag);
            double maxStep = p.MaxYawRateDeg * Deg * Dt;
            double step = Math.Max(-maxStep, Math.Min(maxStep, ChaseCameraMath.Wrap(yawTarget - s.Yaw) * gain));
            return ChaseCameraMath.Wrap(s.Yaw + step);
        }

        private static CarKinematics Wiggle(int i, CarKinematics k)
        {
            double a = Math.Sin(i * 0.07) * 1.3;
            k.Angle = a;
            k.Vx = Math.Cos(a - 0.3 * Math.Sin(i * 0.11)) * (200 + 150 * Math.Sin(i * 0.05));
            k.Vy = Math.Sin(a - 0.3 * Math.Sin(i * 0.11)) * (200 + 150 * Math.Sin(i * 0.05));
            Move(ref k);
            return k;
        }

        [Test]
        public void NormalPhaseMatchesC2()
        {
            var p = new CameraParams();
            var p0 = new CameraParams { DriftOffset = 0 };
            CarKinematics k = Kin(10, 20, 0.2, 0, 0.2);
            CamState s = ChaseCameraMath.Reset(k, p), s0 = ChaseCameraMath.Reset(k, p0);
            for (int i = 0; i < 100; i++)
            {
                k = Wiggle(i, k);
                CamState a = ChaseCameraMath.Step(s, k, p, Dt);
                CamState b = ChaseCameraMath.Step(s, k, p, CamPhase.Normal, Dt);
                Assert.AreEqual(a.Yaw, b.Yaw); Assert.AreEqual(a.PosX, b.PosX); Assert.AreEqual(a.PosY, b.PosY);
                Assert.AreEqual(a.PosH, b.PosH); Assert.AreEqual(a.LookX, b.LookX); Assert.AreEqual(a.LookY, b.LookY);

                double yaw = RefYaw(s, k, p, out double yt);
                double ahead = p.LookAhead * Math.Min(1, Math.Sqrt(k.Vx * k.Vx + k.Vy * k.Vy) / MaxSpeed);
                Assert.AreEqual(yaw, a.Yaw, "yaw at step " + i);
                Assert.AreEqual(k.X + Math.Cos(yt) * ahead, a.LookX, 1e-12, "LookX at step " + i);
                Assert.AreEqual(k.Y + Math.Sin(yt) * ahead, a.LookY, 1e-12, "LookY at step " + i);

                // With the drift offset off, the eye is the C2 eye.
                CamState c = ChaseCameraMath.Step(s0, k, p0, Dt);
                double yaw0 = RefYaw(s0, k, p0, out _);
                Assert.AreEqual(k.X - Math.Cos(yaw0) * p0.Distance, c.PosX, "PosX at step " + i);
                Assert.AreEqual(k.Y - Math.Sin(yaw0) * p0.Distance, c.PosY, "PosY at step " + i);
                Assert.AreEqual(p0.Height, c.PosH);
                s = a;
                s0 = c;
            }
        }

        [Test]
        public void FovFollowsSpeed()
        {
            var p = new CameraParams();
            CamState s = ChaseCameraMath.Reset(Kin(0, 0, 0, 0, 0), p);
            Assert.AreEqual(p.FovMin, ChaseCameraMath.Step(s, Kin(0, 0, 0, 0, 0), p, Dt).Fov, 1e-12);
            Assert.AreEqual(p.FovMax, ChaseCameraMath.Step(s, Kin(0, 0, 0, MaxSpeed, 0), p, Dt).Fov, 1e-9);
            Assert.AreEqual((p.FovMin + p.FovMax) / 2, ChaseCameraMath.Step(s, Kin(0, 0, 0, MaxSpeed / 2, 0), p, Dt).Fov, 1e-9);
            Assert.AreEqual(p.FovMax, ChaseCameraMath.Step(s, Kin(0, 0, 0, 2 * MaxSpeed, 0), p, Dt).Fov, 1e-9, "clamped");
            double last = double.NegativeInfinity;
            for (int i = 0; i <= 10; i++)
            {
                double fov = ChaseCameraMath.Step(s, Kin(0, 0, 0, MaxSpeed * i / 10, 0), p, Dt).Fov;
                Assert.Greater(fov, last, "monotone at " + i);
                last = fov;
            }
        }

        private static double DriftOffsetAfter(double velAngle, out double expected)
        {
            var p = new CameraParams();
            CarKinematics k = Kin(0, 0, 0, 300, velAngle);
            CamState s = ChaseCameraMath.Reset(k, p);
            // 600 steps = 28 lag constants: the lagged offset is within 1e-10 of its target.
            for (int i = 0; i < 600; i++)
            {
                Move(ref k);
                s = ChaseCameraMath.Step(s, k, p, Dt);
            }
            double vS = k.Vy; // Angle = 0: side = (0, 1)
            expected = -Math.Max(-1, Math.Min(1, vS / p.DriftSlipRef)) * p.DriftOffset;
            return SideOffset(s, k, p.Distance);
        }

        [Test]
        public void DriftShiftsCameraInsideTheArc()
        {
            var p = new CameraParams();
            // Velocity 20 deg right of the nose (vS < 0): a left-hand drift, the camera moves left.
            double left = DriftOffsetAfter(-20 * Deg, out double e1);
            Assert.AreEqual(p.DriftOffset * Math.Min(1, 300 * Math.Sin(20 * Deg) / p.DriftSlipRef), e1, 1e-12);
            Assert.Greater(left, 0);
            Assert.AreEqual(e1, left, 1e-6);

            double right = DriftOffsetAfter(20 * Deg, out double e2);
            Assert.Less(right, 0);
            Assert.AreEqual(e2, right, 1e-6);
            Assert.AreEqual(-left, right, 1e-6, "mirror");

            Assert.AreEqual(0, DriftOffsetAfter(0, out _), 1e-6, "straight");

            // Partial slip: 5 deg at 300 -> |vS| = 26 < DriftSlipRef, offset scales linearly.
            double part = DriftOffsetAfter(-5 * Deg, out double e3);
            Assert.AreEqual(p.DriftOffset * 300 * Math.Sin(5 * Deg) / p.DriftSlipRef, e3, 1e-9);
            Assert.AreEqual(e3, part, 1e-6);
        }

        [Test]
        public void IntroStartsFarAndFliesIn()
        {
            var p = new CameraParams();
            CarKinematics k = Kin(5, -7, 0.4, 0, 0.4);
            CamState s = ChaseCameraMath.Reset(k, p);
            CamPhase ph = CamPhase.Normal;

            ph.Intro01 = 0;
            Assert.AreEqual(p.Distance * p.IntroScale, Plan(ChaseCameraMath.Reset(k, p, ph), k), 1e-9, "reset at countdown start");
            Assert.AreEqual(p.Height * p.IntroScale, ChaseCameraMath.Reset(k, p, ph).PosH, 1e-9);

            double last = double.PositiveInfinity;
            for (int i = 0; i <= 10; i++)
            {
                ph.Intro01 = i / 10.0;
                double d = Plan(ChaseCameraMath.Step(s, k, p, ph, Dt), k);
                if (i == 0) Assert.AreEqual(p.Distance * p.IntroScale, d, 1e-9);
                if (i == 5) Assert.AreEqual(p.Distance * (1 + p.IntroScale) / 2, d, 1e-9, "smoothstep(0.5) = 0.5");
                if (i == 10) Assert.AreEqual(p.Distance, d, 1e-9);
                Assert.Less(d, last, "monotone at " + i);
                last = d;
            }
        }

        [Test]
        public void MobileScalesDistanceAndHeightNotLook()
        {
            var p = new CameraParams();
            CarKinematics k = Kin(0, 0, 0.3, 300, 0.3);
            CamState s = ChaseCameraMath.Reset(k, p);
            CamPhase ph = CamPhase.Normal;
            ph.DistanceScale = 1 / 0.65;
            Assert.AreEqual(1 / 0.65, p.MobileDistanceScale, 1e-12);
            for (int i = 0; i < 60; i++)
            {
                Move(ref k);
                CamState a = ChaseCameraMath.Step(s, k, p, Dt);
                CamState m = ChaseCameraMath.Step(s, k, p, ph, Dt);
                Assert.AreEqual(Plan(a, k) / 0.65, Plan(m, k), 1e-9);
                Assert.AreEqual(a.PosH / 0.65, m.PosH, 1e-9);
                Assert.AreEqual(a.LookX, m.LookX);
                Assert.AreEqual(a.LookY, m.LookY);
                Assert.AreEqual(a.Fov, m.Fov);
                s = a;
            }
        }

        [Test]
        public void OrbitCirclesStandingCar()
        {
            var p = new CameraParams();
            CarKinematics k = Kin(30, 40, 1.1, 0, 0);
            CamState s = ChaseCameraMath.Reset(k, p);
            CamPhase ph = CamPhase.Normal;
            ph.Orbit = true;
            double rate = p.OrbitRateDeg * Deg * Dt;
            for (int i = 0; i < 60; i++)
            {
                CamState n = ChaseCameraMath.Step(s, k, p, ph, Dt);
                Assert.AreEqual(rate, ChaseCameraMath.Wrap(n.Yaw - s.Yaw), 1e-12, "orbit step " + i);
                Assert.AreEqual(p.Distance, Plan(n, k), 1e-9);
                Assert.AreEqual(k.X, n.LookX);
                Assert.AreEqual(k.Y, n.LookY);
                s = n;
            }
            Assert.AreEqual(ChaseCameraMath.Wrap(1.1 + 60 * rate), s.Yaw, 1e-9);
        }

        [Test]
        public void OrbitStartsWithoutJump()
        {
            // Finish in a drift at speed: the orbit continues from the current pose, no snap of eye or look point.
            var p = new CameraParams();
            CarKinematics k = Kin(0, 0, 0.6, 400, 0.6 - 0.4);
            CamState s = ChaseCameraMath.Reset(k, p);
            for (int i = 0; i < 120; i++)
            {
                Move(ref k);
                s = ChaseCameraMath.Step(s, k, p, Dt);
            }
            CamPhase ph = CamPhase.Normal;
            ph.Orbit = true;
            double bound = (p.Distance + p.DriftOffset) * p.OrbitRateDeg * Deg * Dt + 2 * p.DriftOffset * (Dt / 0.1);
            for (int i = 0; i < 600; i++)
            {
                CamState n = ChaseCameraMath.Step(s, k, p, ph, Dt);
                Assert.LessOrEqual(Math.Sqrt((n.PosX - s.PosX) * (n.PosX - s.PosX) + (n.PosY - s.PosY) * (n.PosY - s.PosY)), bound + 1e-6);
                Assert.LessOrEqual(Math.Sqrt((n.LookX - s.LookX) * (n.LookX - s.LookX) + (n.LookY - s.LookY) * (n.LookY - s.LookY)),
                    p.LookAhead * (1 - Math.Exp(-Dt / p.YawLag)) + 1e-9);
                s = n;
            }
            Assert.AreEqual(p.Distance, Plan(s, k), 1e-6, "offset eased out");
            Assert.AreEqual(k.X, s.LookX, 1e-6);
            Assert.AreEqual(k.Y, s.LookY, 1e-6);
        }

        [Test]
        public void DriftOffsetKeepsTurnContinuous()
        {
            var p = new CameraParams();
            CamState s = ChaseCameraMath.Reset(Kin(0, 0, 0, 0, 0), p);
            // Target jumps by 150 deg while the car slides 0.4 rad: full slip from the first step.
            CarKinematics k = Kin(0, 0, 150 * Deg + 0.4, 400, 150 * Deg);
            double maxStep = p.MaxYawRateDeg * Deg * Dt;
            double bound = (p.Distance + p.DriftOffset) * maxStep + 2 * p.DriftOffset * (Dt / 0.1) + 1e-6;
            for (int i = 0; i < 300; i++)
            {
                CamState n = ChaseCameraMath.Step(s, k, p, Dt);
                foreach (double v in new[] { n.Yaw, n.PosX, n.PosY, n.PosH, n.LookX, n.LookY, n.Fov, n.Lateral })
                    Assert.IsFalse(double.IsNaN(v), "NaN at step " + i);
                Assert.LessOrEqual(Math.Abs(n.Lateral), p.DriftOffset);
                Assert.LessOrEqual(Math.Sqrt((n.PosX - s.PosX) * (n.PosX - s.PosX) + (n.PosY - s.PosY) * (n.PosY - s.PosY)),
                    bound, "position jump at step " + i);
                s = n;
            }
            Assert.Less(Math.Abs(ChaseCameraMath.Wrap(s.Yaw - 150 * Deg)), 1e-3);
            Assert.AreEqual(p.DriftOffset, Math.Abs(s.Lateral), 1e-3, "full slip, full offset");
        }
    }
}
