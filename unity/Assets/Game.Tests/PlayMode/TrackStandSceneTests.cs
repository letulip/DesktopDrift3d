// SPEC-03 step 8: the temporary track-select stand (2D track-select structure and wording) in the live Game scene.
#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DesktopDrift.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DesktopDrift.Game.Tests
{
    public class TrackStandSceneTests
    {
        sealed class NoSteer : ISteerSource { public int Resolve() => 0; }

        RaceLoop loop = null!;
        HudController hud = null!;
        VisualElement root = null!;

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

        static List<Dictionary<string, object?>> Tracks()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Content/_json/tracks.json"));
            var rows = new List<Dictionary<string, object?>>();
            foreach (object? r in (List<object?>)((Dictionary<string, object?>)Json.Parse(json)!)["TRACKS"]!)
                rows.Add((Dictionary<string, object?>)r!);
            return rows;
        }

        IEnumerator LoadGame()
        {
            RaceLaunch.AutoStart = false;
            SceneManager.LoadScene("Game");
            yield return null;
            loop = Object.FindFirstObjectByType<RaceLoop>();
            hud = Object.FindFirstObjectByType<HudController>();
            Assert.IsNotNull(loop, "RaceLoop");
            Assert.IsNotNull(hud, "HudController");
            loop.SteerSource = new NoSteer();
            // Let the UIDocument build its tree and the controller bind it.
            for (int i = 0; i < 5; i++) yield return null;
            root = hud.GetComponent<UIDocument>().rootVisualElement;
        }

        static void Press(Button b)
        {
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        static void Tap(VisualElement v)
        {
            using (var e = ClickEvent.GetPooled()) { e.target = v; v.SendEvent(e); }
        }

        static bool Shown(VisualElement e) => e.resolvedStyle.display == DisplayStyle.Flex;

        [UnityTest]
        public IEnumerator TimeAttackStandListsTracksAndStartsReversedInstance()
        {
            yield return LoadGame();
            var rows = Tracks();
            Assert.AreEqual(6, rows.Count, "tracks.json rows");

            hud.OpenTrackStand(RaceMode.TimeAttack);
            yield return null;
            Assert.IsTrue(Shown(root.Q("trackOverlay")), "trackOverlay");
            Assert.AreEqual("Time Attack", root.Q<Label>("trackTitle").text);
            Assert.AreEqual("Choose a track", root.Q<Label>("trackSub").text);
            Assert.IsTrue(Shown(root.Q("trackToggle")), "toggle shown for Time Attack");
            Assert.AreEqual("Normal", root.Q<Button>("btnNormal").text);
            Assert.AreEqual("Reversed", root.Q<Button>("btnReversed").text);
            Assert.AreEqual("← Back to menu", root.Q<Button>("trackBack").text);

            var cards = root.Query<VisualElement>(className: "track-card").ToList();
            Assert.AreEqual(6, cards.Count, "cards");
            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual((string)rows[i]["name"]!, cards[i].Q<Label>(className: "track-name").text, "name " + i);
                Assert.AreEqual((string)rows[i]["desc"]!, cards[i].Q<Label>(className: "track-desc").text, "desc " + i);
                Assert.IsNotNull(cards[i].Q<TrackThumbElement>(), "thumbnail " + i);
            }

            Press(root.Q<Button>("btnReversed"));
            Assert.IsTrue(root.Q<Button>("btnReversed").ClassListContains("is-active"));
            Assert.IsFalse(root.Q<Button>("btnNormal").ClassListContains("is-active"));
            Tap(cards[2]);

            string id = (string)rows[2]["id"]!;
            Assert.AreEqual(id, loop.Track.Id);
            Assert.IsTrue(loop.Track.Reversed, "reversed track");
            Assert.AreEqual(id + ":rev", loop.State.Instance);
            Assert.AreEqual(RaceMode.TimeAttack, RaceLaunch.Mode);
            Assert.IsTrue(loop.Running);
            yield return null;
            Assert.IsFalse(Shown(root.Q("trackOverlay")), "stand closed after a race starts");
        }

        [UnityTest]
        public IEnumerator ZenStandHidesToggleAndIsAlwaysForward()
        {
            yield return LoadGame();
            var rows = Tracks();
            RaceLaunch.Reversed = true;   // stale selection must not leak into Zen

            hud.OpenTrackStand(RaceMode.Zen);
            yield return null;
            Assert.AreEqual("Zen Drift", root.Q<Label>("trackTitle").text);
            Assert.AreEqual("Choose a track — just drift", root.Q<Label>("trackSub").text);
            Assert.IsFalse(Shown(root.Q("trackToggle")), "toggle hidden for Zen");

            var cards = root.Query<VisualElement>(className: "track-card").ToList();
            Assert.AreEqual(6, cards.Count);
            Tap(cards[0]);
            string id = (string)rows[0]["id"]!;
            Assert.AreEqual(id, loop.Track.Id);
            Assert.IsFalse(loop.Track.Reversed);
            Assert.AreEqual(id, loop.State.Instance);
            Assert.IsTrue(loop.State.Zen);
        }

        [UnityTest]
        public IEnumerator BackReturnsToModeStandAndSandboxStartsTheOval()
        {
            yield return LoadGame();

            hud.ShowModeStand();
            yield return null;
            Assert.IsTrue(Shown(root.Q("launchOverlay")), "mode stand");
            Press(root.Q<Button>("launchTimeAttack"));
            yield return null;
            Assert.IsTrue(Shown(root.Q("trackOverlay")), "Time Attack opens the track stand");
            Assert.IsFalse(Shown(root.Q("launchOverlay")), "mode stand hidden");

            Press(root.Q<Button>("trackBack"));
            yield return null;
            Assert.IsFalse(Shown(root.Q("trackOverlay")), "track stand closed");
            Assert.IsTrue(Shown(root.Q("launchOverlay")), "back to the mode stand");

            Press(root.Q<Button>("launchSandbox"));
            Assert.AreEqual(OvalTrack.Build().Id, loop.Track.Id, "Sandbox is the oval");
            Assert.AreEqual("", loop.Track.Id);
            Assert.IsTrue(loop.Running);
            yield return null;
            Assert.IsFalse(Shown(root.Q("launchOverlay")), "mode stand closed");
            Assert.IsFalse(Shown(root.Q("trackOverlay")), "no track stand for Sandbox");
        }
    }
}
