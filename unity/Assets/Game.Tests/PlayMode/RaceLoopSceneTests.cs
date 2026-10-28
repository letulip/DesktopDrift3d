// SPEC-02 step 3: the live Game scene – RaceLoop in FixedUpdate drives the recorded oval steering through one
// lap (laps = 1) to Finished, at a fixed step of 1/60.
#nullable enable
using System.Collections;
using System.Collections.Generic;
using DesktopDrift.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DesktopDrift.Game.Tests
{
    public class RaceLoopSceneTests
    {
        private sealed class ScriptedSteer : ISteerSource
        {
            private readonly List<object?> _frames;
            private readonly RaceLoop _loop;
            public ScriptedSteer(List<object?> frames, RaceLoop loop) { _frames = frames; _loop = loop; }
            public int Resolve() => _loop.StepCount < _frames.Count ? (int)(double)((List<object?>)_frames[_loop.StepCount]!)[0]! : 0;
        }

        [UnityTest]
        public IEnumerator ScriptedLapFinishesOneLapRace()
        {
            RaceLaunch.AutoStart = false;
            RaceLaunch.Laps = 1;
            RaceLaunch.CarIndex = 0;
            try
            {
                SceneManager.LoadScene("Game");
                yield return null;
                var loop = Object.FindFirstObjectByType<RaceLoop>();
                Assert.IsNotNull(loop, "RaceLoop in scene Game");
                var frames = (List<object?>)TraceFiles.Trace()["frames"]!;
                loop.SteerSource = new ScriptedSteer(frames, loop);
                bool lapOne = false, finished = false;
                loop.OnEvent += e =>
                {
                    if (e.Kind == RaceEvent.EventKind.LapDone && e.Number == 1) lapOne = true;
                    if (e.Kind == RaceEvent.EventKind.Finished) finished = true;
                };
                loop.StartRace();
                float t0 = Time.realtimeSinceStartup;
                while (!finished && Time.realtimeSinceStartup - t0 < 90f) yield return new WaitForFixedUpdate();

                Assert.IsTrue(lapOne, "LapDone 1");
                Assert.IsTrue(finished, "Finished within 90 s (steps " + loop.StepCount + ")");
                Assert.IsFalse(loop.Running, "Running after Finished");
                Assert.That(loop.StepCount, Is.InRange(1300, 1360), "StepCount");
                Assert.IsTrue(Mathf.Approximately(Time.fixedDeltaTime, 1f / 60f), "fixedDeltaTime " + Time.fixedDeltaTime);
            }
            finally
            {
                RaceLaunch.AutoStart = true;
                RaceLaunch.Laps = null;
                RaceLaunch.CarIndex = 0;
            }
        }
    }
}
