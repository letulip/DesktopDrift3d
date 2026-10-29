// SPEC-03 step 4a: TrackRuntime.Load for all 12 instances (6 tracks x forward/reversed) and the RaceLaunch track options.
#nullable enable
using System;
using DesktopDrift.Core;
using NUnit.Framework;
using UnityEditor;

namespace DesktopDrift.Game.EditModeTests
{
    public sealed class TrackRuntimeTests
    {
        const string CatalogPath = "Assets/Content/Tracks/TrackCatalog.asset";

        static TrackCatalog Catalog()
        {
            var c = AssetDatabase.LoadAssetAtPath<TrackCatalog>(CatalogPath);
            Assert.IsNotNull(c, "missing " + CatalogPath + " - run scripts/import-tracks.sh");
            return c!;
        }

        [TearDown]
        public void TearDown()
        {
            RaceLaunch.Mode = RaceMode.TimeAttack;
            RaceLaunch.TrackId = null;
            RaceLaunch.Reversed = false;
            RaceLaunch.Laps = null;
        }

        [Test]
        public void Load_AllTwelveInstances()
        {
            TrackCatalog catalog = Catalog();
            Assert.AreEqual(6, catalog.Tracks.Count);
            foreach (TrackAsset a in catalog.Tracks)
                foreach (bool rev in new[] { false, true })
                {
                    TrackData t = TrackRuntime.Load(catalog, a.Id, rev);
                    Assert.AreEqual(a.Id, t.Id, a.Id);
                    Assert.AreEqual(rev, t.Reversed, a.Id + " reversed");
                    Assert.Greater(t.Center.Count, 0, a.Id + " center");
                    Assert.AreEqual(rev ? a.Id + ":rev" : a.Id, TrackUtil.InstanceId(t.Id, rev), a.Id);
                }
        }

        [Test]
        public void Load_UnknownIdThrowsNamingTheId()
        {
            var ex = Assert.Throws<ArgumentException>(() => TrackRuntime.Load(Catalog(), "no-such-track", false));
            StringAssert.Contains("no-such-track", ex!.Message);
        }

        [Test]
        public void BuildOptions_ReversedOnlyForTimeAttackAndZen()
        {
            RaceLaunch.Reversed = true;
            foreach (var (mode, want) in new[] { (RaceMode.TimeAttack, true), (RaceMode.Zen, true), (RaceMode.Sandbox, false) })
            {
                RaceLaunch.Mode = mode;
                Assert.AreEqual(want, RaceLaunch.BuildOptions(new ContentDescriptor(), 7).Reversed, mode.ToString());
            }
            RaceLaunch.Reversed = false;
            RaceLaunch.Mode = RaceMode.TimeAttack;
            Assert.IsFalse(RaceLaunch.BuildOptions(new ContentDescriptor(), 7).Reversed);
        }

        [Test]
        public void ResolveTrack_SandboxIsOval_OtherwiseRealTrack()
        {
            TrackCatalog catalog = Catalog();
            RaceLaunch.Mode = RaceMode.Sandbox;
            RaceLaunch.TrackId = "green-study";
            Assert.AreEqual(OvalTrack.Build().Id, RaceLaunch.ResolveTrack(catalog).Id);
            RaceLaunch.Mode = RaceMode.Zen;
            Assert.AreEqual("green-study", RaceLaunch.ResolveTrack(catalog).Id);
            RaceLaunch.TrackId = null;
            Assert.AreEqual(catalog.Tracks[0].Id, RaceLaunch.ResolveTrack(catalog).Id);
            Assert.AreEqual(OvalTrack.Build().Id, RaceLaunch.ResolveTrack(null).Id);
        }
    }
}
