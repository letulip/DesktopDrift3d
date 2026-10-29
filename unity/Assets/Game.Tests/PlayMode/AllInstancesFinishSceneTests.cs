// SPEC-03 step 7: SVG -> TrackAsset -> TrackRuntime.Load -> RaceLoop + RaceRules + AutopilotSteerSource finishes a Time Attack race on
// every one of the 12 track instances (6 tracks x forward/reversed) on EXACTLY the frame and with exactly the score, wallet delta,
// knocked cones and collected caps of the REAL 2D engine (tools/engine-finish-ref.json, written by scripts/engine-finish.sh).
//
// Save: each instance reloads scene Game, whose RaceLoop.Awake installs SaveSchema.Load(null).State – a fresh save. The harness seed is
// { version 4, settings { units kmh, haptics off, sound off, volume 1 } } run through the 2D store's defaults; the rules read nothing
// from settings, so the two fresh saves are equivalent for a race (the wallet is compared as a delta).
#nullable enable
using System;
using System.Collections;
using System.IO;
using System.Text;
using DesktopDrift.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DesktopDrift.Game.Tests
{
    public class AllInstancesFinishSceneTests
    {
        const int FrameCap = 20000;   // the harness cap of the ta-* runs

        [Serializable]
        public class Instance
        {
            public string run = "";
            public int frames;
            public bool finished;
            public int lapNum;
            public int score;
            public int walletDelta;
            public int conesKnocked;
            public int capsCollected;
        }

        [Serializable]
        class Reference { public Instance[] instances = Array.Empty<Instance>(); }

        static Instance[] LoadReference()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tools", "engine-finish-ref.json"));
            return JsonUtility.FromJson<Reference>(File.ReadAllText(path)).instances;
        }

        public static string[] RunNames()
        {
            Instance[] all = LoadReference();
            var names = new string[all.Length];
            for (int i = 0; i < all.Length; i++) names[i] = all[i].run;
            return names;
        }

        [TearDown]
        public void TearDown()
        {
            RaceLaunch.Mode = RaceMode.TimeAttack;
            RaceLaunch.TrackId = null;
            RaceLaunch.Reversed = false;
            RaceLaunch.Laps = null;
            RaceLaunch.CarIndex = 0;
            RaceLaunch.AutoStart = true;
        }

        [UnityTest]
        public IEnumerator InstanceFinishesLikeTheEngine([ValueSource(nameof(RunNames))] string run)
        {
            Instance expected = Array.Find(LoadReference(), i => i.run == run)!;
            Assert.IsNotNull(expected, run);
            bool reversed = run.EndsWith("-rev", StringComparison.Ordinal);
            string trackId = run.Substring("ta-".Length, run.Length - "ta-".Length - (reversed ? "-rev".Length : 0));

            RaceLaunch.AutoStart = false;
            SceneManager.LoadScene("Game");   // a fresh scene = a fresh SaveSchema.Load(null) save
            yield return null;
            var loop = UnityEngine.Object.FindFirstObjectByType<RaceLoop>();
            Assert.IsNotNull(loop, "RaceLoop in scene Game");
            var source = new AutopilotSteerSource(loop);
            loop.SteerSource = source;
            loop.HourOfDay = () => 12;   // the harness clock starts at 12:00 UTC

            RaceLaunch.Mode = RaceMode.TimeAttack;
            RaceLaunch.TrackId = trackId;
            RaceLaunch.Reversed = reversed;
            double walletBefore = loop.Save.Wallet();
            loop.Relaunch();
            Assert.AreEqual(trackId, loop.Track.Id, run);
            bool finished = false;
            loop.OnEvent += e => { if (e.Kind == RaceEvent.EventKind.Finished) finished = true; };
            int steps = 0;
            while (!finished && steps < FrameCap) { loop.StepOnce(source.Resolve()); steps++; }

            int cones = 0;
            foreach (var c in loop.State.Cones) if (c.Knocked) cones++;
            int caps = 0;
            foreach (var c in loop.State.Caps) if (c.Collected) caps++;
            var got = new Instance
            {
                run = run, frames = steps, finished = finished, lapNum = loop.State.LapNum,
                score = (int)CoreMath.JsRound(loop.State.Score), walletDelta = (int)(loop.Save.Wallet() - walletBefore),
                conesKnocked = cones, capsCollected = caps,
            };

            var diff = new StringBuilder();
            void Cmp<T>(string field, T want, T have) { if (!Equals(want, have)) diff.Append(' ').Append(field).Append(": engine ").Append(want).Append(" unity ").Append(have).Append(';'); }
            Cmp("frames", expected.frames, got.frames);
            Cmp("finished", expected.finished, got.finished);
            Cmp("lapNum", expected.lapNum, got.lapNum);
            Cmp("score", expected.score, got.score);
            Cmp("walletDelta", expected.walletDelta, got.walletDelta);
            Cmp("conesKnocked", expected.conesKnocked, got.conesKnocked);
            Cmp("capsCollected", expected.capsCollected, got.capsCollected);
            Debug.Log("finish " + run + " " + JsonUtility.ToJson(got));
            Assert.AreEqual(0, diff.Length, run + " differs from the 2D engine:" + diff);
        }
    }
}
