using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    // Port of DesktopDrift/tests/tire-seed.test.js @ 5556140 – same numbers, same names (in comments).
    [TestFixture]
    public class TireSeedTests
    {
        // JS makeCircle(R, N, cw = false): synthesises center/inner/outer directly.
        private static (List<Vec2> Center, List<Vec2> Inner, List<Vec2> Outer) MakeCircle(double R, int N, bool cw = false)
        {
            var center = new List<Vec2>(); var inner = new List<Vec2>(); var outer = new List<Vec2>();
            const double Half = 30;
            for (int i = 0; i < N; i++)
            {
                double a = (cw ? -1 : 1) * ((double)i / N) * Math.PI * 2;
                double cx = Math.Cos(a) * R, cy = Math.Sin(a) * R;
                double tx = -Math.Sin(a) * (cw ? -1 : 1), ty = Math.Cos(a) * (cw ? -1 : 1);
                double nx = -ty, ny = tx; // left normal of the tangent
                center.Add(new Vec2(cx, cy));
                outer.Add(new Vec2(cx + nx * Half, cy + ny * Half));
                inner.Add(new Vec2(cx - nx * Half, cy - ny * Half));
            }
            return (center, inner, outer);
        }

        // test('seedTires: returns exactly n points')
        [Test]
        public void SeedTires_ReturnsExactlyN()
        {
            var (center, inner, outer) = MakeCircle(500, 200);
            Assert.AreEqual(12, TireSeed.SeedTires(center, inner, outer, 12).Count);
            Assert.AreEqual(1, TireSeed.SeedTires(center, inner, outer, 1).Count);
        }

        // test('seedTires: empty / non-positive n → []')
        [Test]
        public void SeedTires_EmptyOrNonPositiveN()
        {
            var (center, inner, outer) = MakeCircle(500, 200);
            Assert.IsEmpty(TireSeed.SeedTires(center, inner, outer, 0));
            Assert.IsEmpty(TireSeed.SeedTires(new List<Vec2>(), new List<Vec2>(), new List<Vec2>(), 5));
        }

        // test('seedTires: on a curve, tires are pushed toward the concave (inner-of-corner) side')
        [Test]
        public void SeedTires_PushedTowardConcaveSide()
        {
            double R = 500;
            var (center, inner, outer) = MakeCircle(R, 200);
            var pts = TireSeed.SeedTires(center, inner, outer, 8);
            foreach (var p in pts)
            {
                double r = CoreMath.Hypot(p.X, p.Y);
                Assert.IsTrue(r < R - 1, "expected offset inward: r=" + r + " (R=" + R + ")");
                Assert.IsTrue(r > R - 40, "should not overshoot past the inner edge (HALF=30): r=" + r);
            }
        }

        // test('seedTires: very gentle curvature → negligible offset (≈ on the racing line)')
        [Test]
        public void SeedTires_GentleCurvatureNegligibleOffset()
        {
            double R = 100000; int N = 400;
            var (center, inner, outer) = MakeCircle(R, N);
            var pts = TireSeed.SeedTires(center, inner, outer, 6);
            foreach (var p in pts)
            {
                double r = CoreMath.Hypot(p.X, p.Y);
                Assert.IsTrue(Math.Abs(r - R) < R * 1e-3, "near-straight → ≈centerline: |r-R|=" + Math.Abs(r - R));
            }
        }

        // test('seedTires: deterministic (stable capId source)')
        [Test]
        public void SeedTires_Deterministic()
        {
            var (center, inner, outer) = MakeCircle(500, 200);
            var a = TireSeed.SeedTires(center, inner, outer, 10);
            var b = TireSeed.SeedTires(center, inner, outer, 10);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].X, b[i].X);
                Assert.AreEqual(a[i].Y, b[i].Y);
            }
        }
    }
}
