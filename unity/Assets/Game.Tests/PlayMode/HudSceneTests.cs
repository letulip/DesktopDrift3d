#nullable enable
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DesktopDrift.Game.Tests
{
    public class HudSceneTests
    {
        [UnityTest]
        public IEnumerator HudShowsCountdownHintAndPauseOverlay()
        {
            RaceLaunch.AutoStart = true;
            try
            {
                SceneManager.LoadScene("Game");
                yield return null;
                yield return new WaitForSeconds(0.5f);
                var loop = Object.FindFirstObjectByType<RaceLoop>();
                var hud = Object.FindFirstObjectByType<HudController>();
                Assert.IsNotNull(loop, "RaceLoop");
                Assert.IsNotNull(hud, "HudController");
                var root = hud.GetComponent<UIDocument>().rootVisualElement;
                var count = root.Q<Label>("count");
                Assert.IsNotNull(count, "count");
                Assert.That(count.text == "3" || count.text == "2", "countdown text: " + count.text);
                Assert.AreEqual(DisplayStyle.Flex, root.Q("hint").resolvedStyle.display, "hint visible");

                loop.Paused = true;
                yield return null;
                yield return null;
                Assert.AreEqual(DisplayStyle.Flex, root.Q("pauseOverlay").resolvedStyle.display, "pause overlay");
                loop.Paused = false;
            }
            finally
            {
                RaceLaunch.AutoStart = true;
                RaceLaunch.Mode = RaceMode.TimeAttack;
                RaceLaunch.CarIndex = 0;
                RaceLaunch.Laps = null;
            }
        }
    }
}
