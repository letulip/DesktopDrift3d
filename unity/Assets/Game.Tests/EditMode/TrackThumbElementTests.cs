#nullable enable
using System.Collections.Generic;
using System.Linq;
using DesktopDrift.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DesktopDrift.Game.Tests
{
    public class TrackThumbElementTests
    {
        static List<ThumbCommand> GreenStudy()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<TrackCatalog>("Assets/Content/Tracks/TrackCatalog.asset");
            Assert.IsNotNull(catalog);
            TrackData fwd = TrackRuntime.Load(catalog, "green-study", false);
            return TrackThumb.ForInstance(fwd, false);
        }

        [Test]
        public void Plan_StartsWithBackgroundRectThenRoundedTableThenClosedRoundStroke()
        {
            List<ThumbCommand> cmds = GreenStudy();
            List<string> plan = TrackThumbElement.Plan(cmds, 1.0).ToList();

            Assert.AreEqual("fillColor #14130e", plan[0]);
            Assert.AreEqual("fillRect 0 0 260 140", plan[1]);
            Assert.AreEqual("beginPath", plan[2]);
            Assert.AreEqual("roundRect 5 5 250 130 5", plan[3]);
            Assert.AreEqual("fillColor #2f4034", plan[4]);
            Assert.AreEqual("fill", plan[5]);
            Assert.AreEqual("strokeColor #7a6334", plan[6]);
            Assert.AreEqual("lineWidth 2", plan[7]);
            Assert.AreEqual("stroke", plan[8]);

            // track polyline: round cap + join, one beginPath, moveTo + (N-1) lineTo, closePath, stroke
            int n = cmds.Count(c => c.Name == "moveTo" || c.Name == "lineTo");
            Assert.Greater(n, 100);
            int join = plan.IndexOf("lineJoin Round");
            Assert.Greater(join, 8);
            Assert.AreEqual("lineCap Round", plan[join - 1]);
            Assert.AreEqual("beginPath", plan[join + 1]);
            Assert.That(plan[join + 2], Does.StartWith("moveTo "));
            Assert.AreEqual(n, plan.Skip(join + 1).Count(s => s.StartsWith("moveTo ") || s.StartsWith("lineTo ")));
            Assert.AreEqual("closePath", plan[plan.Count - 2]);
            Assert.AreEqual("stroke", plan[plan.Count - 1]);
        }

        [Test]
        public void Plan_ScalesUniformlyIncludingLineWidth()
        {
            List<ThumbCommand> cmds = GreenStudy();
            List<string> one = TrackThumbElement.Plan(cmds, 1.0).ToList();
            List<string> two = TrackThumbElement.Plan(cmds, 2.0).ToList();
            Assert.AreEqual(one.Count, two.Count);
            Assert.AreEqual("fillRect 0 0 520 280", two[1]);
            Assert.AreEqual("roundRect 10 10 500 260 10", two[3]);
            Assert.AreEqual("lineWidth 4", two[7]);
        }

        [Test]
        public void ReversedTrackPlanEqualsForwardPlan()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<TrackCatalog>("Assets/Content/Tracks/TrackCatalog.asset");
            TrackData fwd = TrackRuntime.Load(catalog, "green-study", false);
            CollectionAssert.AreEqual(
                TrackThumbElement.Plan(TrackThumb.ForInstance(fwd, false), 1.0).ToList(),
                TrackThumbElement.Plan(TrackThumb.ForInstance(fwd, true), 1.0).ToList());
        }

        // StrokeAsFills draws the round-join stroke as overlapping fills (Painter2D's own stroke left hairline seams).
        // Its union must be the round-join stroke: no gap inside, and at most a hair outside it.
        [Test]
        public void StrokeAsFills_UnionIsTheRoundJoinStroke()
        {
            // A "D": a half circle in 20-degree steps (gentle turns that need the miter reach), two points on the straight back
            // (no turn), two sharp corners (discs).
            var path = new List<Vector2>();
            for (int deg = -90; deg <= 90; deg += 20)
                path.Add(new Vector2(100 + 60 * Mathf.Cos(deg * Mathf.Deg2Rad), 100 + 60 * Mathf.Sin(deg * Mathf.Deg2Rad)));
            path.Add(new Vector2(100, 120));
            path.Add(new Vector2(100, 80));
            const float half = 9f;
            var quads = new List<Vector2[]>();
            var discs = new List<Vector2>();
            TrackThumbElement.StrokeAsFills(path, true, half, false, quads, discs);
            Assert.AreEqual(path.Count, quads.Count, "one quad per segment of the closed path");
            Assert.AreEqual(2, discs.Count, "discs only at the two sharp corners");

            int gaps = 0, outside = 0;
            for (float y = 20; y <= 180; y += 0.5f)
                for (float x = 30; x <= 180; x += 0.5f)
                {
                    var s = new Vector2(x, y);
                    float d = float.MaxValue;
                    for (int i = 0; i < path.Count; i++) d = Mathf.Min(d, SegmentDistance(s, path[i], path[(i + 1) % path.Count]));
                    bool covered = discs.Any(c => (s - c).magnitude <= half) || quads.Any(q => InConvexQuad(q, s));
                    if (d <= half - 0.05f && !covered) gaps++;
                    // beyond the round join: the miter tip (+0.14 at 20 degrees) and SeamOverlap past it (corner at +0.36)
                    if (d > half + 0.4f && covered) outside++;
                }
            Assert.AreEqual(0, gaps, "points inside the stroke left uncovered");
            Assert.AreEqual(0, outside, "points covered beyond the stroke");
        }

        [Test]
        public void StrokeAsFills_OpenPathEndsAreNotExtendedAndGetDiscsOnlyForRoundCaps()
        {
            var path = new List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(20, 0) };
            var quads = new List<Vector2[]>();
            var discs = new List<Vector2>();
            TrackThumbElement.StrokeAsFills(path, false, 2, true, quads, discs);
            CollectionAssert.AreEqual(new[] { new Vector2(0, 0), new Vector2(20, 0) }, discs);
            Assert.AreEqual(2, quads.Count);
            float o = (float)TrackThumbElement.SeamOverlap;
            CollectionAssert.AreEqual(new[] { 0f, 10 + o, 10 + o, 0f }, quads[0].Select(v => v.x));
            CollectionAssert.AreEqual(new[] { 10 - o, 20f, 20f, 10 - o }, quads[1].Select(v => v.x));

            quads.Clear(); discs.Clear();
            TrackThumbElement.StrokeAsFills(path, false, 2, false, quads, discs);
            Assert.AreEqual(0, discs.Count, "butt caps get no discs");
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        static bool InConvexQuad(Vector2[] q, Vector2 p)
        {
            bool pos = false, neg = false;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = q[i], b = q[(i + 1) % 4];
                float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                if (cross > 0) pos = true;
                if (cross < 0) neg = true;
            }
            return !(pos && neg);
        }
    }
}
