// Port of DesktopDrift/tests/ach-sync.test.js @ 5556140 (2026-08-06), ported 2026-10-28.
// Seeds a save that already meets several achievement conditions (a returning player), loads it
// through SaveSchema.Load, and verifies AchievementSync.SyncState unlocks + credits them once.
#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    [TestFixture]
    public class AchSyncTests
    {
        private static Dictionary<string, object?> LoadJson(string name)
        {
            string path = TestPaths.Locate("unity/Assets/Content/_json/" + name);
            return (Dictionary<string, object?>)Json.Parse(File.ReadAllText(path))!;
        }

        private static List<Dictionary<string, object?>> Items(Dictionary<string, object?> doc, string key)
        {
            return ((List<object?>)doc[key]!).Select(o => (Dictionary<string, object?>)o!).ToList();
        }

        private static ContentDescriptor BuildContent()
        {
            var tracks = Items(LoadJson("tracks.json"), "TRACKS")
                .Select(t => new Achievements.ContentTrack { Id = (string)t["id"]!, Name = (string)t["name"]! }).ToList();
            var shop = Items(LoadJson("shop.json"), "CATALOG")
                .Select(i => new Achievements.ContentShopItem { Id = (string)i["id"]!, Kind = (string)i["kind"]! }).ToList();
            var cars = Items(LoadJson("cars.json"), "CARS")
                .Select(c => new Achievements.ContentCar
                {
                    Id = (string)c["id"]!,
                    Price = c.TryGetValue("price", out object? p) && p is double pd ? (double?)pd : null,
                }).ToList();
            return Achievements.BuildContent(tracks, shop, cars);
        }

        // A pre-feature save: EVERY track cleared both ways, a 640 record, one item from every shop section,
        // 600 tires, a cola cap on every track. Derived from the live track list so adding a track can't
        // silently break the "unlocks everything" assertions.
        private static (SaveState State, string First, ContentDescriptor Content) Seed()
        {
            List<string> forwardIds = Items(LoadJson("tracks.json"), "TRACKS").Select(t => (string)t["id"]!).ToList();
            var cleared = new List<object?>();
            foreach (string id in forwardIds) cleared.Add(id);
            foreach (string id in forwardIds) cleared.Add(id + ":rev");
            var caps = new Dictionary<string, object?>();
            foreach (string id in forwardIds) caps[id] = new List<object?> { 0.0 };
            string first = forwardIds[0]; // the 640-PPS record goes on the first track (DDK + stars)

            var save = new Dictionary<string, object?>
            {
                ["version"] = 2.0,
                ["wallet"] = 600.0,
                ["owned"] = new List<object?>
                {
                    "finish-matte", "trail-mint", "trail-oro", "glass-smoke", "outline-white", "emo-joy",
                    "neon-layout-per-zone", "neon-anim-pulse",
                },
                ["stats"] = new Dictionary<string, object?>
                {
                    ["caps"] = caps, ["tires"] = new Dictionary<string, object?>(), ["cleared"] = cleared,
                    ["runs"] = 0.0, ["driftSecs"] = 0.0,
                },
                ["records"] = new Dictionary<string, object?>
                {
                    [first] = new Dictionary<string, object?>
                    {
                        ["timeattack"] = new Dictionary<string, object?>
                        {
                            ["bestPPS"] = 640.0, ["bestPPSTotal"] = 20000.0, ["bestPPSTime"] = 31.0,
                        },
                    },
                },
                ["achievements"] = new Dictionary<string, object?>(),
            };
            SaveState state = SaveSchema.Load(Json.Serialize(save)).State;
            return (state, first, BuildContent());
        }

        [Test]
        public void RetroactiveSweep_UnlocksEverythingAlreadyEarnedByState()
        {
            var (s, first, content) = Seed();
            int before = s.Wallet();
            var unlocked = new HashSet<string>(AchievementSync.SyncState(s, content).Select(u => u.Id));

            // Progression (from `cleared`)
            foreach (string id in new[] { "road-tripper", "mirror-walker", "backwards", "completionist" })
                Assert.That(unlocked, Does.Contain(id), "expected " + id);
            // Economy (from `owned`) - both shop sections present -> well-rounded too
            foreach (string id in new[] { "big-spender", "fashionista", "well-rounded" })
                Assert.That(unlocked, Does.Contain(id), "expected " + id);
            // Collection (from `caps`)
            Assert.That(unlocked, Does.Contain("soda-pop"));
            Assert.That(unlocked, Does.Contain("cola-collector"));
            // Wallet ladder (600 >= 500) + DDK crown (record 640 >= 600 on the first track)
            Assert.That(unlocked, Does.Contain("hoard-500"));
            Assert.That(unlocked, Does.Contain("ddk-" + first));
            // Reconstructable from records: finished-ever + star tiers (640 PPS -> 5 stars)
            Assert.That(unlocked, Does.Contain("first-drift"));
            Assert.That(unlocked, Does.Contain("three-star"));
            Assert.That(unlocked, Does.Contain("four-star"));
            Assert.That(unlocked, Does.Contain("five-star"));

            // Genuinely run-only (never stored: near misses, cones, combo, time) -> still locked
            Assert.That(unlocked, Does.Not.Contain("daredevil"));
            Assert.That(unlocked, Does.Not.Contain("glass-cannon"));
            Assert.That(unlocked, Does.Not.Contain("slalom-saint"));

            // Rewards were credited (wallet grew) and the store now records them as unlocked
            Assert.That(s.Wallet(), Is.GreaterThan(before), "rewards should be credited to the wallet");
            Assert.That(s.AchUnlocked(), Does.Contain("completionist"));
        }

        [Test]
        public void Sweep_IsIdempotent_ASecondCallUnlocksNothingAndPaysNothing()
        {
            var (s, _, content) = Seed();
            AchievementSync.SyncState(s, content);
            int w = s.Wallet();
            List<Achievements.UnlockedEntry> again = AchievementSync.SyncState(s, content);
            Assert.That(again.Count, Is.EqualTo(0));
            Assert.That(s.Wallet(), Is.EqualTo(w));
        }
    }
}
