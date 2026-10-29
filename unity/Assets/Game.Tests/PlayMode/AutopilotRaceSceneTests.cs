// SPEC-03 step 4b: the harness autopilot drives a real Time Attack race to the finish on green-study (forward and reversed).
#nullable enable
using System.Collections;
using DesktopDrift.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DesktopDrift.Game.Tests
{
    public class AutopilotRaceSceneTests
    {
        const int MaxSteps = 7000;

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

        static IEnumerator Drive(bool reversed)
        {
            RaceLaunch.AutoStart = false;
            SceneManager.LoadScene("Game");
            yield return null;
            var loop = Object.FindFirstObjectByType<RaceLoop>();
            Assert.IsNotNull(loop, "RaceLoop in scene Game");
            var source = new AutopilotSteerSource(loop);
            loop.SteerSource = source;

            RaceLaunch.Mode = RaceMode.TimeAttack;
            RaceLaunch.TrackId = "green-study";
            RaceLaunch.Reversed = reversed;
            loop.Relaunch();
            Assert.AreEqual("green-study", loop.Track.Id);
            bool finished = false;
            loop.OnEvent += e => { if (e.Kind == RaceEvent.EventKind.Finished) finished = true; };
            int steps = 0;
            while (!finished && steps < MaxSteps) { loop.StepOnce(source.Resolve()); steps++; }
            Assert.IsTrue(finished, "Finished event within " + MaxSteps + " steps (lap " + loop.State.LapNum + ", steps " + steps + ")");
            Assert.AreEqual(3, loop.State.LapNum, "LapNum at finish; steps to finish = " + steps);
            Debug.Log("autopilot green-study reversed=" + reversed + " finished in " + steps + " steps");
        }

        [UnityTest]
        public IEnumerator AutopilotFinishesGreenStudyForward() => Drive(false);

        [UnityTest]
        public IEnumerator AutopilotFinishesGreenStudyReversed() => Drive(true);
    }
}
