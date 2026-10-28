// SPEC-02 step 3: RaceLoop drives RaceRules exactly like the Core replay – bit for bit against a RaceRules
// stepped directly, and within 1e-6 against the recorded 2D engine trace (tools/engine-trace-oval.json).
#nullable enable
using System;
using System.Collections.Generic;
using DesktopDrift.Core;
using NUnit.Framework;
using UnityEngine;

namespace DesktopDrift.Game.Tests
{
    public class RaceLoopParityTests
    {
        private static Dictionary<string, object?> O(object? v) => (Dictionary<string, object?>)v!;
        private static List<object?> A(object? v) => (List<object?>)v!;
        private static double N(object? v) => (double)v!;

        private static Func<double> Lcg()
        {
            uint s = 1;
            return () => { s = unchecked(1664525u * s + 1013904223u); return s / 4294967296.0; };
        }

        [Test]
        public void LoopMatchesCoreReplayAndEngineTrace()
        {
            RaceLaunch.AutoStart = false;
            RaceLaunch.CarIndex = 0;
            RaceLaunch.Laps = 2;
            var go = new GameObject("loop");
            try
            {
                var loop = go.AddComponent<RaceLoop>();
                string cars = TraceFiles.Content("cars.json"), registry = TraceFiles.Content("car-registry.json");
                string config = TraceFiles.Content("config.json"), tracks = TraceFiles.Content("tracks.json"), shop = TraceFiles.Content("shop.json");
                var root = TraceFiles.Trace();
                var clock = O(root["clock"]);
                double dateBase = N(clock["dateBase"]), frameMs = N(clock["frameMs"]);
                double simNow = 0;
                Func<int> hour = () => (int)(((long)Math.Floor((dateBase + simNow) / 3600000.0)) % 24);

                var catalog = CarCatalog.FromJson(cars, registry);
                var physics = CarCatalog.PhysicsFromConfig(config);
                loop.Init(OvalTrack.Build(), catalog, physics, ContentJson.Build(tracks, shop, cars),
                    SaveSchema.Load(Json.Serialize(root["saveSeed"])).State);
                loop.Random = Lcg();
                loop.HourOfDay = hour;
                loop.NowMs = () => dateBase + simNow;
                var events = new List<(int Step, RaceEvent Ev)>();
                loop.OnEvent += e => events.Add((loop.StepCount, e));
                loop.StartRace(new RaceOptions
                {
                    Laps = 2, Stock = true, InitItems = false, Zen = false, Reversed = false, CarCount = 8,
                    Content = ContentJson.Build(tracks, shop, cars),
                });

                SaveState save = SaveSchema.Load(Json.Serialize(root["saveSeed"])).State;
                save.NowMs = () => dateBase + simNow;
                CarDefinition car = catalog.Cars[0];
                var rules = new RaceRules(OvalTrack.Build(), car.Drive, car.Len, car.Wid, physics, new RaceOptions
                {
                    Laps = 2, Stock = true, InitItems = false, Zen = false, Reversed = false, CarCount = 8,
                    Content = ContentJson.Build(tracks, shop, cars),
                }, save, Lcg(), hour);
                rules.Start();

                var frames = A(root["frames"]);
                Assert.AreEqual(2530, frames.Count, "trace frames");
                string[] names = { "x", "y", "angle", "vx", "vy", "score" };
                for (int i = 0; i < frames.Count; i++)
                {
                    var f = A(frames[i]);
                    simNow += frameMs;
                    int steer = (int)N(f[0]);
                    loop.StepOnce(steer);
                    rules.Step(steer, RaceLoop.FixedDt);
                    CarState a = loop.State.Car, b = rules.State.Car;
                    double[] got = { a.X, a.Y, a.Angle, a.Vx, a.Vy, loop.State.Score };
                    double[] direct = { b.X, b.Y, b.Angle, b.Vx, b.Vy, rules.State.Score };
                    for (int k = 0; k < names.Length; k++)
                    {
                        if (BitConverter.DoubleToInt64Bits(got[k]) != BitConverter.DoubleToInt64Bits(direct[k]))
                            Assert.Fail($"frame {i + 1} {names[k]}: loop={got[k]:R} rules={direct[k]:R}");
                        double js = N(f[k + 1]);
                        if (Math.Abs(got[k] - js) > 1e-6)
                            Assert.Fail($"frame {i + 1} {names[k]}: loop={got[k]:R} trace={js:R}");
                    }
                }

                Assert.AreEqual(2530, loop.StepCount, "StepCount");
                Assert.IsFalse(loop.Running, "Running after Finished");
                var finished = events.FindAll(e => e.Ev.Kind == RaceEvent.EventKind.Finished);
                Assert.AreEqual(1, finished.Count, "Finished events");
                Assert.AreEqual(2530, finished[0].Step, "Finished on the last frame");

                double x = loop.State.Car.X;
                loop.StepOnce(1);
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(x), BitConverter.DoubleToInt64Bits(loop.State.Car.X), "no motion after Finished");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                RaceLaunch.AutoStart = true;
                RaceLaunch.CarIndex = 0;
                RaceLaunch.Laps = null;
            }
        }
    }
}
