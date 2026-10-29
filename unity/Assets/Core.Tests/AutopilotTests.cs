// SPEC-03 step 4b gate: Autopilot.Steer reproduces the steer column of the traces recorded by tools/engine-harness.mjs
// (whose autopilot it mirrors) for EVERY frame: steer[i] comes from the car of frame i-1 (frame -1 = the start pose).
// green-study-zen-cap is excluded: its steering is forced by the harness.
#nullable enable
using System.Collections.Generic;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    [TestFixture]
    public class AutopilotTests
    {
        [TestCase("oval")]
        [TestCase("green-study")]
        [TestCase("green-study-rev")]
        [TestCase("oval-sandbox")]
        public void SteerMatchesRecordedTrace(string name)
        {
            var root = RaceReplayTests.Doc("tools/engine-trace-" + name + ".json");
            var src = (Dictionary<string, object?>)root["source"]!;
            var opts = (Dictionary<string, object?>)root["opts"]!;
            TrackData track = RaceReplayTests.BuildTrack((string)src["module"]!, (bool)opts["reversed"]!);
            var frames = (List<object?>)root["frames"]!;
            var fields = (List<object?>)root["frameFields"]!;
            Assert.AreEqual("steer", fields[0], "frameFields[0]");
            Assert.AreEqual("x", fields[1]); Assert.AreEqual("y", fields[2]); Assert.AreEqual("angle", fields[3]);

            var pilot = new Autopilot((int)(double)root["lookahead"]!, (double)root["deadBand"]!);
            double x = track.StartPos.X, y = track.StartPos.Y, a = track.StartAngle;
            int mismatches = 0;
            string first = "";
            for (int i = 0; i < frames.Count; i++)
            {
                var f = (List<object?>)frames[i]!;
                int expected = (int)(double)f[0]!;
                int actual = pilot.Steer(x, y, a, track.Center);
                if (actual != expected)
                {
                    if (mismatches == 0)
                        first = name + " frame " + i + ": expected " + expected + ", actual " + actual + ", err " + pilot.LastErr + " (car " + x + "," + y + "," + a + ")";
                    mismatches++;
                }
                x = (double)f[1]!; y = (double)f[2]!; a = (double)f[3]!;
            }
            Assert.AreEqual(0, mismatches, mismatches + " of " + frames.Count + " frames differ; first: " + first);
        }
    }
}
