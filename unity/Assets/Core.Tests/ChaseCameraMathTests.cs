// SPEC-02 §6 (D-08): C2 chase camera – continuity, convergence, stop, left/right symmetry, drift, yaw-rate cap.
using System;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    public class ChaseCameraMathTests
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

        private static double MaxStep(CameraParams p) => p.MaxYawRateDeg * Deg * Dt;

        private static void AssertNoNaN(CamState s)
        {
            foreach (double v in new[] { s.Yaw, s.YawTarget, s.PosX, s.PosY, s.PosH, s.LookX, s.LookY, s.LookH })
                Assert.IsFalse(double.IsNaN(v), "NaN in CamState");
        }

        [Test]
        public void TurnIsContinuousAndConverges()
        {
            var p = new CameraParams();
            CamState s = ChaseCameraMath.Reset(Kin(0, 0, 0, 0, 0), p);
            CarKinematics k = Kin(0, 0, 150 * Deg, 400, 150 * Deg);
            for (int i = 0; i < 300; i++)
            {
                CamState n = ChaseCameraMath.Step(s, k, p, Dt);
                Assert.LessOrEqual(Math.Abs(ChaseCameraMath.Wrap(n.Yaw - s.Yaw)), MaxStep(p) + 1e-9, "yaw jump at step " + i);
                Assert.LessOrEqual(Math.Sqrt((n.PosX - s.PosX) * (n.PosX - s.PosX) + (n.PosY - s.PosY) * (n.PosY - s.PosY)),
                    p.Distance * MaxStep(p) + 1e-6, "position jump at step " + i);
                s = n;
            }
            Assert.Less(Math.Abs(ChaseCameraMath.Wrap(s.Yaw - 150 * Deg)), 1e-3);
        }

        [Test]
        public void ConvergesBehindCarOnStraight()
        {
            // Starts behind a car heading 0, then drives along 0.3: 360 steps (6 s) bring the yaw error
            // below 1e-3 / Distance, so the 1e-3 position tolerance holds (180 steps leave ~0.03 units).
            var p = new CameraParams();
            CamState s = ChaseCameraMath.Reset(Kin(0, 0, 0, 0, 0), p);
            CarKinematics k = Kin(0, 0, 0.3, 300, 0.3);
            for (int i = 0; i < 360; i++)
            {
                Move(ref k);
                s = ChaseCameraMath.Step(s, k, p, Dt);
            }
            Assert.Less(Math.Abs(ChaseCameraMath.Wrap(s.Yaw - 0.3)), 1e-3);
            Assert.AreEqual(k.X - Math.Cos(0.3) * p.Distance, s.PosX, 1e-3);
            Assert.AreEqual(k.Y - Math.Sin(0.3) * p.Distance, s.PosY, 1e-3);
            Assert.AreEqual(p.Height, s.PosH);
            Assert.AreEqual(k.X + Math.Cos(0.3) * p.LookAhead * (300 / MaxSpeed), s.LookX, 1e-3);
            Assert.AreEqual(k.Y + Math.Sin(0.3) * p.LookAhead * (300 / MaxSpeed), s.LookY, 1e-3);
        }

        [Test]
        public void StandingCarKeepsCameraStill()
        {
            var p = new CameraParams();
            CarKinematics k = Kin(0, 0, 1.0, 0, 0);
            CamState s0 = ChaseCameraMath.Reset(k, p);
            CamState s = s0;
            for (int i = 0; i < 120; i++)
            {
                s = ChaseCameraMath.Step(s, k, p, Dt);
                AssertNoNaN(s);
            }
            Assert.AreEqual(1.0, s.Yaw);
            Assert.AreEqual(s0.PosX, s.PosX);
            Assert.AreEqual(s0.PosY, s.PosY);

            // Crawling at 5 units/s sideways-backwards: weight 5/80 keeps the heading near the car axis.
            k = Kin(0, 0, 1.0, 5, 1.0 + 2.0);
            for (int i = 0; i < 120; i++)
            {
                Move(ref k);
                s = ChaseCameraMath.Step(s, k, p, Dt);
                AssertNoNaN(s);
            }
            Assert.Less(Math.Abs(ChaseCameraMath.Wrap(s.Yaw - 1.0)), 0.2);
        }

        [Test]
        public void LeftAndRightAreMirrorImages()
        {
            var p = new CameraParams();
            CarKinematics k = Kin(0, 0, 0, 0, 0);
            CarKinematics m = Kin(0, 0, 0, 0, 0);
            CamState s = ChaseCameraMath.Reset(k, p);
            CamState sm = ChaseCameraMath.Reset(m, p);
            double angle = 0;
            for (int i = 0; i < 200; i++)
            {
                angle += 0.01 * i;
                k.Angle = angle;
                k.Vx = Math.Cos(angle - 0.1) * 300;
                k.Vy = Math.Sin(angle - 0.1) * 300;
                Move(ref k);
                m = new CarKinematics { X = k.X, Y = -k.Y, Angle = -k.Angle, Vx = k.Vx, Vy = -k.Vy, MaxSpeed = k.MaxSpeed };
                s = ChaseCameraMath.Step(s, k, p, Dt);
                sm = ChaseCameraMath.Step(sm, m, p, Dt);
                Assert.AreEqual(-s.Yaw, sm.Yaw, 1e-12, "yaw at step " + i);
                Assert.AreEqual(s.PosX, sm.PosX, 1e-9, "PosX at step " + i);
                Assert.AreEqual(-s.PosY, sm.PosY, 1e-9, "PosY at step " + i);
                Assert.AreEqual(-s.LookY, sm.LookY, 1e-9, "LookY at step " + i);
            }
        }

        [Test]
        public void DriftTurnsCarInsideFrame()
        {
            var p = new CameraParams();
            CarKinematics k = Kin(0, 0, 0.5, 350, 0.5 - 0.35);
            CamState s = ChaseCameraMath.Reset(k, p);
            for (int i = 0; i < 180; i++)
            {
                Move(ref k);
                s = ChaseCameraMath.Step(s, k, p, Dt);
            }
            Assert.Less(Math.Abs(ChaseCameraMath.Wrap(s.Yaw - (0.5 - 0.35))), 1e-3, "heading follows velocity");
            Assert.AreEqual(0.35, ChaseCameraMath.Wrap(k.Angle - s.Yaw), 1e-3, "car axis turned by the slip angle");
        }

        [Test]
        public void YawRateIsCapped()
        {
            var p = new CameraParams { YawLag = 0.001 };
            CamState s = ChaseCameraMath.Reset(Kin(0, 0, 0, 0, 0), p);
            CarKinematics k = Kin(0, 0, 90 * Deg, 400, 90 * Deg);
            double maxStep = MaxStep(p);
            int capped = 0;
            for (int i = 0; i < 60; i++)
            {
                double rem = ChaseCameraMath.Wrap(90 * Deg - s.Yaw);
                CamState n = ChaseCameraMath.Step(s, k, p, Dt);
                if (rem > 2 * maxStep)
                {
                    Assert.AreEqual(maxStep, ChaseCameraMath.Wrap(n.Yaw - s.Yaw), 1e-12, "capped step " + i);
                    capped++;
                }
                s = n;
            }
            Assert.GreaterOrEqual(capped, 28);
            Assert.AreEqual(90 * Deg, s.Yaw, 1e-9);
        }

        [Test]
        public void ResetSnapsBehindCarAxis()
        {
            var p = new CameraParams();
            CarKinematics k = Kin(120, -45, 0.7, 200, 0.4);
            CamState s = ChaseCameraMath.Reset(k, p);
            Assert.AreEqual(0.7, s.Yaw);
            Assert.AreEqual(0.7, s.YawTarget);
            Assert.AreEqual(k.X - Math.Cos(0.7) * p.Distance, s.PosX, 1e-9);
            Assert.AreEqual(k.Y - Math.Sin(0.7) * p.Distance, s.PosY, 1e-9);
            Assert.AreEqual(p.Distance, Math.Sqrt((k.X - s.PosX) * (k.X - s.PosX) + (k.Y - s.PosY) * (k.Y - s.PosY)), 1e-9);
            Assert.AreEqual(p.Height, s.PosH);
        }

        [Test]
        public void WrapIsHalfOpenAroundZero()
        {
            Assert.AreEqual(Math.PI, ChaseCameraMath.Wrap(Math.PI));
            Assert.AreEqual(Math.PI, ChaseCameraMath.Wrap(-Math.PI));
            Assert.AreEqual(0.5, ChaseCameraMath.Wrap(0.5 + 4 * Math.PI), 1e-12);
            Assert.AreEqual(-0.5, ChaseCameraMath.Wrap(-0.5 - 4 * Math.PI), 1e-12);
        }
    }
}
