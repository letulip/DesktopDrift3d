// SPEC-01 step 8: targeted RaceRules checks on the sandbox oval with scripted steering / placed cars.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    [TestFixture]
    public class RaceRulesTests
    {
        private const double Dt = 1.0 / 60;

        private static RaceRules Make(RaceOptions? opts = null, TrackData? track = null, SaveState? save = null)
        {
            var car = RaceReplayTests.Car();
            uint s = 1;
            Func<double> random = () => { s = unchecked(1664525u * s + 1013904223u); return s / 4294967296.0; };
            var rules = new RaceRules(track ?? OvalTrack.Build(), car.P, car.Len, car.Wid, car.K,
                opts ?? new RaceOptions { Laps = 2 }, save ?? SaveSchema.Load(null).State, random, () => 12);
            rules.Start();
            return rules;
        }

        // Skip the 3-2-1 countdown without the GO side effects.
        private static RaceRules Racing(RaceOptions? opts = null, TrackData? track = null, SaveState? save = null)
        {
            RaceRules r = Make(opts, track, save);
            r.State.StartCd = 0;
            return r;
        }

        private static void Place(RaceRules r, double x, double y, double angle, double vx = 0, double vy = 0)
        {
            CarState c = r.State.Car;
            c.X = x; c.Y = y; c.Angle = angle; c.Vx = vx; c.Vy = vy;
        }

        private static List<RaceEvent> Of(List<RaceEvent> ev, RaceEvent.EventKind k) => ev.Where(e => e.Kind == k).ToList();

        [Test]
        public void Countdown_PipsOncePerNumber_ThenGoAndGameplayStart()
        {
            RaceRules r = Make();
            var pips = new List<(int Frame, int N)>();
            int goFrame = -1;
            for (int f = 1; f <= 200 && goFrame < 0; f++)
            {
                List<RaceEvent> ev = r.Step(0, Dt);
                foreach (RaceEvent e in Of(ev, RaceEvent.EventKind.Countdown)) pips.Add((f, e.Number));
                if (Of(ev, RaceEvent.EventKind.Go).Count == 1)
                {
                    goFrame = f;
                    var kinds = ev.Select(e => e.Kind).ToList();
                    Assert.AreEqual(new[] { RaceEvent.EventKind.Go, RaceEvent.EventKind.GameplayStart }, kinds, "GO step events");
                    Assert.AreEqual(1.0, r.State.GoT, "goT");
                    Assert.IsTrue(r.State.GameplayActive, "gameplayActive");
                }
                else Assert.AreEqual(0, r.State.Car.Vx, "car frozen during the countdown");
            }
            Assert.AreEqual(new[] { 3, 2, 1 }, pips.Select(p => p.N).ToArray(), "pips");
            Assert.AreEqual(1, pips[0].Frame, "pip 3 on the first frame");
            Assert.That(pips[1].Frame, Is.InRange(60, 61), "pip 2");
            Assert.That(pips[2].Frame, Is.InRange(120, 121), "pip 1");
            Assert.That(goFrame, Is.InRange(180, 181), "GO frame");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConeHit_Penalty100_UnlessZen(bool zen)
        {
            RaceRules r = Racing(new RaceOptions { Laps = 2, Zen = zen });
            r.State.Score = 500;
            Cone cone = r.State.Cones[5];
            Place(r, cone.X, cone.Y, 0, 200, 0);
            List<RaceEvent> ev = r.Step(0, Dt);
            Assert.AreEqual(1, Of(ev, RaceEvent.EventKind.ConeHit).Count, "ConeHit");
            Assert.AreEqual(5, Of(ev, RaceEvent.EventKind.ConeHit)[0].Index, "cone index");
            Assert.IsTrue(cone.Knocked, "knocked");
            Assert.AreEqual(zen ? 500 : 400, r.State.Score, "score");
            RaceEvent flash = Of(ev, RaceEvent.EventKind.Flash).First();
            Assert.AreEqual("Cone!  -100", flash.Text);
            Assert.AreEqual("#ffb14d", flash.Color);
            Assert.AreEqual("cone", Of(ev, RaceEvent.EventKind.Haptic)[0].Id, "hapticCone");
        }

        [Test]
        public void Combo_BanksOnCleanDriftEnd()
        {
            RaceRules r = Racing();
            Vec2 start = r.Track.StartPos;
            Place(r, start.X, start.Y, r.Track.StartAngle);   // on track, at rest ⇒ not drifting
            r.State.ComboPoints = 50.4;
            r.State.MultBuild = 2;
            r.State.DriftGrace = 0.49;
            List<RaceEvent> ev = r.Step(0, Dt);
            Assert.AreEqual(50, r.State.Score, "banked into the score");
            Assert.AreEqual(0, r.State.ComboPoints, "points reset");
            Assert.AreEqual(1, r.State.Mult, "multiplier reset after a real break");
            Assert.AreEqual("+50 banked", Of(ev, RaceEvent.EventKind.Flash).Single().Text);
            Assert.IsFalse(r.State.ComboUnbroken, "the unbroken streak ended");
            Assert.AreEqual(0, Of(ev, RaceEvent.EventKind.ComboBurn).Count, "no burn");
        }

        [Test]
        public void Combo_BurnsOnWallCrashAboveThreshold()
        {
            RaceRules r = Racing();
            double cr = RaceReplayTests.Car().Wid * 0.55;
            Place(r, OvalTrack.TableW / 2 - cr - 1, 0, 0, 400, 0);
            r.State.ComboPoints = 77;
            r.State.Score = 300;
            List<RaceEvent> ev = r.Step(0, Dt);
            RaceEvent burn = Of(ev, RaceEvent.EventKind.ComboBurn).First();
            Assert.AreEqual("WALL!", burn.Text, "burn reason");
            Assert.AreEqual("WALL!  combo 77 lost", Of(ev, RaceEvent.EventKind.Flash).First().Text);
            Assert.AreEqual("crash", Of(ev, RaceEvent.EventKind.Sfx).First().Id, "sfx.crash");
            Assert.AreEqual(300, r.State.Score, "a burnt combo is not banked");
            Assert.AreEqual(0, r.State.ComboPoints);
            Assert.AreEqual(1, r.State.RunCrashes, "runCrashes");
            Assert.That(r.State.CrashCd, Is.EqualTo(0.5 - Dt).Within(1e-12), "crashCd armed then ticked");
        }

        [Test]
        public void Checkpoints_AntiSkip_ThenLapWrap()
        {
            RaceRules r = Racing();
            TrackData t = r.Track;
            Place(r, t.Checkpoints[3].X, t.Checkpoints[3].Y, 0);
            Assert.AreEqual(0, Of(r.Step(0, Dt), RaceEvent.EventKind.Checkpoint).Count, "skipping to cp 3 does not count");
            Assert.AreEqual(1, r.State.NextCp);
            for (int i = 1; i < t.Checkpoints.Count; i++)
            {
                Place(r, t.Checkpoints[i].X, t.Checkpoints[i].Y, 0);
                List<RaceEvent> ev = r.Step(0, Dt);
                Assert.AreEqual(i, Of(ev, RaceEvent.EventKind.Checkpoint).Single().Index, "passed cp " + i);
                Assert.AreEqual((i + 1) % t.Checkpoints.Count, r.State.NextCp, "nextCp after " + i);
            }
            Assert.IsNull(r.State.PrevFinishDot, "reset before the finish approach");
            double c = Math.Cos(t.StartAngle), s = Math.Sin(t.StartAngle);
            Place(r, t.StartPos.X - c * 40, t.StartPos.Y - s * 40, t.StartAngle);
            Assert.AreEqual(0, Of(r.Step(0, Dt), RaceEvent.EventKind.LapDone).Count, "behind the line");
            Assert.That(r.State.PrevFinishDot, Is.LessThan(0));
            Place(r, t.StartPos.X + c * 40, t.StartPos.Y + s * 40, t.StartAngle);
            List<RaceEvent> lap = r.Step(0, Dt);
            Assert.AreEqual(1, Of(lap, RaceEvent.EventKind.LapDone).Single().Number, "lap 1 done");
            Assert.AreEqual(1, r.State.LapNum);
            Assert.AreEqual(1, r.State.LapScores.Count);
            Assert.AreEqual(1, r.State.NextCp, "wrapped to cp 1");
            Assert.AreEqual(0, r.State.LapTime);
            StringAssert.StartsWith("LAP ", Of(lap, RaceEvent.EventKind.Flash).Last().Text);
            Assert.AreEqual("#9dff8f", Of(lap, RaceEvent.EventKind.Flash).Last().Color);
        }

        [Test]
        public void Finish_PaysFinishPayoutPlusFirstClear_OnFreshSave()
        {
            TrackData track = OvalTrack.Build();
            track.Id = "oval-test";
            SaveState save = SaveSchema.Load(null).State;
            RaceRules r = Racing(new RaceOptions { Laps = 1 }, track, save);
            r.State.NextCp = 0;
            r.State.Score = 1234;
            r.State.LapTime = 10;
            double c = Math.Cos(track.StartAngle), s = Math.Sin(track.StartAngle);
            Place(r, track.StartPos.X - c * 40, track.StartPos.Y - s * 40, track.StartAngle);
            r.Step(0, Dt);
            Place(r, track.StartPos.X + c * 40, track.StartPos.Y + s * 40, track.StartAngle);
            List<RaceEvent> ev = r.Step(0, Dt);
            RaceResult res = Of(ev, RaceEvent.EventKind.Finished).Single().Result!;
            Assert.AreEqual(1234, res.Score);
            Assert.That(res.Pps, Is.EqualTo(1234 / res.TotalTime).Within(1e-12), "pps");
            Assert.AreEqual(Economy.FinishPayout(res.Pps), res.Tires.Finish, "finish payout");
            Assert.AreEqual(Economy.FirstClearBonus, res.Tires.FirstClear, "first clear");
            Assert.AreEqual("Race", res.TrackName);
            Assert.IsTrue(res.IsNewRecord, "first run is a record");
            var reasons = save.Ledger().Select(e => (string)((Dictionary<string, object?>)e!)["reason"]!).ToList();
            Assert.AreEqual("Race — first clear", reasons[0]);
            Assert.AreEqual("Race — finish bonus", reasons[1]);
            Assert.IsTrue(r.State.RaceFinished);
            // A second finish on the same save: no first-clear bonus any more.
            RaceRules again = Racing(new RaceOptions { Laps = 1 }, track, save);
            again.State.NextCp = 0; again.State.Score = 10; again.State.LapTime = 10;
            Place(again, track.StartPos.X - c * 40, track.StartPos.Y - s * 40, track.StartAngle);
            again.Step(0, Dt);
            Place(again, track.StartPos.X + c * 40, track.StartPos.Y + s * 40, track.StartAngle);
            Assert.AreEqual(0, Of(again.Step(0, Dt), RaceEvent.EventKind.Finished).Single().Result!.Tires.FirstClear);
        }

        [Test]
        public void Stop_EmitsPairedGameplayStop_OnlyAfterGo()
        {
            RaceRules early = Make();
            early.Step(0, Dt);
            Assert.AreEqual(0, Of(early.Stop(), RaceEvent.EventKind.GameplayStop).Count, "no stop before GO");

            RaceRules r = Make();
            for (int f = 0; f < 200 && !r.State.GameplayActive; f++) r.Step(0, Dt);
            Assert.IsTrue(r.State.GameplayActive);
            Assert.AreEqual(1, Of(r.Stop(), RaceEvent.EventKind.GameplayStop).Count, "paired stop");
            Assert.AreEqual(0, Of(r.Stop(), RaceEvent.EventKind.GameplayStop).Count, "exactly once");
        }
    }
}
