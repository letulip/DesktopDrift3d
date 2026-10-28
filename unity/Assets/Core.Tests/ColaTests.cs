using System;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    [TestFixture]
    public class ColaTests
    {
        private const double Tau = 2 * Math.PI;
        private const double Decay = Tau / 6; // rad/s, as per spec

        // angDelta: returns 0 for equal angles
        [Test]
        public void AngDelta_ZeroForEqualAngles()
        {
            Assert.That(Cola.AngDelta(1, 1), Is.EqualTo(0));
        }

        // angDelta: returns positive delta for CCW step
        [Test]
        public void AngDelta_PositiveForCcwStep()
        {
            Assert.That(Cola.AngDelta(0, 0.5), Is.GreaterThan(0));
        }

        // angDelta: wraps across +π boundary
        [Test]
        public void AngDelta_WrapsAcrossPlusPi()
        {
            double d = Cola.AngDelta(Math.PI * 0.995, -Math.PI * 0.995);
            Assert.That(Math.Abs(d), Is.LessThan(0.1));
        }

        // angDelta: wraps across -π boundary
        [Test]
        public void AngDelta_WrapsAcrossMinusPi()
        {
            double d = Cola.AngDelta(-Math.PI * 0.995, Math.PI * 0.995);
            Assert.That(Math.Abs(d), Is.LessThan(0.1));
        }

        // angDelta: result is always in (-π, π]
        [Test]
        public void AngDelta_ResultAlwaysInRange()
        {
            double[] angles = { 0, 1, -1, Math.PI, -Math.PI, Tau, -Tau, 3.1, -3.1 };
            foreach (double a in angles)
                foreach (double b in angles)
                {
                    double d = Cola.AngDelta(a, b);
                    Assert.That(d > -Math.PI - 1e-9 && d <= Math.PI + 1e-9, Is.True,
                        "angDelta(" + a + ", " + b + ") = " + d + " out of range");
                }
        }

        // capProgress: returns 0 for sweep=0
        [Test]
        public void CapProgress_ZeroForZeroSweep()
        {
            Assert.That(Cola.CapProgress(0), Is.EqualTo(0));
        }

        // capProgress: returns 0.5 for half loop
        [Test]
        public void CapProgress_HalfLoop()
        {
            Assert.That(Cola.CapProgress(Math.PI), Is.EqualTo(0.5));
        }

        // capProgress: returns 1 for exactly one full loop
        [Test]
        public void CapProgress_FullLoop()
        {
            Assert.That(Cola.CapProgress(Tau), Is.EqualTo(1));
        }

        // capProgress: clamps to 1 beyond one loop
        [Test]
        public void CapProgress_ClampsBeyondOneLoop()
        {
            Assert.That(Cola.CapProgress(Tau * 3), Is.EqualTo(1));
        }

        // capProgress: works for negative sweep (CW direction)
        [Test]
        public void CapProgress_NegativeSweep()
        {
            Assert.That(Cola.CapProgress(-Math.PI), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(Cola.CapProgress(-Tau), Is.EqualTo(1));
        }

        // stepSweep: accumulates when engaged
        [Test]
        public void StepSweep_AccumulatesWhenEngaged()
        {
            Assert.That(Cola.StepSweep(0, 0, 0.3, true, 0.016, Decay), Is.GreaterThan(0));
        }

        // stepSweep: accumulates correct delta when engaged
        [Test]
        public void StepSweep_AccumulatesCorrectDelta()
        {
            Assert.That(Cola.StepSweep(1.0, 1.0, 1.4, true, 0.016, Decay), Is.EqualTo(1.4).Within(1e-9));
        }

        // stepSweep: decays toward 0 when idle (positive sweep)
        [Test]
        public void StepSweep_DecaysWhenIdlePositive()
        {
            double s = Cola.StepSweep(1.0, 0, 0, false, 0.1, Decay);
            Assert.That(s < 1.0 && s >= 0, Is.True, "expected drain, got " + s);
        }

        // stepSweep: decays toward 0 when idle (negative sweep)
        [Test]
        public void StepSweep_DecaysWhenIdleNegative()
        {
            double s = Cola.StepSweep(-1.0, 0, 0, false, 0.1, Decay);
            Assert.That(s > -1.0 && s <= 0, Is.True, "expected drain, got " + s);
        }

        // stepSweep: never overshoots 0 from positive side
        [Test]
        public void StepSweep_NoOvershootFromPositive()
        {
            Assert.That(Cola.StepSweep(0.01, 0, 0, false, 10, Decay), Is.EqualTo(0));
        }

        // stepSweep: never overshoots 0 from negative side
        [Test]
        public void StepSweep_NoOvershootFromNegative()
        {
            Assert.That(Cola.StepSweep(-0.01, 0, 0, false, 10, Decay), Is.EqualTo(0));
        }

        // stepSweep: stays 0 when already 0 and idle
        [Test]
        public void StepSweep_StaysZeroWhenIdle()
        {
            Assert.That(Cola.StepSweep(0, 0, 0.5, false, 0.016, Decay), Is.EqualTo(0));
        }
    }
}
