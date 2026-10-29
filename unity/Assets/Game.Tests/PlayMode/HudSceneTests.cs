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

        // Feel round 1 (D-23): the portrait values live under the "narrow" class on the document root; the
        // selector must reach elements below the UXML container, and the two widths are the 2D sandbox.css
        // values x3 (120px / 156px). The controller is disabled so its per-frame toggle cannot interfere.
        [UnityTest]
        public IEnumerator NarrowClassSwitchesPortraitLayout()
        {
            RaceLaunch.AutoStart = false;
            try
            {
                SceneManager.LoadScene("Game");
                yield return null;
                var hud = Object.FindFirstObjectByType<HudController>();
                Assert.IsNotNull(hud, "HudController");
                hud.enabled = false;
                var root = hud.GetComponent<UIDocument>().rootVisualElement;
                var timePanel = root.Q("timePanel");
                var mini = root.Q("mini");
                Assert.IsNotNull(timePanel, "timePanel");

                root.EnableInClassList("narrow", true);
                yield return null;
                yield return null;
                // Tolerance: the panel scale snaps layout to device pixels (360 -> 360.79 in the batch editor).
                Assert.AreEqual(360f, timePanel.resolvedStyle.width, 5f, "portrait width (2D 120px x3)");
                Assert.AreEqual(309f, mini.resolvedStyle.height, 5f, "portrait minimap height (2D 103px x3)");

                root.EnableInClassList("narrow", false);
                yield return null;
                yield return null;
                Assert.AreEqual(468f, timePanel.resolvedStyle.width, 5f, "landscape width (2D 156px x3)");
                hud.enabled = true;
            }
            finally
            {
                RaceLaunch.AutoStart = true;
            }
        }
    }
}
