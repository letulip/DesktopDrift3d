using System;
using NUnit.Framework;

namespace DesktopDrift.Game.Tests
{
    public class SkidMarksTests
    {
        const double Eps = 1e-9;

        [TestCase(41, 61, true)]
        [TestCase(40, 61, false)]
        [TestCase(41, 60, false)]
        [TestCase(40, 60, false)]
        [TestCase(200, 500, true)]
        public void ShouldMark_StrictThresholds(double slip, double speed, bool expected)
        {
            Assert.AreEqual(expected, SkidMarks.ShouldMark(speed, slip));
        }

        [TestCase(0.0)]
        [TestCase(0.7)]
        [TestCase(Math.PI / 2)]
        [TestCase(-2.3)]
        public void SlipOf_PureSidewaysIsSpeed_PureForwardIsZero(double a)
        {
            double v = 150;
            // Forward = (cos a, sin a), side = (-sin a, cos a).
            Assert.AreEqual(v, SkidMarks.SlipOf(-Math.Sin(a) * v, Math.Cos(a) * v, a), Eps);
            Assert.AreEqual(v, SkidMarks.SlipOf(Math.Sin(a) * v, -Math.Cos(a) * v, a), Eps);
            Assert.AreEqual(0, SkidMarks.SlipOf(Math.Cos(a) * v, Math.Sin(a) * v, a), Eps);
        }

        [TestCase(0, 0.05f)]
        [TestCase(100, 0.55f)]
        [TestCase(1000, 0.55f)]
        [TestCase(60, 0.35f)]
        public void Alpha_SixBucketsLike2D(double slip, float expected)
        {
            Assert.AreEqual(expected, SkidMarks.Alpha(slip), 1e-6f);
        }

        [Test]
        public void WheelPoints_AngleZero()
        {
            SkidMarks.WheelPoints(100, 50, 0, out double lx, out double ly, out double rx, out double ry);
            Assert.AreEqual(88, lx, Eps); Assert.AreEqual(62, ly, Eps);
            Assert.AreEqual(88, rx, Eps); Assert.AreEqual(38, ry, Eps);
        }

        [Test]
        public void WheelPoints_AngleHalfPi()
        {
            SkidMarks.WheelPoints(100, 50, Math.PI / 2, out double lx, out double ly, out double rx, out double ry);
            Assert.AreEqual(88, lx, Eps); Assert.AreEqual(38, ly, Eps);
            Assert.AreEqual(112, rx, Eps); Assert.AreEqual(38, ry, Eps);
        }
    }
}
