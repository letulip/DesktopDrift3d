// Port of DesktopDrift/tests/achievements.test.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    [TestFixture]
    public class AchievementsTests
    {
        // A representative world: 3 tracks x forward/reversed = 6 instances, shop with 2 sections.
        private static ContentDescriptor MakeContent()
        {
            return new ContentDescriptor
            {
                ForwardIds = new List<string> { "green-study", "steel-kitchen", "workbench" },
                ReversedIds = new List<string> { "green-study:rev", "steel-kitchen:rev", "workbench:rev" },
                AllInstanceIds = new List<string>
                {
                    "green-study", "steel-kitchen", "workbench",
                    "green-study:rev", "steel-kitchen:rev", "workbench:rev",
                },
                ForwardTrackIds = new List<string> { "green-study", "steel-kitchen", "workbench" },
                ShopCategories = new List<string> { "finish", "trail" },
                CatalogById = new Dictionary<string, string> { { "finish-matte", "finish" }, { "trail-mint", "trail" } },
                Cars = new List<string> { "plum", "toretto", "smasher" },
                Names = new Dictionary<string, string>
                {
                    { "green-study", "Midnight Deadline" },
                    { "green-study:rev", "Midnight Deadline (reversed)" },
                },
            };
        }

        private static readonly ContentDescriptor Content = MakeContent();

        // Minimal finished-run scaffold; override per test.
        private static RunStats Run(Action<RunStats>? over = null)
        {
            var r = new RunStats
            {
                Finished = true, Pps = 120, Stars = 1, NearMisses = 0, Crashes = 3, ConesHit = 2, ConesTotal = 6,
                TimeAt8 = 0, ComboUnbroken = false, TiresThisRun = 0, TireTotalOnTrack = 4, Hour = 14,
            };
            if (over != null) over(r);
            return r;
        }

        private static AchievementContext Ctx(RunStats? run = null, double wallet = 0,
            string[]? owned = null, string[]? ownedCars = null, IEnumerable<string>? cleared = null,
            Dictionary<string, double>? records = null, Dictionary<string, List<object?>>? caps = null,
            double runs = 0, double driftSecs = 0)
        {
            return new AchievementContext
            {
                Content = Content, Wallet = wallet,
                Owned = owned != null ? owned.ToList() : new List<string>(),
                OwnedCars = ownedCars != null ? ownedCars.ToList() : new List<string>(),
                Cleared = cleared != null ? cleared.ToList() : new List<string>(),
                Records = records ?? new Dictionary<string, double>(),
                Caps = caps ?? new Dictionary<string, List<object?>>(),
                Lifetime = new AchievementContext.LifetimeStats { Runs = runs, DriftSecs = driftSecs },
                Run = run,
            };
        }

        // Which ids does Evaluate() unlock for this ctx (from a clean slate)?
        private static HashSet<string> Firing(AchievementContext c)
        {
            return new HashSet<string>(Achievements.Evaluate(c, new HashSet<string>()).Unlocked.Select(u => u.Id));
        }

        private static Dictionary<string, double> Recs(IEnumerable<string> ids, double v)
        {
            return ids.ToDictionary(id => id, id => v);
        }

        private static Dictionary<string, object?> Obj(params (string, object?)[] kv)
        {
            var d = new Dictionary<string, object?>();
            foreach ((string k, object? v) in kv) d[k] = v;
            return d;
        }

        // flattenRecords: { inst: { timeattack: { bestPPS } } } → { inst: bestPPS }
        [Test]
        public void FlattenRecords_NestedBestPps()
        {
            var flat = Achievements.FlattenRecords(new Dictionary<string, object?>
            {
                { "green-study", Obj(("timeattack", Obj(("bestPPS", 640.0), ("bestPPSTotal", 1.0), ("bestPPSTime", 2.0)))) },
                { "green-study:rev", Obj(("timeattack", Obj(("bestPPS", 300.0)))) },
                { "empty", Obj() },   // no timeattack → skipped
            });
            Assert.That(flat, Is.EqualTo(new Dictionary<string, double> { { "green-study", 640 }, { "green-study:rev", 300 } }));
            Assert.That(Achievements.FlattenRecords(null), Is.Empty);
        }

        // buildContent: derives instances + shop sections from the registries
        [Test]
        public void BuildContent_DerivesInstancesAndShopSections()
        {
            var tracks = new[]
            {
                new Achievements.ContentTrack { Id = "a", Name = "Alpha" },
                new Achievements.ContentTrack { Id = "b", Name = "Beta" },
            };
            var catalog = new[]
            {
                new Achievements.ContentShopItem { Id = "finish-x", Kind = "finish" },
                new Achievements.ContentShopItem { Id = "trail-y", Kind = "trail" },
                new Achievements.ContentShopItem { Id = "trail-z", Kind = "trail" },
            };
            ContentDescriptor c = Achievements.BuildContent(tracks, catalog, new Achievements.ContentCar[0]);
            Assert.That(c.ForwardIds, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(c.ReversedIds, Is.EqualTo(new[] { "a:rev", "b:rev" }));
            Assert.That(c.AllInstanceIds, Is.EqualTo(new[] { "a", "b", "a:rev", "b:rev" }));
            Assert.That(c.ShopCategories, Is.EqualTo(new[] { "finish", "trail" }));   // distinct kinds, order preserved
            Assert.That(c.CatalogById["trail-y"], Is.EqualTo("trail"));
            Assert.That(c.Names["a:rev"], Is.EqualTo("Alpha (reversed)"));
        }

        // ── Catalog integrity ─────────────────────────────────────────────────

        // catalog: ids are unique
        [Test]
        public void Catalog_IdsAreUnique()
        {
            var ids = Achievements.BuildCatalog(Content).Select(a => a.Id).ToList();
            Assert.That(ids.Count, Is.EqualTo(new HashSet<string>(ids).Count));
        }

        // catalog: every check runs on a null-run ctx without throwing
        [Test]
        public void Catalog_EveryCheckRunsOnNullRunCtx()
        {
            AchievementContext x = Ctx();   // run: null (the purchase-eval shape)
            foreach (AchievementDef a in Achievements.BuildCatalog(Content))
                Assert.DoesNotThrow(() => a.Check(x), a.Id);
        }

        // catalog: generates one DDK crown per instance + Absolute DDK
        [Test]
        public void Catalog_OneDdkCrownPerInstancePlusAbsolute()
        {
            var ids = Achievements.BuildCatalog(Content).Select(a => a.Id).ToList();
            foreach (string inst in Content.AllInstanceIds) Assert.That(ids, Does.Contain("ddk-" + inst), "missing ddk-" + inst);
            Assert.That(ids, Does.Contain("absolute-ddk"));
        }

        // catalog: DDK name uses the display name from content
        [Test]
        public void Catalog_DdkNameUsesDisplayName()
        {
            AchievementDef ddk = Achievements.BuildCatalog(Content).First(a => a.Id == "ddk-green-study");
            Assert.That(ddk.Name, Is.EqualTo("DDK Midnight Deadline"));
        }

        // ── Progression ───────────────────────────────────────────────────────

        // first-drift fires on a finished run OR any recorded history
        [Test]
        public void FirstDrift_FinishedRunOrHistory()
        {
            Assert.That(Firing(Ctx(run: Run())), Does.Contain("first-drift"));
            Assert.That(Firing(Ctx(run: null)), Does.Not.Contain("first-drift"));                                   // truly fresh save
            Assert.That(Firing(Ctx(run: null, cleared: new[] { "green-study" })), Does.Contain("first-drift"));     // finished before
            Assert.That(Firing(Ctx(run: null, records: new Dictionary<string, double> { { "green-study", 120 } })), Does.Contain("first-drift"));
        }

        // road-tripper needs every forward instance cleared (not just some)
        [Test]
        public void RoadTripper_NeedsEveryForwardInstance()
        {
            Assert.That(Firing(Ctx(cleared: new[] { "green-study", "steel-kitchen" })), Does.Not.Contain("road-tripper"));
            Assert.That(Firing(Ctx(cleared: Content.ForwardIds)), Does.Contain("road-tripper"));
        }

        // mirror-walker fires on any reversed clear; backwards needs all
        [Test]
        public void MirrorWalker_AnyReversed_BackwardsNeedsAll()
        {
            Assert.That(Firing(Ctx(cleared: new[] { "green-study:rev" })), Does.Contain("mirror-walker"));
            Assert.That(Firing(Ctx(cleared: new[] { "green-study:rev" })), Does.Not.Contain("backwards"));
            Assert.That(Firing(Ctx(cleared: Content.ReversedIds)), Does.Contain("backwards"));
        }

        // completionist needs every instance, both directions
        [Test]
        public void Completionist_NeedsEveryInstance()
        {
            Assert.That(Firing(Ctx(cleared: Content.ForwardIds)), Does.Not.Contain("completionist"));
            Assert.That(Firing(Ctx(cleared: Content.AllInstanceIds)), Does.Contain("completionist"));
        }

        // clear-family is retrospective: records count as proof of a clear
        [Test]
        public void ClearFamily_IsRetrospective_RecordsCountAsClear()
        {
            Func<IEnumerable<string>, HashSet<string>> noCleared = ids => Firing(Ctx(cleared: new string[0], records: Recs(ids, 200)));

            Assert.That(noCleared(Content.ForwardIds), Does.Contain("road-tripper"));
            Assert.That(noCleared(Content.ReversedIds), Does.Contain("backwards"));
            Assert.That(noCleared(new[] { "green-study:rev" }), Does.Contain("mirror-walker"));
            Assert.That(noCleared(Content.AllInstanceIds), Does.Contain("completionist"));

            // cleared and records combine rather than replace each other
            HashSet<string> split = Firing(Ctx(cleared: Content.ForwardIds, records: Recs(Content.ReversedIds, 200)));
            Assert.That(split, Does.Contain("completionist"));
        }

        // clear-family still requires EVERY instance (records do not over-credit)
        [Test]
        public void ClearFamily_StillRequiresEveryInstance()
        {
            var partial = Content.ForwardIds.Take(Content.ForwardIds.Count - 1);
            Assert.That(Firing(Ctx(cleared: new string[0], records: Recs(partial, 200))), Does.Not.Contain("road-tripper"));
            Assert.That(Firing(Ctx(cleared: new string[0], records: new Dictionary<string, double> { { "green-study", 200 } })), Does.Not.Contain("completionist"));
        }

        // ── Skill ─────────────────────────────────────────────────────────────

        // star tiers fire at their thresholds and not below (this run)
        [Test]
        public void StarTiers_FireAtThresholds()
        {
            Assert.That(Firing(Ctx(run: Run(r => r.Stars = 2))), Does.Not.Contain("three-star"));
            Assert.That(Firing(Ctx(run: Run(r => r.Stars = 3))), Does.Contain("three-star"));
            HashSet<string> f4 = Firing(Ctx(run: Run(r => r.Stars = 4)));
            Assert.That(f4.Contains("three-star") && f4.Contains("four-star") && !f4.Contains("five-star"), Is.True);
            Assert.That(Firing(Ctx(run: Run(r => r.Stars = 5))), Does.Contain("five-star"));
        }

        // star tiers also fire from a recorded best (returning player, run:null)
        [Test]
        public void StarTiers_FireFromRecordedBest()
        {
            // starsForPps: 100/star. 250→2★, 300→3★, 520→5★.
            Assert.That(Firing(Ctx(run: null, records: new Dictionary<string, double> { { "x", 250 } })), Does.Not.Contain("three-star")); // 2★ < 3
            Assert.That(Firing(Ctx(run: null, records: new Dictionary<string, double> { { "x", 300 } })), Does.Contain("three-star"));     // 3★
            HashSet<string> f = Firing(Ctx(run: null, records: new Dictionary<string, double> { { "a", 120 }, { "b", 520 } }));           // best = 520 → 5★
            Assert.That(f.Contains("three-star") && f.Contains("four-star") && f.Contains("five-star"), Is.True);
        }

        // daredevil at 10 near misses
        [Test]
        public void Daredevil_AtTenNearMisses()
        {
            Assert.That(Firing(Ctx(run: Run(r => r.NearMisses = 9))), Does.Not.Contain("daredevil"));
            Assert.That(Firing(Ctx(run: Run(r => r.NearMisses = 10))), Does.Contain("daredevil"));
        }

        // ── Combo ─────────────────────────────────────────────────────────────

        // flow tiers key off timeAt8
        [Test]
        public void FlowTiers_KeyOffTimeAt8()
        {
            Assert.That(Firing(Ctx(run: Run(r => r.TimeAt8 = 10))), Does.Contain("flow-1"));
            Assert.That(Firing(Ctx(run: Run(r => r.TimeAt8 = 10))), Does.Not.Contain("flow-2"));
            Assert.That(Firing(Ctx(run: Run(r => r.TimeAt8 = 30))), Does.Contain("flow-2"));
            Assert.That(Firing(Ctx(run: Run(r => r.TimeAt8 = 30))), Does.Not.Contain("flow-3"));   // 60s tier
            Assert.That(Firing(Ctx(run: Run(r => r.TimeAt8 = 60))), Does.Contain("flow-3"));
        }

        // perpetual needs a finished, unbroken run
        [Test]
        public void Perpetual_NeedsFinishedUnbrokenRun()
        {
            Assert.That(Firing(Ctx(run: Run(r => r.ComboUnbroken = false))), Does.Not.Contain("perpetual"));
            Assert.That(Firing(Ctx(run: Run(r => r.ComboUnbroken = true))), Does.Contain("perpetual"));
        }

        // ── Economy ───────────────────────────────────────────────────────────

        // clean-sweep: all tires this run, and the track has tires
        [Test]
        public void CleanSweep_AllTiresAndTrackHasTires()
        {
            Assert.That(Firing(Ctx(run: Run(r => { r.TiresThisRun = 3; r.TireTotalOnTrack = 4; }))), Does.Not.Contain("clean-sweep"));
            Assert.That(Firing(Ctx(run: Run(r => { r.TiresThisRun = 4; r.TireTotalOnTrack = 4; }))), Does.Contain("clean-sweep"));
            Assert.That(Firing(Ctx(run: Run(r => { r.TiresThisRun = 0; r.TireTotalOnTrack = 0; }))), Does.Not.Contain("clean-sweep")); // no tires → never
        }

        // soda-pop fires once any cap is collected (works with run:null)
        [Test]
        public void SodaPop_AnyCapCollected()
        {
            Assert.That(Firing(Ctx(caps: new Dictionary<string, List<object?>>())), Does.Not.Contain("soda-pop"));
            Assert.That(Firing(Ctx(caps: new Dictionary<string, List<object?>> { { "green-study", new List<object?> { 0 } } })), Does.Contain("soda-pop"));
        }

        // big-spender / fashionista / well-rounded on the purchase-eval (run:null)
        [Test]
        public void ShopAchievements_OnPurchaseEval()
        {
            Assert.That(Firing(Ctx(owned: new[] { "finish-matte" })), Does.Contain("big-spender"));
            Assert.That(Firing(Ctx(owned: new[] { "a", "b", "c", "d" })), Does.Not.Contain("fashionista"));
            Assert.That(Firing(Ctx(owned: new[] { "a", "b", "c", "d", "e" })), Does.Contain("fashionista"));
            Assert.That(Firing(Ctx(owned: new[] { "finish-matte" })), Does.Not.Contain("well-rounded"));            // only 1 of 2 sections
            Assert.That(Firing(Ctx(owned: new[] { "finish-matte", "trail-mint" })), Does.Contain("well-rounded")); // both sections
        }

        // ── Hidden ────────────────────────────────────────────────────────────

        // glass-cannon: 5 stars AND 15+ near misses
        [Test]
        public void GlassCannon_FiveStarsAndFifteenNearMisses()
        {
            Assert.That(Firing(Ctx(run: Run(r => { r.Stars = 5; r.NearMisses = 14; }))), Does.Not.Contain("glass-cannon"));
            Assert.That(Firing(Ctx(run: Run(r => { r.Stars = 5; r.NearMisses = 15; }))), Does.Contain("glass-cannon"));
        }

        // crash gags: fire per-race at 5/10/20/40, graduated, run-only
        [Test]
        public void CrashGags_GraduatedRunOnly()
        {
            Assert.That(Firing(Ctx(run: Run(r => r.Crashes = 4))).Contains("crash-5"), Is.False);
            Assert.That(Firing(Ctx(run: Run(r => r.Crashes = 5))), Does.Contain("crash-5"));
            Assert.That(Firing(Ctx(run: Run(r => r.Crashes = 5))).Contains("crash-10"), Is.False);
            HashSet<string> f40 = Firing(Ctx(run: Run(r => r.Crashes = 40)));
            Assert.That(f40.Contains("crash-5") && f40.Contains("crash-10") && f40.Contains("crash-20") && f40.Contains("crash-40"), Is.True);  // one messy race unlocks all
            Assert.That(Firing(Ctx(run: null)).Contains("crash-5"), Is.False);                                                                    // not reconstructable
        }

        // untouchable: finished with zero crashes
        [Test]
        public void Untouchable_FinishedWithZeroCrashes()
        {
            Assert.That(Firing(Ctx(run: Run(r => r.Crashes = 1))), Does.Not.Contain("untouchable"));
            Assert.That(Firing(Ctx(run: Run(r => r.Crashes = 0))), Does.Contain("untouchable"));
        }

        // cone achievements: slalom (none hit) vs bulldozer (all hit)
        [Test]
        public void ConeAchievements_SlalomVsBulldozer()
        {
            Assert.That(Firing(Ctx(run: Run(r => { r.ConesHit = 0; r.ConesTotal = 6; }))), Does.Contain("slalom-saint"));
            Assert.That(Firing(Ctx(run: Run(r => { r.ConesHit = 0; r.ConesTotal = 0; }))), Does.Not.Contain("slalom-saint")); // no cones → not earned
            Assert.That(Firing(Ctx(run: Run(r => { r.ConesHit = 6; r.ConesTotal = 6; }))), Does.Contain("bulldozer"));
            Assert.That(Firing(Ctx(run: Run(r => { r.ConesHit = 5; r.ConesTotal = 6; }))), Does.Not.Contain("bulldozer"));
        }

        // one-pps: fires only when the rounded run PPS is exactly 1
        [Test]
        public void OnePps_OnlyWhenRoundedPpsIsOne()
        {
            Assert.That(Firing(Ctx(run: Run(r => r.Pps = 1))), Does.Contain("one-pps"));       // a whole 1 PPS
            Assert.That(Firing(Ctx(run: Run(r => r.Pps = 1.4))), Does.Contain("one-pps"));     // rounds to 1 (matches the results screen)
            Assert.That(Firing(Ctx(run: Run(r => r.Pps = 1.5))), Does.Not.Contain("one-pps")); // rounds to 2
            Assert.That(Firing(Ctx(run: Run(r => r.Pps = 0))), Does.Not.Contain("one-pps"));   // shows "0 PPS", not 1
            Assert.That(Firing(Ctx(run: Run(r => r.Pps = 120))), Does.Not.Contain("one-pps")); // a normal run never triggers it
            Assert.That(Firing(Ctx(run: null)), Does.Not.Contain("one-pps"));                  // not reconstructable from history
        }

        // cola-collector: a cap on every forward track
        [Test]
        public void ColaCollector_CapOnEveryForwardTrack()
        {
            Assert.That(Firing(Ctx(caps: new Dictionary<string, List<object?>>
            {
                { "green-study", new List<object?> { 0 } }, { "steel-kitchen", new List<object?> { 0 } },
            })), Does.Not.Contain("cola-collector"));
            Assert.That(Firing(Ctx(caps: new Dictionary<string, List<object?>>
            {
                { "green-study", new List<object?> { 0 } }, { "steel-kitchen", new List<object?> { 0 } },
                { "workbench", new List<object?> { 0 } },
            })), Does.Contain("cola-collector"));
        }

        // time-of-day buckets are disjoint and correct
        [Test]
        public void TimeOfDayBuckets_AreDisjointAndCorrect()
        {
            Assert.That(Firing(Ctx(run: Run(r => r.Hour = 23))), Does.Contain("night-owl"));
            Assert.That(Firing(Ctx(run: Run(r => r.Hour = 1))), Does.Contain("night-owl"));
            Assert.That(Firing(Ctx(run: Run(r => r.Hour = 3))), Does.Contain("midnight-drift"));
            Assert.That(Firing(Ctx(run: Run(r => r.Hour = 6))), Does.Contain("early-bird"));
            HashSet<string> noon = Firing(Ctx(run: Run(r => r.Hour = 12)));
            Assert.That(!noon.Contains("night-owl") && !noon.Contains("midnight-drift") && !noon.Contains("early-bird"), Is.True);
        }

        // ── DDK ───────────────────────────────────────────────────────────────

        // DDK_PPS is 600; per-instance crown fires at the threshold
        [Test]
        public void DdkPps_Is600_CrownFiresAtThreshold()
        {
            Assert.That(Economy.DdkPps, Is.EqualTo(600));
            Assert.That(Firing(Ctx(records: new Dictionary<string, double> { { "green-study", 599 } })), Does.Not.Contain("ddk-green-study"));
            Assert.That(Firing(Ctx(records: new Dictionary<string, double> { { "green-study", 600 } })), Does.Contain("ddk-green-study"));
        }

        // absolute-ddk needs the crown on every instance
        [Test]
        public void AbsoluteDdk_NeedsCrownOnEveryInstance()
        {
            Dictionary<string, double> almost = Recs(Content.AllInstanceIds, 600);
            almost["workbench:rev"] = 599;
            Assert.That(Firing(Ctx(records: almost)), Does.Not.Contain("absolute-ddk"));
            Assert.That(Firing(Ctx(records: Recs(Content.AllInstanceIds, 700))), Does.Contain("absolute-ddk"));
        }

        // ── Ladders + evaluate mechanics ──────────────────────────────────────

        // ladders report progress and unlock at target
        [Test]
        public void Ladders_ReportProgressAndUnlockAtTarget()
        {
            Achievements.EvalResult r = Achievements.Evaluate(Ctx(runs: 12, driftSecs: 25 * 60), new HashSet<string>());
            Achievements.ProgressEntry races10 = r.Progress.First(p => p.Id == "races-10");
            Assert.That(races10.Value, Is.EqualTo(12));
            var ids = new HashSet<string>(r.Unlocked.Select(u => u.Id));
            Assert.That(ids, Does.Contain("races-10"));        // 12 ≥ 10
            Assert.That(ids, Does.Not.Contain("races-50"));    // 12 < 50
            Assert.That(ids.Contains("drift-10") && ids.Contains("drift-25"), Is.True); // 25 min ≥ both
            Assert.That(ids, Does.Not.Contain("drift-50"));
        }

        // hoard ladder keys off the wallet balance
        [Test]
        public void HoardLadder_KeysOffWallet()
        {
            var ids = new HashSet<string>(Achievements.Evaluate(Ctx(wallet: 1200), new HashSet<string>()).Unlocked.Select(u => u.Id));
            Assert.That(ids.Contains("hoard-500") && ids.Contains("hoard-1000"), Is.True);
            Assert.That(ids, Does.Not.Contain("hoard-2500"));
        }

        // evaluate skips already-unlocked ids (idempotent, no re-fire)
        [Test]
        public void Evaluate_SkipsAlreadyUnlocked()
        {
            AchievementContext world = Ctx(run: Run(r => r.Stars = 5));
            Achievements.EvalResult first = Achievements.Evaluate(world, new HashSet<string>());
            Assert.That(first.Unlocked.Any(u => u.Id == "five-star"), Is.True);
            Achievements.EvalResult again = Achievements.Evaluate(world,
                new HashSet<string> { "five-star", "first-drift", "three-star", "four-star" });
            Assert.That(again.Unlocked.Any(u => u.Id == "five-star"), Is.False);
        }

        // evaluate returns name/icon/reward for the toast
        [Test]
        public void Evaluate_ReturnsNameIconReward()
        {
            Achievements.UnlockedEntry u = Achievements.Evaluate(Ctx(run: Run()), new HashSet<string>())
                .Unlocked.First(e => e.Id == "first-drift");
            Assert.That(u.Id, Is.EqualTo("first-drift"));
            Assert.That(u.Name, Is.EqualTo("First Drift"));
            Assert.That(u.Icon, Is.EqualTo("🏁"));
            Assert.That(u.Reward, Is.EqualTo(10));
        }

        // car achievements fire from ownedCars
        [Test]
        public void CarAchievements_FireFromOwnedCars()
        {
            Assert.That(Firing(Ctx()), Does.Not.Contain("new-wheels"));                                     // none owned
            Assert.That(Firing(Ctx(ownedCars: new[] { "plum" })), Does.Contain("new-wheels"));              // first car
            Assert.That(Firing(Ctx(ownedCars: new[] { "plum", "toretto" })), Does.Not.Contain("three-car-garage"));
            Assert.That(Firing(Ctx(ownedCars: new[] { "plum", "toretto", "smasher" })), Does.Contain("three-car-garage"));
            Assert.That(Firing(Ctx(ownedCars: new[] { "plum", "toretto" })), Does.Not.Contain("full-garage")); // not all
            Assert.That(Firing(Ctx(ownedCars: new[] { "plum", "toretto", "smasher" })), Does.Contain("full-garage")); // all 3
        }

        // ── Catalog metadata proven equal to the exported achievements.json ───

        private static Dictionary<string, object?> LoadJson(string name)
        {
            string path = TestPaths.Locate("unity/Assets/Content/_json/" + name);
            return (Dictionary<string, object?>)Json.Parse(File.ReadAllText(path))!;
        }

        private static List<Dictionary<string, object?>> Items(Dictionary<string, object?> doc, string key)
        {
            return ((List<object?>)doc[key]!).Select(o => (Dictionary<string, object?>)o!).ToList();
        }

        private static List<string> Strs(object? o)
        {
            return ((List<object?>)o!).Select(s => (string)s!).ToList();
        }

        private static Dictionary<string, string> StrMap(object? o)
        {
            return ((Dictionary<string, object?>)o!).ToDictionary(kv => kv.Key, kv => (string)kv.Value!);
        }

        [Test]
        public void CatalogMatchesExportedJson()
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

            Dictionary<string, object?> expected = LoadJson("achievements.json");
            var ec = (Dictionary<string, object?>)expected["content"]!;

            ContentDescriptor content = Achievements.BuildContent(tracks, shop, cars);
            Assert.That(content.ForwardIds, Is.EqualTo(Strs(ec["forwardIds"])), "forwardIds");
            Assert.That(content.ReversedIds, Is.EqualTo(Strs(ec["reversedIds"])), "reversedIds");
            Assert.That(content.AllInstanceIds, Is.EqualTo(Strs(ec["allInstanceIds"])), "allInstanceIds");
            Assert.That(content.ForwardTrackIds, Is.EqualTo(Strs(ec["forwardTrackIds"])), "forwardTrackIds");
            Assert.That(content.ShopCategories, Is.EqualTo(Strs(ec["shopCategories"])), "shopCategories");
            Assert.That(content.Cars, Is.EqualTo(Strs(ec["cars"])), "cars");
            Assert.That(content.CatalogById, Is.EqualTo(StrMap(ec["catalogById"])), "catalogById");
            Assert.That(content.Names, Is.EqualTo(StrMap(ec["names"])), "names");

            var order = Items(expected, "categoryOrder");
            Assert.That(Achievements.CategoryOrder.Count, Is.EqualTo(order.Count), "categoryOrder length");
            for (int i = 0; i < order.Count; i++)
            {
                Assert.That(Achievements.CategoryOrder[i].Key, Is.EqualTo((string)order[i]["key"]!), "categoryOrder key " + i);
                Assert.That(Achievements.CategoryOrder[i].Label, Is.EqualTo((string)order[i]["label"]!), "categoryOrder label " + i);
            }

            var want = Items(expected, "catalog");
            List<AchievementDef> got = Achievements.BuildCatalog(content);
            Assert.That(got.Count, Is.EqualTo(want.Count), "catalog length");
            for (int i = 0; i < want.Count; i++)
            {
                AchievementDef g = got[i];
                Dictionary<string, object?> w = want[i];
                string at = "catalog[" + i + "] " + (string)w["id"]!;
                Assert.That(g.Id, Is.EqualTo((string)w["id"]!), at + " id");
                Assert.That(g.Name, Is.EqualTo((string)w["name"]!), at + " name");
                Assert.That(g.Desc, Is.EqualTo((string)w["desc"]!), at + " desc");
                Assert.That(g.Icon, Is.EqualTo((string)w["icon"]!), at + " icon");
                Assert.That(g.Category, Is.EqualTo((string)w["category"]!), at + " category");
                Assert.That(g.Hidden, Is.EqualTo((bool)w["hidden"]!), at + " hidden");
                Assert.That((double)g.Reward, Is.EqualTo((double)w["reward"]!), at + " reward");
                Assert.That(g.Bar, Is.EqualTo(w.ContainsKey("bar")), at + " bar");
                if (w.ContainsKey("target")) Assert.That(g.Target, Is.EqualTo((double)w["target"]!), at + " target");
            }
        }
    }
}
