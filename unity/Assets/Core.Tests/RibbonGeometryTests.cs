// Gate for RibbonGeometry (js/render.js 190-194, 261-266): the 2D round-stroke ribbon on the oval and two SVG tracks.
// The SVG tracks need the 2D repository (TrackBuilderParityTests.Build fails loudly without it – intended).
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    [TestFixture]
    public class RibbonGeometryTests
    {
        private const int Samples = 3000;
        // An inscribed N-gon sags up to half*(1 - cos(pi/N)) below the circle: 1.92 at N=16 (half=100) – more than the 0.5
        // coverage margin below – and 0.48 at N=32. The coverage gate therefore runs with 32 rim segments (the default 16
        // is a visual-only choice; the exact circle is approximated either way).
        private const int DiscSegments = 32;

        private static List<Vec2> PathOf(IReadOnlyList<Vec2> c, int step)
        {
            var p = new List<Vec2>();
            for (int i = 0; i < c.Count; i += step) p.Add(c[i]);
            return p;
        }

        private static double DistSeg(Vec2 p, Vec2 a, Vec2 b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double l2 = dx * dx + dy * dy;
            double t = l2 == 0.0 ? 0.0 : ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2;
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;
            return CoreMath.Hypot(p.X - (a.X + t * dx), p.Y - (a.Y + t * dy));
        }

        private static double DistPoly(Vec2 p, List<Vec2> path)
        {
            double best = double.MaxValue;
            for (int i = 0; i < path.Count; i++)
            {
                double d = DistSeg(p, path[i], path[(i + 1) % path.Count]);
                if (d < best) best = d;
            }
            return best;
        }

        private static double Cross(Vec2 a, Vec2 b, Vec2 p)
        {
            return (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
        }

        // inclusive: on-edge counts as inside; strict: only clearly interior points count.
        private static bool InAnyTriangle(RibbonGeometry.Ribbon r, Vec2 p, bool strict)
        {
            double eps = strict ? 1e-9 : -1e-9;
            for (int t = 0; t < r.Triangles.Count; t += 3)
            {
                Vec2 a = r.Vertices[r.Triangles[t]], b = r.Vertices[r.Triangles[t + 1]], c = r.Vertices[r.Triangles[t + 2]];
                if (Cross(a, b, p) > eps && Cross(b, c, p) > eps && Cross(c, a, p) > eps) return true;
            }
            return false;
        }

        private static void Check(string id, IReadOnlyList<Vec2> center, double half, int discSegments)
        {
            const int step = 3;
            RibbonGeometry.Ribbon r = RibbonGeometry.Build(center, half, step, discSegments);
            List<Vec2> path = PathOf(center, step);
            Assert.That(r.PathPoints, Is.EqualTo(path.Count), id + " path points");

            int segments = 0;
            for (int i = 0; i < path.Count; i++)
            {
                Vec2 a = path[i], b = path[(i + 1) % path.Count];
                if (CoreMath.Hypot(b.X - a.X, b.Y - a.Y) > 0.0) segments++;
            }
            int triangles = r.Triangles.Count / 3;
            Assert.That(r.Triangles.Count % 3, Is.EqualTo(0));
            Assert.That(triangles, Is.EqualTo(2 * segments + discSegments * path.Count), id + " triangle count");

            // (b) every vertex within half + 1e-9 of the closed polyline
            double maxOver = double.MinValue;
            foreach (Vec2 v in r.Vertices)
            {
                double o = DistPoly(v, path) - half;
                if (o > maxOver) maxOver = o;
            }
            Assert.That(maxOver, Is.LessThanOrEqualTo(1e-9), id + " vertex beyond half");

            // (d) every triangle counter-clockwise
            double minArea = double.MaxValue;
            for (int t = 0; t < r.Triangles.Count; t += 3)
            {
                double area = Cross(r.Vertices[r.Triangles[t]], r.Vertices[r.Triangles[t + 1]], r.Vertices[r.Triangles[t + 2]]) / 2.0;
                if (area < minArea) minArea = area;
            }
            Assert.That(minArea, Is.GreaterThan(0.0), id + " clockwise or degenerate triangle");

            // (c) coverage with a fixed LCG (seed 1)
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (Vec2 p in path)
            {
                minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
            }
            minX -= half; minY -= half; maxX += half; maxY += half;
            ulong state = 1;
            int inside = 0, outside = 0;
            for (int s = 0; s < Samples; s++)
            {
                state = state * 6364136223846793005UL + 1442695040888963407UL;
                double u = (state >> 11) / 9007199254740992.0;
                state = state * 6364136223846793005UL + 1442695040888963407UL;
                double w = (state >> 11) / 9007199254740992.0;
                var p = new Vec2(minX + u * (maxX - minX), minY + w * (maxY - minY));
                double d = DistPoly(p, path);
                if (d < half - 0.5)
                {
                    inside++;
                    Assert.That(InAnyTriangle(r, p, false), id + " uncovered sample at (" + p.X + ", " + p.Y + ") d=" + d);
                }
                else if (d > half + 0.5)
                {
                    outside++;
                    Assert.That(!InAnyTriangle(r, p, true), id + " overpaint sample at (" + p.X + ", " + p.Y + ") d=" + d);
                }
            }
            Assert.That(inside, Is.GreaterThan(100), id + " too few inside samples");
            Assert.That(outside, Is.GreaterThan(100), id + " too few outside samples");
            TestContext.Progress.WriteLine("RIBBON " + id + ": pathPoints=" + path.Count + " segments=" + segments + " triangles=" + triangles +
                " maxVertexDist-half=" + maxOver.ToString("R", CultureInfo.InvariantCulture) + " inside=" + inside + " outside=" + outside);
        }

        [Test]
        public void Oval()
        {
            TrackData t = OvalTrack.Build();
            Check("oval", t.Center, t.TrackHalf, DiscSegments);
        }

        [Test]
        public void CafeMarble()
        {
            TrackData t = TrackBuilderParityTests.Build("cafe-marble");
            Check("cafe-marble", t.Center, t.TrackHalf, DiscSegments);
        }

        [Test]
        public void DiningOak()
        {
            TrackData t = TrackBuilderParityTests.Build("dining-oak");
            Check("dining-oak", t.Center, t.TrackHalf, DiscSegments);
        }
    }
}
