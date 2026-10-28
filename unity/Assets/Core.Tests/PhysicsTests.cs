// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/tests/physics.test.js @ 5556140 (2026-08-06), ported 2026-10-27.
using System;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    public class PhysicsTests
    {
        // Bismark `_drive` profile (CFG + car override) — inlined so the test pins the physics
        // math, not the live config (which is tuned independently).
        private static DriveProfile MakeProfile()
        {
            return new DriveProfile
            {
                Thrust = 580, MaxSpeed = 470, Steer = 2.2, SteerSmooth = 4.5, LowSpeedTurn = 0.20,
                SelfAlign = 0.82, Grip = 0.98, RollFriction = 0.995, DriftDrag = 0.004, DriftSteerBoost = 1.2,
            };
        }

        // config.js: PHYS_HZ = 120, GRIP_WOBBLE = 0.7, STEER_WOBBLE = 0.16
        private static PhysicsConstants MakeConstants()
        {
            return new PhysicsConstants { PhysHz = 120, GripWobble = 0.7, SteerWobble = 0.16 };
        }

        private const double Dt = 1.0 / 60;
        private const double Eps = 1e-5;

        private struct Golden
        {
            public readonly int I;
            public readonly double X, Y, Vx, Vy, Angle, SteerSmooth;

            public Golden(int i, double x, double y, double vx, double vy, double angle, double steerSmooth)
            {
                I = i; X = x; Y = y; Vx = vx; Vy = vy; Angle = angle; SteerSmooth = steerSmooth;
            }
        }

        // Expected car state at the recorded steps.
        private static readonly Golden[] GoldenRows =
        {
            new Golden(1,  0.159504,   0,          9.570242,   0,          0.00055,  0.075),
            new Golden(10, 8.514045,   0.051443,   91.479714,  1.129742,   0.042859, 0.541418),
            new Golden(40, 105.688556, 23.315309,  242.842036, 127.111312, 0.952639, 0.955775),
            new Golden(41, 109.717303, 25.552082,  241.724767, 134.206354, 0.98506,  0.809092),
            new Golden(80, 274.424315, 179.775095, 323.354674, 251.50645,  0.297955, -0.913506),
        };

        private static void AssertClose(int step, string field, double got, double want)
        {
            Assert.Less(Math.Abs(got - want), Eps, "step " + step + " " + field + ": got " + got + ", want " + want);
        }

        [Test]
        public void StepCar_ReproducesGoldenDriftTrajectory()
        {
            var p = MakeProfile();
            var k = MakeConstants();
            var car = new CarState();
            var sm = new StepMotion();

            for (int i = 1; i <= 80; i++)
            {
                double steerTarget = i <= 40 ? 1 : -1;
                CarPhysics.StepCar(car, sm, steerTarget, p, k, Dt);
                foreach (var g in GoldenRows)
                {
                    if (g.I != i) continue;
                    AssertClose(i, "car.x", car.X, g.X);
                    AssertClose(i, "car.y", car.Y, g.Y);
                    AssertClose(i, "car.vx", car.Vx, g.Vx);
                    AssertClose(i, "car.vy", car.Vy, g.Vy);
                    AssertClose(i, "car.angle", car.Angle, g.Angle);
                    AssertClose(i, "steerSmooth", sm.SteerSmooth, g.SteerSmooth);
                }
            }
        }

        [Test]
        public void StepCar_ReturnsKinematicsSnapshot()
        {
            var p = MakeProfile();
            var k = MakeConstants();
            var car = new CarState { X = 0, Y = 0, Vx = 120, Vy = 40, Angle = 0.3 };
            var sm = new StepMotion();
            var r = CarPhysics.StepCar(car, sm, 1, p, k, Dt);

            Assert.IsFalse(double.IsNaN(r.VS));
            // speed is the pre-step speed (hypot of the entry velocity)
            Assert.Less(Math.Abs(r.Speed - CoreMath.Hypot(120, 40)), Eps);
            // fwd/side are the pre-integration direction vectors
            Assert.Less(Math.Abs(r.Fwd.X - Math.Cos(0.3)), Eps);
            Assert.Less(Math.Abs(r.Fwd.Y - Math.Sin(0.3)), Eps);
            Assert.Less(Math.Abs(r.Side.X - (-Math.Sin(0.3))), Eps);
            Assert.Less(Math.Abs(r.Side.Y - Math.Cos(0.3)), Eps);
        }
    }
}
