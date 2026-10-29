#nullable enable
using System.Collections.Generic;
using System.Linq;
using DesktopDrift.Core;
using NUnit.Framework;
using UnityEditor;

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
    }
}
