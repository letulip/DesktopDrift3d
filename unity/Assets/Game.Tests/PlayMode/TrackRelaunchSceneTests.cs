// SPEC-03 step 4a: Relaunch swaps oval <-> real track (reversed instance) in the live Game scene.
#nullable enable
using System.Collections;
using DesktopDrift.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DesktopDrift.Game.Tests
{
    public class TrackRelaunchSceneTests
    {
        sealed class NoSteer : ISteerSource { public int Resolve() => 0; }

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
        public IEnumerator RelaunchSwapsRealReversedTrackAndOval()
        {
            RaceLaunch.AutoStart = false;
            SceneManager.LoadScene("Game");
            yield return null;
            var loop = Object.FindFirstObjectByType<RaceLoop>();
            Assert.IsNotNull(loop, "RaceLoop in scene Game");
            loop.SteerSource = new NoSteer();

            RaceLaunch.Mode = RaceMode.TimeAttack;
            RaceLaunch.TrackId = "green-study";
            RaceLaunch.Reversed = true;
            loop.Relaunch();
            Assert.AreEqual("green-study", loop.Track.Id);
            float t0 = Time.realtimeSinceStartup;
            while (loop.StepCount < 120 && Time.realtimeSinceStartup - t0 < 30f) yield return new WaitForFixedUpdate();
            Assert.GreaterOrEqual(loop.StepCount, 120, "fixed steps ran");
            Assert.AreEqual("green-study:rev", loop.State.Instance);
            Assert.AreEqual(3, loop.State.TotalLaps);

            RaceLaunch.Mode = RaceMode.Sandbox;
            loop.Relaunch();
            Assert.AreEqual(OvalTrack.Build().Id, loop.Track.Id, "Sandbox is the oval");
            Assert.IsTrue(loop.Running);
            yield return new WaitForFixedUpdate();
        }
    }
}
