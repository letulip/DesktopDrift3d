// SPEC-01 step 8 gate: replays tools/engine-trace-{oval,green-study}.json – recorded from the REAL 2D race
// engine (DesktopDrift/js/game-engine.js startGame + frame()) by tools/engine-harness.mjs – through
// RaceRules frame by frame. Doubles within 1e-6, integers / bools / strings exactly, flash events at the
// same frames, and the final save (wallet, ledger, records, achievements) equal.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    [TestFixture]
    public class RaceReplayTests
    {
        private const double Tol = 1e-6;

        private static Dictionary<string, object?> O(object? v) => (Dictionary<string, object?>)v!;
        private static List<object?> A(object? v) => (List<object?>)v!;
        private static double N(object? v) => (double)v!;
        private static string Str(object? v) => v is double d ? d.ToString("R", CultureInfo.InvariantCulture) : v is bool b ? (b ? "true" : "false") : v == null ? "null" : v.ToString()!;

        private static Dictionary<string, object?> Doc(string relative)
        {
            return O(Json.Parse(File.ReadAllText(TestPaths.Locate(relative))));
        }

        internal static ContentDescriptor Content()
        {
            var tracks = A(Doc("unity/Assets/Content/_json/tracks.json")["TRACKS"]).Select(O)
                .Select(t => new Achievements.ContentTrack { Id = (string)t["id"]!, Name = (string)t["name"]! }).ToList();
            var shop = A(Doc("unity/Assets/Content/_json/shop.json")["CATALOG"]).Select(O)
                .Select(i => new Achievements.ContentShopItem { Id = (string)i["id"]!, Kind = (string)i["kind"]! }).ToList();
            var cars = A(Doc("unity/Assets/Content/_json/cars.json")["CARS"]).Select(O)
                .Select(c => new Achievements.ContentCar { Id = (string)c["id"]!, Price = c.TryGetValue("price", out object? p) && p is double pd ? (double?)pd : null })
                .ToList();
            return Achievements.BuildContent(tracks, shop, cars);
        }

        // cars.json CARS[0] (Bismark): drive profile, len, wid; config.json physics constants.
        internal static (DriveProfile P, double Len, double Wid, int CarCount, PhysicsConstants K) Car()
        {
            var cars = A(Doc("unity/Assets/Content/_json/cars.json")["CARS"]);
            var c0 = O(cars[0]);
            Assert.AreEqual("bismark", c0["id"], "CARS[0]");
            var d = O(c0["drive"]);
            var p = new DriveProfile
            {
                Thrust = N(d["thrust"]), MaxSpeed = N(d["maxSpeed"]), Steer = N(d["steer"]), SteerSmooth = N(d["steerSmooth"]),
                LowSpeedTurn = N(d["lowSpeedTurn"]), SelfAlign = N(d["selfAlign"]), Grip = N(d["grip"]),
                RollFriction = N(d["rollFriction"]), DriftDrag = N(d["driftDrag"]), DriftSteerBoost = N(d["driftSteerBoost"]),
            };
            var cfg = Doc("unity/Assets/Content/_json/config.json");
            var k = new PhysicsConstants { PhysHz = N(cfg["PHYS_HZ"]), GripWobble = N(cfg["GRIP_WOBBLE"]), SteerWobble = N(cfg["STEER_WOBBLE"]) };
            return (p, N(c0["len"]), N(c0["wid"]), cars.Count, k);
        }

        private sealed class Outcome
        {
            public string Summary = "";
        }

        private static Outcome Replay(string name)
        {
            var root = Doc("tools/engine-trace-" + name + ".json");
            var src = O(root["source"]);
            Assert.AreEqual(name, src["track"], "source.track");
            Assert.AreEqual("LCG 1664525/1013904223 mod 2^32", src["rng"], "source.rng");

            TrackData track = name == "oval" ? OvalTrack.Build() : TrackBuilderParityTests.Build(name);
            var car = Car();
            var clock = O(root["clock"]);
            double frameMs = N(clock["frameMs"]), dateBase = N(clock["dateBase"]);
            Assert.AreEqual("UTC", clock["tz"], "clock.tz");
            double simNow = 0;

            SaveState save = SaveSchema.Load(Json.Serialize(root["saveSeed"])).State;
            save.NowMs = () => dateBase + simNow;   // Date.now() = DATE_BASE + simulated ms

            // Numerical Recipes LCG, 32-bit (engine-harness.mjs: s = (imul(1664525, s) + 1013904223) >>> 0; s / 2^32)
            uint s = (uint)N(src["seed"]);
            Func<double> random = () => { s = unchecked(1664525u * s + 1013904223u); return s / 4294967296.0; };
            // new Date().getHours() with TZ=UTC
            Func<int> hour = () => (int)(((long)Math.Floor((dateBase + simNow) / 3600000.0)) % 24);

            var o = O(root["opts"]);
            var opts = new RaceOptions
            {
                Laps = o["laps"] is double l ? (int?)(int)l : null,
                Zen = (bool)o["zen"]!, Reversed = (bool)o["reversed"]!, InitItems = (bool)o["initItems"]!, Stock = (bool)o["stock"]!,
                CarCount = car.CarCount, Content = Content(),
            };
            double dt = N(root["dt"]);
            int walletStart = save.Wallet();

            var rules = new RaceRules(track, car.P, car.Len, car.Wid, car.K, opts, save, random, hour);
            rules.Start();
            RaceState S = rules.State;

            var fields = A(root["frameFields"]).Select(f => (string)f!).ToList();
            var getters = new Dictionary<string, Func<object?>>
            {
                ["x"] = () => S.Car.X, ["y"] = () => S.Car.Y, ["angle"] = () => S.Car.Angle, ["vx"] = () => S.Car.Vx, ["vy"] = () => S.Car.Vy,
                ["score"] = () => S.Score, ["comboPoints"] = () => S.ComboPoints, ["mult"] = () => S.Mult, ["multBuild"] = () => S.MultBuild,
                ["transitions"] = () => S.Transitions, ["nearMisses"] = () => S.NearMisses, ["nearMissCd"] = () => S.NearMissCd,
                ["crashCd"] = () => S.CrashCd, ["driftTime"] = () => S.DriftTime, ["driftGrace"] = () => S.DriftGrace,
                ["lastSlipSign"] = () => S.LastSlipSign, ["lapNum"] = () => S.LapNum, ["lapTime"] = () => S.LapTime, ["nextCp"] = () => S.NextCp,
                ["startCd"] = () => S.StartCd, ["goT"] = () => S.GoT, ["bestLap"] = () => S.BestLap, ["lastLap"] = () => S.LastLap,
                ["physT"] = () => S.PhysT, ["steerSmooth"] = () => S.SteerSmooth,
                ["conesKnocked"] = () => S.Cones.Count(c => c.Knocked), ["wallet"] = () => save.Wallet(),
            };

            string? fail = null;
            void Check(int frame, string field, object? js, object? cs)
            {
                if (fail != null) return;
                bool ok;
                if (js == null) ok = cs == null;
                else if (js is bool jb) ok = cs is bool cb && cb == jb;
                else if (cs is int ci) ok = (double)ci == (double)js;
                else if (cs is double cd) ok = Math.Abs(cd - (double)js) <= Tol;
                else if (js is string js2) ok = cs is string cs2 && cs2 == js2;
                else ok = false;
                if (!ok) fail = name + " frame " + frame + " (frames[" + (frame - 1) + "]) field '" + field + "': JS=" + Str(js) + " C#=" + Str(cs);
            }

            var frames = A(root["frames"]);
            var flashes = new List<(int, string)>();
            string prevFlash = S.FlashMsg;
            double prevFlashT = S.FlashT;
            int finishedAt = -1;
            for (int i = 0; i < frames.Count && fail == null; i++)
            {
                var f = A(frames[i]);
                int frame = i + 1;
                simNow += frameMs;
                List<RaceEvent> ev = rules.Step((int)N(f[0]), dt);
                if (ev.Any(e => e.Kind == RaceEvent.EventKind.Finished))
                {
                    Check(frame, "finished-at", (double)frames.Count, frame);
                    finishedAt = frame;
                }
                for (int j = 1; j < fields.Count; j++)
                {
                    string field = fields[j];
                    if (field == "caps[sweep,collected]")
                    {
                        var caps = A(f[j]);
                        Check(frame, "caps.length", (double)caps.Count, S.Caps.Count);
                        for (int c = 0; c < caps.Count && c < S.Caps.Count; c++)
                        {
                            var cap = A(caps[c]);
                            Check(frame, "caps[" + c + "].sweep", cap[0], S.Caps[c].Sweep);
                            Check(frame, "caps[" + c + "].collected", cap[1], S.Caps[c].Collected);
                        }
                        continue;
                    }
                    Assert.IsTrue(getters.ContainsKey(field), "unmapped frame field " + field);
                    Check(frame, field, f[j], getters[field]());
                }
                // engine-harness.mjs: an event is recorded when flashMsg changes or flashT was re-armed.
                if (S.FlashMsg != prevFlash || S.FlashT > prevFlashT)
                {
                    flashes.Add((frame, S.FlashMsg));
                    RaceEvent? last = ev.LastOrDefault(e => e.Kind == RaceEvent.EventKind.Flash);
                    Check(frame, "Flash event", S.FlashMsg, last?.Text);
                }
                prevFlash = S.FlashMsg; prevFlashT = S.FlashT;
            }
            Assert.IsNull(fail, fail);

            var evs = A(root["events"]);
            for (int i = 0; i < Math.Max(evs.Count, flashes.Count); i++)
            {
                string js = i < evs.Count ? N(A(evs[i])[0]) + " '" + (string)A(evs[i])[1]! + "'" : "<none>";
                string cs = i < flashes.Count ? flashes[i].Item1 + " '" + flashes[i].Item2 + "'" : "<none>";
                Assert.AreEqual(js, cs, name + " flash event #" + i + ": JS=" + js + " C#=" + cs);
            }

            var fin = O(root["final"]);
            void F(string field, object? js, object? cs) { Check((int)N(fin["frames"]), "final." + field, js, cs); }
            F("frames", fin["frames"], frames.Count);
            F("finished", fin["finished"], finishedAt == frames.Count);
            F("lapNum", fin["lapNum"], S.LapNum);
            F("score", fin["score"], (int)CoreMath.JsRound(S.Score));
            var ls = A(fin["lapScores"]);
            F("lapScores.length", (double)ls.Count, S.LapScores.Count);
            for (int i = 0; i < ls.Count && i < S.LapScores.Count; i++)
            {
                F("lapScores[" + i + "].n", O(ls[i])["n"], S.LapScores[i].N);
                F("lapScores[" + i + "].pts", O(ls[i])["pts"], S.LapScores[i].Pts);
                F("lapScores[" + i + "].t", O(ls[i])["t"], S.LapScores[i].T);
            }
            F("bestLap", fin["bestLap"], S.BestLap);
            if (fin["record"] == null)
            {
                F("record", null, track.Id.Length > 0 && save.Records().ContainsKey(track.Id) ? "present" : null);
            }
            else
            {
                var ta = O(O(fin["record"])["timeattack"]);
                var cta = O(O(save.Records()[track.Id])["timeattack"]);
                foreach (string k in new[] { "bestPPS", "bestPPSTotal", "bestPPSTime" }) F("record.timeattack." + k, ta[k], cta[k]);
            }
            F("walletStart", fin["walletStart"], walletStart);
            F("walletEnd", fin["walletEnd"], save.Wallet());
            F("walletDelta", fin["walletDelta"], save.Wallet() - walletStart);
            var led = A(fin["ledger"]);
            var cled = save.Ledger();
            F("ledger.length", (double)led.Count, cled.Count);
            for (int i = 0; i < led.Count && i < cled.Count; i++)
                foreach (string k in new[] { "t", "amount", "reason", "balance" })
                    F("ledger[" + i + "]." + k, O(led[i])[k], O(cled[i])[k]);
            string jsAch = string.Join(",", A(fin["achievements"]).Select(x => (string)x!));
            string csAch = string.Join(",", save.AchUnlocked().OrderBy(x => x, StringComparer.Ordinal));
            F("achievements", jsAch, csAch);
            F("conesKnocked", fin["conesKnocked"], S.Cones.Count(c => c.Knocked));
            F("capsCollected", fin["capsCollected"], S.Caps.Count(c => c.Collected));
            Assert.IsNull(fail, fail);

            TestContext.WriteLine(name + ": all " + frames.Count + " frames match, " + flashes.Count + " flash events, ledger " + cled.Count);
            return new Outcome
            {
                Summary = string.Join("|", S.Car.X.ToString("R", CultureInfo.InvariantCulture), S.Car.Y.ToString("R", CultureInfo.InvariantCulture),
                    S.Score.ToString("R", CultureInfo.InvariantCulture), S.LapNum, save.Wallet(), Json.Serialize(save.Root), flashes.Count),
            };
        }

        [Test]
        public void Oval_EngineTrace_ReplaysFrameByFrame()
        {
            Replay("oval");
        }

        [Test]
        public void GreenStudy_EngineTrace_ReplaysFrameByFrame()
        {
            Replay("green-study");
        }

        [Test]
        public void Oval_Replay_IsDeterministic()
        {
            string a = Replay("oval").Summary;
            string b = Replay("oval").Summary;
            Assert.AreEqual(a, b, "two oval replays diverged");
        }
    }
}
