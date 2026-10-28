using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    // Port of DesktopDrift/tests/collision.test.js @ 5556140 – same numbers, same names (in comments).
    [TestFixture]
    public class CollisionTests
    {
        private const double Eps = 1e-5; // JS EPS
        private const double ConeR = 18, Cr = 30, NmBand = 42;
        private const double CpR = 30;

        private static TableRect TableRectShape() => new TableRect { W = 3400, H = 2900, Round = false };
        private static TableRect TableRound() => new TableRect { W = 3400, H = 2900, Round = true };
        private static CarState FastCar(double x, double y) => new CarState { X = x, Y = y, Vx = 200, Vy = 0 };
        private static CarState SlowCar(double x, double y) => new CarState { X = x, Y = y, Vx = 100, Vy = 0 };
        private static readonly IReadOnlyList<Vec2> Cps = new[] { new Vec2(0, 0), new Vec2(100, 0), new Vec2(100, 100), new Vec2(0, 100) };
        private static readonly IReadOnlyList<Cone> NoCones = new Cone[0];
        private static readonly IReadOnlyList<Prop> NoProps = new Prop[0];

        // JS literal { x, y, r, hl, _cos, _sin } (no ang): cos/sin given directly.
        private static Prop MakeProp(double x, double y, double r, double hl, double cos, double sin) =>
            new Prop { X = x, Y = y, R = r, Hl = hl, Cos = cos, Sin = sin };

        private static Vec2[] BodyPtsOf(CarState car, double hx, double hy, double nose) => new[]
        {
            new Vec2(car.X + hx * nose, car.Y + hy * nose),
            new Vec2(car.X, car.Y),
            new Vec2(car.X - hx * nose, car.Y - hy * nose),
        };

        private static Cone MakeCone(double x, double y, double vx, double vy, double spin = 0) =>
            new Cone { X = x, Y = y, Vx = vx, Vy = vy, Ang = 0, Spin = spin, Knocked = true };

        // nearMiss: below speed gate → false regardless of position
        [Test]
        public void NearMiss_BelowSpeedGate_False()
        {
            var car = SlowCar(0, 0);
            var cone = new Cone { X = ConeR + Cr + 10, Y = 0, Knocked = false };
            Assert.That(Collision.NearMiss(car, new[] { cone }, NoProps, TableRectShape(), ConeR, Cr, NmBand), Is.False);
        }

        // nearMiss: prop just inside the band → true
        [Test]
        public void NearMiss_PropJustInsideBand_True()
        {
            var gap = NmBand / 2;
            var prop = MakeProp(0, Cr + 20 + gap, 20, 0, 1, 0);
            Assert.That(Collision.NearMiss(FastCar(0, 0), NoCones, new[] { prop }, TableRectShape(), ConeR, Cr, NmBand), Is.True);
        }

        // nearMiss: prop well clear of band → false
        [Test]
        public void NearMiss_PropWellClearOfBand_False()
        {
            var prop = MakeProp(0, Cr + 20 + NmBand + 50, 20, 0, 1, 0);
            Assert.That(Collision.NearMiss(FastCar(0, 0), NoCones, new[] { prop }, TableRectShape(), ConeR, Cr, NmBand), Is.False);
        }

        // nearMiss: standing cone just inside band → true
        [Test]
        public void NearMiss_StandingConeJustInsideBand_True()
        {
            var gap = NmBand / 2;
            var cone = new Cone { X = ConeR + Cr + gap, Y = 0, Knocked = false };
            Assert.That(Collision.NearMiss(FastCar(0, 0), new[] { cone }, NoProps, TableRectShape(), ConeR, Cr, NmBand), Is.True);
        }

        // nearMiss: knocked cone is ignored
        [Test]
        public void NearMiss_KnockedConeIgnored()
        {
            var gap = NmBand / 2;
            var cone = new Cone { X = ConeR + Cr + gap, Y = 0, Knocked = true };
            Assert.That(Collision.NearMiss(FastCar(0, 0), new[] { cone }, NoProps, TableRectShape(), ConeR, Cr, NmBand), Is.False);
        }

        // nearMiss: rect table edge just inside band → true
        [Test]
        public void NearMiss_RectTableEdgeJustInsideBand_True()
        {
            var gap = NmBand / 2;
            var car = FastCar(TableRectShape().W / 2 - Cr - gap, 0);
            Assert.That(Collision.NearMiss(car, NoCones, NoProps, TableRectShape(), ConeR, Cr, NmBand), Is.True);
        }

        // finishDot: returns positive when car is past the line, negative when behind
        [Test]
        public void FinishDot_SignPastBehindOnLine()
        {
            var c0 = new Vec2(0, 0);
            Assert.That(Collision.FinishDot(new CarState { X = 10, Y = 0 }, c0, 1, 0), Is.GreaterThan(0), "past line → positive");
            Assert.That(Collision.FinishDot(new CarState { X = -10, Y = 0 }, c0, 1, 0), Is.LessThan(0), "behind line → negative");
            Assert.That(Collision.FinishDot(new CarState { X = 0, Y = 0 }, c0, 1, 0), Is.EqualTo(0), "on line → 0");
            Assert.That(Collision.FinishDot(new CarState { X = 5, Y = 999 }, c0, 1, 0), Is.GreaterThan(0), "lateral offset irrelevant");
        }

        // crossedFinish: approaching (neg→pos) → true
        [Test]
        public void CrossedFinish_Approaching_True()
        {
            Assert.That(Collision.CrossedFinish(-1, 1), Is.True);
            Assert.That(Collision.CrossedFinish(-0.1, 0), Is.True); // dot exactly 0 counts as crossing
        }

        // crossedFinish: receding, same side, or exact-0 prevDot → false
        [Test]
        public void CrossedFinish_RecedingSameSideOrZeroPrev_False()
        {
            Assert.That(Collision.CrossedFinish(1, -1), Is.False); // receding
            Assert.That(Collision.CrossedFinish(-1, -1), Is.False); // still behind
            Assert.That(Collision.CrossedFinish(1, 1), Is.False); // already past
            Assert.That(Collision.CrossedFinish(0, 1), Is.False); // prevDot=0 is not < 0
        }

        // advanceCheckpoint: car far from the current checkpoint → index unchanged
        [Test]
        public void AdvanceCheckpoint_FarFromCurrent_Unchanged()
        {
            Assert.That(Collision.AdvanceCheckpoint(1, 500, 500, Cps, CpR), Is.EqualTo(1));
        }

        // advanceCheckpoint: car within CP_R of the current checkpoint → index advances
        [Test]
        public void AdvanceCheckpoint_WithinRadius_Advances()
        {
            Assert.That(Collision.AdvanceCheckpoint(1, 110, 0, Cps, CpR), Is.EqualTo(2));
        }

        // advanceCheckpoint: reaching the LAST checkpoint wraps the index to 0
        [Test]
        public void AdvanceCheckpoint_LastCheckpoint_WrapsToZero()
        {
            Assert.That(Collision.AdvanceCheckpoint(3, 0, 100, Cps, CpR), Is.EqualTo(0));
        }

        // advanceCheckpoint: ANTI-SKIP — sitting on a later checkpoint does not jump ahead
        [Test]
        public void AdvanceCheckpoint_AntiSkip_LaterCheckpointDoesNotJumpAhead()
        {
            Assert.That(Collision.AdvanceCheckpoint(1, 0, 100, Cps, CpR), Is.EqualTo(1));
        }

        // advanceCheckpoint: undefined checkpoint at the index → index unchanged
        [Test]
        public void AdvanceCheckpoint_NoCheckpointAtIndex_Unchanged()
        {
            Assert.That(Collision.AdvanceCheckpoint(9, 0, 0, Cps, CpR), Is.EqualTo(9));
        }

        // nearMiss: capsule prop — nearest point on segment used, not centre
        [Test]
        public void NearMiss_CapsuleProp_NearestSegmentPointUsed()
        {
            var prop = MakeProp(200, 0, 20, 100, 1, 0);
            var car = FastCar(200, Cr + 20 + NmBand / 2);
            Assert.That(Collision.NearMiss(car, NoCones, new[] { prop }, TableRectShape(), ConeR, Cr, NmBand), Is.True);
            var farCar = FastCar(200, Cr + 20 + NmBand + 50);
            Assert.That(Collision.NearMiss(farCar, NoCones, new[] { prop }, TableRectShape(), ConeR, Cr, NmBand), Is.False);
        }

        // resolveWall — rect wall pushback + impact (golden)
        [Test]
        public void ResolveWall_RectWall_Golden()
        {
            var table = new TableRect { W = 3400, H = 2900, Round = false };
            double hx = 1, hy = 0, nose = 25, rr = 40;
            var car = new CarState { X = 1660, Y = 0, Vx = 300, Vy = 50 };
            var hit = Collision.ResolveWall(car, table, rr, hx, hy, nose, BodyPtsOf(car, hx, hy, nose));
            Assert.That(car.X, Is.EqualTo(1635).Within(Eps), "x");
            Assert.That(car.Y, Is.EqualTo(0).Within(Eps), "y");
            Assert.That(car.Vx, Is.EqualTo(-90).Within(Eps), "vx");
            Assert.That(car.Vy, Is.EqualTo(42.5).Within(Eps), "vy");
            Assert.That(hit, Is.EqualTo(300).Within(Eps), "hit");
        }

        // resolveWall — round wall pushback + impact (golden)
        [Test]
        public void ResolveWall_RoundWall_Golden()
        {
            var table = new TableRect { W = 2000, H = 2000, Round = true };
            double hx = Math.Cos(0.3), hy = Math.Sin(0.3), nose = 25, rr = 40;
            var car = new CarState { X = 950, Y = 120, Vx = 200, Vy = 60 };
            var hit = Collision.ResolveWall(car, table, rr, hx, hy, nose, BodyPtsOf(car, hx, hy, nose));
            Assert.That(car.X, Is.EqualTo(928.007825).Within(Eps), "x");
            Assert.That(car.Y, Is.EqualTo(117.123332).Within(Eps), "y");
            Assert.That(car.Vx, Is.EqualTo(-65.657399).Within(Eps), "vx");
            Assert.That(car.Vy, Is.EqualTo(25.250906).Within(Eps), "vy");
            Assert.That(hit, Is.EqualTo(206.092633).Within(Eps), "hit");
        }

        // resolveProps — circular prop pushout + reflection (golden)
        [Test]
        public void ResolveProps_CircularProp_Golden()
        {
            double rr = 40, hx = 1, hy = 0, nose = 25;
            var props = new[] { MakeProp(100, 0, 30, 0, 1, 0) };
            var car = new CarState { X = 60, Y = 0, Vx = 250, Vy = 0 };
            var crash = Collision.ResolveProps(car, props, rr, BodyPtsOf(car, hx, hy, nose));
            Assert.That(car.X, Is.EqualTo(5).Within(Eps), "x");
            Assert.That(car.Y, Is.EqualTo(0).Within(Eps), "y");
            Assert.That(car.Vx, Is.EqualTo(-100).Within(Eps), "vx");
            Assert.That(car.Vy, Is.EqualTo(0).Within(Eps), "vy");
            Assert.That(crash, Is.EqualTo(250).Within(Eps), "crash");
        }

        // resolveWall/resolveProps — no contact leaves the car untouched
        [Test]
        public void ResolveWallAndProps_NoContact_CarUntouched()
        {
            var table = new TableRect { W = 3400, H = 2900, Round = false };
            double hx = 1, hy = 0, nose = 25, rr = 40;
            var car = new CarState { X = 0, Y = 0, Vx = 100, Vy = 0 };
            Assert.That(Collision.ResolveWall(car, table, rr, hx, hy, nose, BodyPtsOf(car, hx, hy, nose)), Is.EqualTo(0));
            Assert.That(Collision.ResolveProps(car, new[] { MakeProp(1000, 1000, 20, 0, 1, 0) }, rr, BodyPtsOf(car, hx, hy, nose)), Is.EqualTo(0));
            Assert.That(car.X, Is.EqualTo(0).Within(Eps), "x");
            Assert.That(car.Vx, Is.EqualTo(100).Within(Eps), "vx");
        }

        // stepKnockedCone: free cone — position advances, velocity decays, angle updates
        [Test]
        public void StepKnockedCone_FreeCone_AdvancesAndDecays()
        {
            var c = MakeCone(0, 0, 100, 50, 2);
            double dt = 1.0 / 60, fAdj = dt * 60; // PHYS_HZ=60
            Collision.StepKnockedCone(c, NoProps, ConeR, dt, fAdj);
            var dAdj = Math.Pow(0.9, fAdj);
            Assert.That(c.X, Is.EqualTo(100 * dt).Within(1e-9));
            Assert.That(c.Y, Is.EqualTo(50 * dt).Within(1e-9));
            Assert.That(c.Vx, Is.EqualTo(100 * dAdj).Within(1e-9));
            Assert.That(c.Vy, Is.EqualTo(50 * dAdj).Within(1e-9));
            Assert.That(c.Ang, Is.EqualTo(2 * dt).Within(1e-9)); // spin * dt before spin decays
            Assert.That(c.Spin, Is.EqualTo(2 * dAdj).Within(1e-9));
        }

        // stepKnockedCone: cone overlapping point prop — pushed out, velocity damped
        [Test]
        public void StepKnockedCone_OverlappingPointProp_PushedOutAndDamped()
        {
            double propR = 20, overlap = 1;
            var propY = ConeR + propR - overlap; // 37
            var prop = MakeProp(0, propY, propR, 0, 1, 0);
            var c = MakeCone(0, 0, 0, 100, 0);
            double dt = 1.0 / 60, fAdj = 1;
            Collision.StepKnockedCone(c, new[] { prop }, ConeR, dt, fAdj);
            Assert.That(c.Y, Is.EqualTo(propY - (ConeR + propR)).Within(1e-6));
            Assert.That(c.Vy, Is.LessThan(100 * Math.Pow(0.9, fAdj)), $"vy should be less than free-decay value, got {c.Vy}");
        }

        // stepKnockedCone: cone clear of all props — no pushback
        [Test]
        public void StepKnockedCone_ClearOfProps_NoPushback()
        {
            var prop = MakeProp(500, 500, 20, 0, 1, 0);
            var c = MakeCone(0, 0, 10, 0, 0);
            var x0 = c.X + 10.0 / 60;
            Collision.StepKnockedCone(c, new[] { prop }, ConeR, 1.0 / 60, 1);
            Assert.That(c.X, Is.EqualTo(x0).Within(1e-9));
        }
    }
}
