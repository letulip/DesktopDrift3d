using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    // Port of DesktopDrift/tests/track-util.test.js @ 5556140 – same numbers, same names (in comments).
    [TestFixture]
    public class TrackUtilTests
    {
        private static void Near(double a, double b, double eps = 1e-9)
        {
            Assert.IsTrue(Math.Abs(a - b) < eps, a + " ≈ " + b);
        }

        private static void AssertPt(double x, double y, Vec2 p)
        {
            Assert.AreEqual(x, p.X);
            Assert.AreEqual(y, p.Y);
        }

        // JS center.indexOf(cp) / center.includes(cp) – the checkpoints are centreline points.
        private static int IndexOfPt(IReadOnlyList<Vec2> list, Vec2 p)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].X == p.X && list[i].Y == p.Y) return i;
            return -1;
        }

        private static Collectible Tile(string capId, double x, double y) => new Collectible { CapId = capId, X = x, Y = y };

        // test('reconcileTires: prunes orphans + dupes to the current tiles (never exceeds tile count)')
        [Test]
        public void ReconcileTires_PrunesOrphansAndDupes()
        {
            var tiles = Enumerable.Range(0, 12).Select(k => Tile("t" + k, k * 10, 0)).ToList();
            // A bloated store: index-keyed collects, legacy coordinate keys, orphans, and a duplicate.
            var stored = new[] { "t0", "t1", "20,0", "30,0", "999,999", "888,111", "t0" };
            var output = TrackUtil.ReconcileTires(stored, tiles);
            CollectionAssert.AreEqual(new[] { "t0", "t1", "t2", "t3" }, output.OrderBy(s => s, StringComparer.Ordinal).ToList());
            Assert.IsTrue(output.Count <= tiles.Count, "count can never exceed the tile total");
        }

        // test('reconcileTires: empty store collects nothing')
        [Test]
        public void ReconcileTires_EmptyStoreCollectsNothing()
        {
            Assert.IsEmpty(TrackUtil.ReconcileTires(new string[0], new[] { Tile("t0", 0, 0) }));
        }

        // test('filterConesOnTrack: keeps edge cones, culls cones inside a far-away lane')
        [Test]
        public void FilterConesOnTrack_KeepsEdgeCullsIntruder()
        {
            var center = Enumerable.Range(0, 200).Select(i => new Vec2(i * 20, 0)).ToList();
            var edge = new Cone { X = center[10].X, Y = 100, Ci = 10 };       // legit: 100gu off its own segment
            var intruder = new Cone { X = center[100].X, Y = 0, Ci = 10 };    // sits ON the centreline far from ci=10
            var output = TrackUtil.FilterConesOnTrack(new[] { edge, intruder }, center, 100);
            Assert.IsTrue(output.Contains(edge), "legit edge cone kept");
            Assert.IsTrue(!output.Contains(intruder), "intruding cone culled");
        }

        // test('filterConesOnTrack: a simple loop with no self-crossing loses no cones')
        [Test]
        public void FilterConesOnTrack_SimpleLoopLosesNothing()
        {
            int N = 60; double R = 300, half = 100;
            var center = Enumerable.Range(0, N).Select(i =>
            {
                double a = 2 * Math.PI * i / N; return new Vec2(R * Math.Cos(a), R * Math.Sin(a));
            }).ToList();
            var cones = Enumerable.Range(0, N).Select(i =>
            {
                double a = 2 * Math.PI * i / N;
                return new Cone { X = (R + half) * Math.Cos(a), Y = (R + half) * Math.Sin(a), Ci = i };  // outer-edge ring
            }).ToList();
            Assert.AreEqual(cones.Count, TrackUtil.FilterConesOnTrack(cones, center, half).Count);
        }

        // test('parseSvgPath: M/L/H/V/Z → array of [x,y] pairs')
        [Test]
        public void ParseSvgPath_MLHVZ()
        {
            var pts = TrackUtil.ParseSvgPath("M10 20 L30 20 H50 V40 Z");
            Assert.AreEqual(4, pts.Count);
            AssertPt(10, 20, pts[0]);
            AssertPt(30, 20, pts[1]);
            AssertPt(50, 20, pts[2]); // H50 → x=50, y=20
            AssertPt(50, 40, pts[3]); // V40 → x=50, y=40
            // Fractional coordinates (as in real track SVGs)
            var pts2 = TrackUtil.ParseSvgPath("M11374.3 1531.75L6486.45 2177.88Z");
            Assert.AreEqual(2, pts2.Count);
            Assert.IsTrue(Math.Abs(pts2[0].X - 11374.3) < 0.01);
            Assert.IsTrue(Math.Abs(pts2[1].Y - 2177.88) < 0.01);
        }

        // test('parseSvgPath: deduplicates explicit closing vertex (L back-to-start Z)')
        [Test]
        public void ParseSvgPath_DedupsClosingVertex()
        {
            var pts = TrackUtil.ParseSvgPath("M10 20 L30 20 L50 40 L10 20 Z");
            Assert.AreEqual(3, pts.Count);
            AssertPt(10, 20, pts[0]);
            AssertPt(50, 40, pts[2]); // last kept vertex is the one before the dup
            // Non-closing path (last != first) must not be trimmed
            var pts2 = TrackUtil.ParseSvgPath("M10 20 L30 20 L50 40 Z");
            Assert.AreEqual(3, pts2.Count);
        }

        // test('chaikin: n points → 2n, convex combination of neighbours')
        [Test]
        public void Chaikin_DoublesPointsConvexCombination()
        {
            var sq = new[] { new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 10), new Vec2(0, 10) };
            var output = TrackUtil.Chaikin(sq);
            Assert.AreEqual(8, output.Count);
            Near(output[0].X, 0 * .75 + 10 * .25);
            Near(output[0].Y, 0);
        }

        // test('offsetEdges: gentle curve (R >> half) — edges symmetric, outer = half from centre')
        [Test]
        public void OffsetEdges_GentleCurveSymmetric()
        {
            var center = new[] { new Vec2(-100, -100), new Vec2(100, -100), new Vec2(100, 100), new Vec2(-100, 100) };
            double half = 40;
            var e = TrackUtil.OffsetEdges(center, half);
            Assert.AreEqual(4, e.Center.Count);
            Assert.AreEqual(4, e.Outer.Count);
            Assert.AreEqual(4, e.Inner.Count);
            for (int i = 0; i < 4; i++)
            {
                Near((e.Outer[i].X + e.Inner[i].X) / 2, center[i].X);
                Near((e.Outer[i].Y + e.Inner[i].Y) / 2, center[i].Y);
                Near(CoreMath.Hypot(e.Outer[i].X - center[i].X, e.Outer[i].Y - center[i].Y), half);
            }
        }

        // test('offsetEdges: hairpin (R < half) — inner offset clamped, no self-intersection')
        [Test]
        public void OffsetEdges_HairpinInnerClamped()
        {
            double R = 40, half = 80;
            var pts = new[]
            {
                new Vec2(R * Math.Cos(-Math.PI / 3), R * Math.Sin(-Math.PI / 3)),
                new Vec2(R, 0),
                new Vec2(R * Math.Cos(Math.PI / 3), R * Math.Sin(Math.PI / 3)),
            };
            var e = TrackUtil.OffsetEdges(pts, half);
            Near(CoreMath.Hypot(e.Outer[1].X - pts[1].X, e.Outer[1].Y - pts[1].Y), half);
            double d = CoreMath.Hypot(e.Inner[1].X - pts[1].X, e.Inner[1].Y - pts[1].Y);
            Assert.IsTrue(d < half, "inner offset at apex " + d + " should be < " + half + " (clamped)");
        }

        // test('placeCones: independent arc-length accumulators — uniform edges give equal count')
        [Test]
        public void PlaceCones_IndependentAccumulators()
        {
            var outer = Enumerable.Range(0, 10).Select(i => new Vec2(i, 0)).ToList();
            var inner = Enumerable.Range(0, 10).Select(i => new Vec2(i, 5)).ToList();
            var cones = TrackUtil.PlaceCones(outer, inner, 5); // minSpacing=5 GU, point spacing=1 GU → 4 cones
            Assert.AreEqual(4, cones.Count);
            Assert.AreEqual(false, cones[0].Knocked);
            Assert.AreEqual(0, cones[0].Vx);
        }

        // test('sampleCheckpoints: K points, first = center[0]')
        [Test]
        public void SampleCheckpoints_KPointsFirstIsCenter0()
        {
            var center = Enumerable.Range(0, 16).Select(i => new Vec2(i, 0)).ToList();
            var cps = TrackUtil.SampleCheckpoints(center, 8);
            Assert.AreEqual(8, cps.Count);
            Assert.AreEqual(center[0], cps[0]);
            Assert.AreEqual(center[2], cps[1]); // floor(1/8*16)=2
        }

        // test('prepProp: hl defaults to 0, caches cos/sin')
        [Test]
        public void PrepProp_DefaultsHlCachesCosSin()
        {
            var a = TrackUtil.PrepProp(new Prop { Ang = 0 });
            Assert.AreEqual(0, a.Hl);
            Near(a.Cos, 1);
            Near(a.Sin, 0);
            var b = TrackUtil.PrepProp(new Prop { Ang = Math.PI / 2, Hl = 5 });
            Assert.AreEqual(5, b.Hl);
            Near(b.Cos, 0);
            Near(b.Sin, 1);
        }

        // test('sampleCheckpointsByCorner: returns ≥K points, all on the centreline, anchored at finish')
        [Test]
        public void ByCorner_AtLeastKOnCentrelineAnchored()
        {
            var center = Enumerable.Range(0, 48).Select(i => new Vec2(
                Math.Cos((i / 48.0) * 2 * Math.PI) * 200,
                Math.Sin((i / 48.0) * 2 * Math.PI) * 200)).ToList();
            var cps = TrackUtil.SampleCheckpointsByCorner(center, 8);
            Assert.IsTrue(cps.Count >= 8, "at least K checkpoints");
            Assert.AreEqual(center[0], cps[0], "checkpoint[0] anchored at the finish");
            foreach (var cp in cps) Assert.IsTrue(IndexOfPt(center, cp) >= 0, "each cp is a centerline point");
        }

        // test('sampleCheckpointsByCorner: an oversized gap gets extra checkpoints inserted')
        [Test]
        public void ByCorner_OversizedGapGetsExtraCheckpoints()
        {
            var center = Enumerable.Range(0, 32).Select(i => new Vec2(i * 10, 0)).ToList();
            var cps = TrackUtil.SampleCheckpointsByCorner(center, 8);
            Assert.IsTrue(cps.Count > 8, "oversized gaps split → more than K checkpoints, got " + cps.Count);
            Assert.AreEqual(center[0], cps[0], "checkpoint[0] still anchored at the finish");
            foreach (var cp in cps) Assert.IsTrue(IndexOfPt(center, cp) >= 0, "each cp is a centerline point");
        }

        // test('sampleCheckpointsByCorner: picks the corner apex inside its arc-length sector')
        [Test]
        public void ByCorner_PicksCornerApexInSector()
        {
            var center = new[]
            {
                new Vec2(0, 0), new Vec2(10, 0), new Vec2(20, 0), new Vec2(30, 0),
                new Vec2(40, 0),  // 4 — apex of 90° bend
                new Vec2(40, 10), new Vec2(40, 20), new Vec2(40, 30),
            };
            var cps = TrackUtil.SampleCheckpointsByCorner(center, 3);
            Assert.AreEqual(3, cps.Count);
            Assert.AreEqual(center[0], cps[0], "checkpoint[0] is always center[0] (finish line)");
            Assert.AreEqual(center[4], cps[1], "sector 1 should land on the 90° apex");
        }

        // test('sampleCheckpointsByCorner: pure straight falls back to arc-length sector midpoint')
        [Test]
        public void ByCorner_PureStraightFallsBackToSectorMidpoint()
        {
            var center = Enumerable.Range(0, 8).Select(i => new Vec2(i * 10, 0)).ToList();
            var cps = TrackUtil.SampleCheckpointsByCorner(center, 2);
            Assert.AreEqual(center[0], cps[0]); // finish line always anchored at center[0]
            Assert.AreEqual(center[7], cps[1]);
        }

        // test('sampleCheckpointsByCorner: checkpoint[0] is always center[0] regardless of curvature')
        [Test]
        public void ByCorner_Checkpoint0AlwaysCenter0()
        {
            var center = Enumerable.Range(0, 16).Select(i => new Vec2(
                Math.Cos((i / 16.0) * 2 * Math.PI) * 100,
                Math.Sin((i / 16.0) * 2 * Math.PI) * 100)).ToList();
            var cps = TrackUtil.SampleCheckpointsByCorner(center, 4);
            Assert.AreEqual(center[0], cps[0]);
        }

        // test('sampleCheckpointsByCorner: minimum spacing pushes hairpin-double to sector midpoint')
        [Test]
        public void ByCorner_MinimumSpacingKeepsCheckpointsDistinct()
        {
            int N = 40; double R = 500;
            var center = Enumerable.Range(0, N).Select(i => new Vec2(
                Math.Cos(((double)i / N) * 2 * Math.PI) * R,
                Math.Sin(((double)i / N) * 2 * Math.PI) * R)).ToList();
            var cps = TrackUtil.SampleCheckpointsByCorner(center, 4);
            Assert.AreEqual(center[0], cps[0]);
            var idxs = cps.Select(cp => IndexOfPt(center, cp)).ToList();
            // JS asserts exactly 4 ([0,12,22,35] under V8's Math.hypot). All curvatures on this circle are equal
            // up to ulp noise, so the per-sector argmax depends on hypot's rounding: CoreMath.Hypot mirrors V8's
            // algorithm precisely so that this expectation holds here too (a naive sqrt(x*x+y*y) gave [0,7,16,22,35]).
            Assert.AreEqual(new[] { 0, 12, 22, 35 }, idxs.ToArray(), "checkpoint indices: " + string.Join(",", idxs));
            Assert.AreEqual(idxs.Count, new HashSet<int>(idxs).Count, "all checkpoints must be at different centerline points");
        }

        // test('sampleCheckpointsByCorner: arc-length sectors prevent clustering in Chaikin-dense region')
        [Test]
        public void ByCorner_ArcLengthSectorsPreventClustering()
        {
            var dense = Enumerable.Range(0, 40).Select(i => new Vec2(i * 0.01, 0)).ToList();
            var sparse = new[] { new Vec2(1, 0), new Vec2(3, 0), new Vec2(6, 0), new Vec2(9, 0) };
            var center = dense.Concat(sparse).ToList(); // N=44
            var cps = TrackUtil.SampleCheckpointsByCorner(center, 2);
            int inDense = cps.Count(cp => IndexOfPt(center, cp) < dense.Count);
            Assert.IsTrue(inDense < 2, "expected <2 CPs in the dense region, got " + inDense);
        }

        // test('nearestCenter: finds the nearest point on a straight centerline')
        [Test]
        public void NearestCenter_FindsNearestOnStraight()
        {
            var center = new[] { 0, 10, 20, 30, 40 }.Select(x => new Vec2(x, 0)).ToList();
            var (dist, idx) = TrackUtil.NearestCenter(12, 5, center, 0);
            Assert.AreEqual(1, idx);
            Near(dist, Math.Sqrt(4 + 25));
        }

        // test('nearestCenter: wraps correctly across the closed loop seam')
        [Test]
        public void NearestCenter_WrapsAcrossSeam()
        {
            var center = new[] { 0, 10, 20, 30, 40 }.Select(x => new Vec2(x, 0)).ToList();
            var (dist, idx) = TrackUtil.NearestCenter(2, 0, center, 4);
            Assert.AreEqual(0, idx);
            Near(dist, 2);
        }

        // test('circularAdvance: forward step returns the distance')
        [Test]
        public void CircularAdvance_ForwardStep()
        {
            Assert.AreEqual(5, TrackUtil.CircularAdvance(5, 0, 100));
        }

        // test('circularAdvance: same position returns 0')
        [Test]
        public void CircularAdvance_SamePositionIsZero()
        {
            Assert.AreEqual(0, TrackUtil.CircularAdvance(3, 3, 100));
        }

        // test('circularAdvance: backward movement returns 0')
        [Test]
        public void CircularAdvance_BackwardIsZero()
        {
            Assert.AreEqual(0, TrackUtil.CircularAdvance(3, 5, 100)); // went back 2 → d=98 > 50
        }

        // test('circularAdvance: crossing the lap seam counts as forward')
        [Test]
        public void CircularAdvance_CrossingSeamIsForward()
        {
            // ref=410, idx=3 on N=416: d=(3-410+416)%416=9, ≤208 → 9
            Assert.AreEqual(9, TrackUtil.CircularAdvance(3, 410, 416));
        }

        // test('nearestCenter: tracks moving car around a loop without index jumps')
        [Test]
        public void NearestCenter_TracksMovingCarAroundLoop()
        {
            int N = 40;
            var center = Enumerable.Range(0, N).Select(i => new Vec2(
                Math.Cos(((double)i / N) * 2 * Math.PI) * 500,
                Math.Sin(((double)i / N) * 2 * Math.PI) * 500)).ToList();
            int prevIdx = 0;
            for (int i = 0; i < N; i++)
            {
                var (dist, idx) = TrackUtil.NearestCenter(center[i].X, center[i].Y, center, prevIdx);
                Assert.AreEqual(i, idx);
                Near(dist, 0);
                prevIdx = idx;
            }
        }
    }
}
