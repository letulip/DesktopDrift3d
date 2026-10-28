using NUnit.Framework;

namespace DesktopDrift.Game.Tests
{
    public class SteerInputTests
    {
        const double W = 1000;
        static int R(double[] xs, bool l = false, bool r = false) => SteerInput.Resolve(xs, l, r, W);

        [Test] public void EmptyIsZero() => Assert.AreEqual(0, R(new double[0]));
        [Test] public void LeftHalf() => Assert.AreEqual(-1, R(new[] { 100.0 }));
        [Test] public void RightHalf() => Assert.AreEqual(1, R(new[] { 900.0 }));
        [Test] public void TwoLeftIsSignNotSum() => Assert.AreEqual(-1, R(new[] { 100.0, 200.0 }));
        [Test] public void TwoRightIsSignNotSum() => Assert.AreEqual(1, R(new[] { 600.0, 800.0 }));
        [Test] public void OneEachIsZero() => Assert.AreEqual(0, R(new[] { 100.0, 900.0 }));
        [Test] public void TwoLeftOneRight() => Assert.AreEqual(-1, R(new[] { 100.0, 200.0, 900.0 }));
        [Test] public void KeyBeatsPointer() => Assert.AreEqual(-1, R(new[] { 900.0 }, l: true));
        [Test] public void BothKeysFallBackToPointer() => Assert.AreEqual(-1, R(new[] { 100.0 }, true, true));
        [Test] public void ExactlyHalfIsRight() => Assert.AreEqual(1, R(new[] { 500.0 }));
    }
}
