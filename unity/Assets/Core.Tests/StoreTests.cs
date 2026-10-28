// Port of DesktopDrift/tests/store.test.js @ 5556140 (2026-08-06), ported 2026-10-28.
// The JS module singleton becomes a fresh SaveSchema.Load(null).State per test, so each test sets up
// the state it depends on (the JS tests ran in sequence against one shared store).
#nullable enable
using System.Collections.Generic;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    [TestFixture]
    public class StoreTests
    {
        private static SaveState Fresh() { return SaveSchema.Load(null).State; }
        private static object? P(string json) { return Json.Parse(json); }

        private static Dictionary<string, object?> Last(SaveState s)
        {
            List<object?> l = s.Ledger();
            return (Dictionary<string, object?>)l[l.Count - 1]!;
        }

        [Test]
        public void Defaults_Garage()
        {
            SaveState s = Fresh();
            Dictionary<string, object?> g = s.Garage();
            Assert.That(g["carIndex"], Is.EqualTo(0.0));
            Assert.That(g["cars"], Is.EqualTo(P("{}")));
        }

        [Test]
        public void CarLook_LazilyReturnsNullDefaultLookPerCarIndex()
        {
            Assert.That(Fresh().CarLook(0), Is.EqualTo(P(
                "{\"bodyColor\":null,\"neonColor\":null,\"finish\":null,\"trailColor\":null,\"glassColor\":null,\"outlineColor\":null,\"neon\":null,\"expression\":null}")));
        }

        [Test]
        public void Defaults_SettingsRecordsAchievements()
        {
            SaveState s = Fresh();
            Assert.That(s.Settings(), Is.EqualTo(P("{\"units\":\"kmh\",\"haptics\":true,\"soundEnabled\":true,\"volume\":1.0}")));
            Assert.That(s.Records(), Is.EqualTo(P("{}")));
            Assert.That(s.Achievements(), Is.EqualTo(P("{}")));
        }

        [Test]
        public void Getters_ReturnTheSameLiveObjectAcrossCalls()
        {
            SaveState s = Fresh();
            Assert.That(s.Garage(), Is.SameAs(s.Garage()));
        }

        [Test]
        public void Stats_DefaultsToCapsTiresClearedPlusLifetimeCounters()
        {
            Assert.That(Fresh().Stats(), Is.EqualTo(P(
                "{\"caps\":{},\"tires\":{},\"tiresSwept\":[],\"trophies\":[],\"cleared\":[],\"runs\":0,\"driftSecs\":0}")));
        }

        [Test]
        public void Wallet_DefaultsTo0_AddTiresAccumulatesAndClampsAt0()
        {
            SaveState s = Fresh();
            Assert.That(s.Wallet(), Is.EqualTo(0));
            Assert.That(s.AddTires(5), Is.EqualTo(5));
            Assert.That(s.AddTires(3), Is.EqualTo(8));
            Assert.That(s.Wallet(), Is.EqualTo(8));
            Assert.That(s.AddTires(-100), Is.EqualTo(0)); // clamped, never negative
        }

        [Test]
        public void HasTrophy_MarkTrophy_PerInstanceBadgeMarkerIdempotent()
        {
            SaveState s = Fresh();
            Assert.That(s.HasTrophy("green-study"), Is.False);
            Assert.That(s.MarkTrophy("green-study"), Is.True);
            Assert.That(s.HasTrophy("green-study"), Is.True);
            Assert.That(s.MarkTrophy("green-study"), Is.False);
            Assert.That(s.HasTrophy("green-study:rev"), Is.False); // reversed is a separate instance
        }

        [Test]
        public void HasPerpetual_MarkPerpetual_PerInstanceBadgeMarkerIdempotent()
        {
            SaveState s = Fresh();
            Assert.That(s.HasPerpetual("green-study"), Is.False);
            Assert.That(s.MarkPerpetual("green-study"), Is.True);
            Assert.That(s.HasPerpetual("green-study"), Is.True);
            Assert.That(s.MarkPerpetual("green-study"), Is.False);
            Assert.That(s.HasPerpetual("green-study:rev"), Is.False);
        }

        [Test]
        public void TireSwept_MarkTireSwept_PerTrackFlagFirstTimeOnly()
        {
            SaveState s = Fresh();
            Assert.That(s.TireSwept("green-study"), Is.False);
            Assert.That(s.MarkTireSwept("green-study"), Is.True);
            Assert.That(s.TireSwept("green-study"), Is.True);
            Assert.That(s.MarkTireSwept("green-study"), Is.False);
            Assert.That(s.TireSwept("steel-kitchen"), Is.False); // independent per instance
        }

        [Test]
        public void CollectedCaps_ReturnsEmptyForUnknownTrack()
        {
            Assert.That(Fresh().CollectedCaps("green-study"), Is.Empty);
        }

        [Test]
        public void MarkCleared_TrueOnFirstFinishFalseThereafter()
        {
            SaveState s = Fresh();
            Assert.That(s.MarkCleared("green-study"), Is.True);
            Assert.That(s.MarkCleared("green-study"), Is.False);
            Assert.That(s.MarkCleared("green-study:rev"), Is.True);
            Assert.That(s.Stats()["cleared"], Is.EqualTo(P("[\"green-study\",\"green-study:rev\"]")));
        }

        [Test]
        public void CapCollect_RecordsACapIndex()
        {
            SaveState s = Fresh();
            s.CapCollect("green-study", 0);
            Assert.That(s.CollectedCaps("green-study"), Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void CapCollect_NoOpIfIndexAlreadyRecorded()
        {
            SaveState s = Fresh();
            s.CapCollect("green-study", 0);
            int rev = s.Revision;
            s.CapCollect("green-study", 0);
            Assert.That(s.CollectedCaps("green-study"), Is.EqualTo(new[] { 0 }));
            Assert.That(s.Revision, Is.EqualTo(rev)); // nothing to persist
        }

        [Test]
        public void CapCollect_AppendsNewIndices()
        {
            SaveState s = Fresh();
            s.CapCollect("green-study", 0);
            s.CapCollect("green-study", 2);
            Assert.That(s.CollectedCaps("green-study"), Is.EqualTo(new[] { 0, 2 }));
        }

        [Test]
        public void MutateLiveObject_PlusSave_PersistsCorrectJsonShape()
        {
            SaveState s = Fresh();
            int persisted = 0;
            s.OnChanged = () => persisted++;
            s.Garage()["carIndex"] = 2.0;
            Dictionary<string, object?> lk = s.CarLook(2);
            lk["bodyColor"] = "#ff0000";
            lk["neonColor"] = "#39FF14";
            s.Save();
            Assert.That(persisted, Is.EqualTo(1));

            var raw = (Dictionary<string, object?>)Json.Parse(Json.Serialize(s.Root))!;
            Assert.That(raw["version"], Is.EqualTo(1.0)); // 3D edition schema v1 (D-17), not the 2D 4
            var g = (Dictionary<string, object?>)raw["garage"]!;
            Assert.That(g["carIndex"], Is.EqualTo(2.0));
            var car2 = (Dictionary<string, object?>)((Dictionary<string, object?>)g["cars"]!)["2"]!;
            Assert.That(car2["bodyColor"], Is.EqualTo("#ff0000"));
            Assert.That(car2["neonColor"], Is.EqualTo("#39FF14"));
            Assert.That(raw["settings"], Is.EqualTo(P("{\"units\":\"kmh\",\"haptics\":true,\"soundEnabled\":true,\"volume\":1.0}")));
        }

        // ── Shop: owned / grant / purchase + per-car looks ────────────────────

        [Test]
        public void Owned_DefaultsToEmpty()
        {
            Assert.That(Fresh().Owned(), Is.Empty);
        }

        [Test]
        public void Grant_RecordsOwnership_Idempotent_IsOwnedReflectsIt()
        {
            SaveState s = Fresh();
            Assert.That(s.IsOwned("finish-matte"), Is.False);
            s.Grant("finish-matte");
            s.Grant("finish-matte"); // no-op (already owned)
            Assert.That(s.Owned(), Is.EqualTo(new object[] { "finish-matte" }));
            Assert.That(s.IsOwned("finish-matte"), Is.True);
        }

        [Test]
        public void CarLook_EachCarKeepsAnIndependentLook()
        {
            SaveState s = Fresh();
            Dictionary<string, object?> a = s.CarLook(0); a["finish"] = "matte"; a["trailColor"] = "#ff00aa";
            Dictionary<string, object?> b = s.CarLook(1); b["finish"] = "chrome"; b["neonColor"] = "#00ffcc";
            s.Save();
            Assert.That(s.CarLook(0)["finish"], Is.EqualTo("matte"));
            Assert.That(s.CarLook(0)["trailColor"], Is.EqualTo("#ff00aa"));
            Assert.That(s.CarLook(1)["finish"], Is.EqualTo("chrome"));
            Assert.That(s.CarLook(0)["neonColor"], Is.Null); // car 1's neon did NOT leak onto car 0
            var raw = (Dictionary<string, object?>)Json.Parse(Json.Serialize(s.Root))!;
            var cars = (Dictionary<string, object?>)((Dictionary<string, object?>)raw["garage"]!)["cars"]!;
            Assert.That(((Dictionary<string, object?>)cars["0"]!)["finish"], Is.EqualTo("matte"));
            Assert.That(((Dictionary<string, object?>)cars["1"]!)["finish"], Is.EqualTo("chrome"));
        }

        [Test]
        public void Purchase_SuccessDeductsWalletGrantsItem_FailureLeavesStateIntact()
        {
            SaveState s = Fresh();
            s.Grant("finish-matte");
            s.AddTires(100); // wallet -> 100
            BuyResult ok = s.Purchase(new ShopItem { Id = "finish-pearl", Price = 80 });
            Assert.That(ok.Ok, Is.True);
            Assert.That(ok.Wallet, Is.EqualTo(20));
            Assert.That(ok.Owned, Is.EqualTo(new[] { "finish-matte", "finish-pearl" }));
            Assert.That(s.Wallet(), Is.EqualTo(20));
            Assert.That(s.IsOwned("finish-pearl"), Is.True);

            // already owned -> rejected, no further deduction
            BuyResult dup = s.Purchase(new ShopItem { Id = "finish-pearl", Price = 80 });
            Assert.That(dup.Ok, Is.False);
            Assert.That(dup.Reason, Is.EqualTo("owned"));
            Assert.That(s.Wallet(), Is.EqualTo(20));

            // too expensive -> rejected, wallet untouched
            BuyResult broke = s.Purchase(new ShopItem { Id = "finish-chrome", Price = 250 });
            Assert.That(broke.Ok, Is.False);
            Assert.That(broke.Reason, Is.EqualTo("broke"));
            Assert.That(s.Wallet(), Is.EqualTo(20));
            Assert.That(s.IsOwned("finish-chrome"), Is.False);
        }

        // ── Ledger (tire-coin history) ────────────────────────────────────────

        [Test]
        public void Ledger_AddTiresRecordsASignedEntryWithReasonAndResultingBalance()
        {
            SaveState s = Fresh();
            s.NowMs = () => 1700000000000; // JS: Date.now() > 0 (Core has no clock – injected here, see SaveState.NowMs)
            int before = s.Ledger().Count;
            s.AddTires(7, "Test reward");
            Dictionary<string, object?> last = Last(s);
            Assert.That(s.Ledger().Count, Is.EqualTo(before + 1));
            Assert.That(last["amount"], Is.EqualTo(7.0));
            Assert.That(last["reason"], Is.EqualTo("Test reward"));
            Assert.That(last["balance"], Is.EqualTo((double)s.Wallet()));
            Assert.That((double)last["t"]!, Is.GreaterThan(0));
        }

        [Test]
        public void Ledger_ZeroDeltaChangeRecordsNothing()
        {
            SaveState s = Fresh();
            int before = s.Ledger().Count;
            s.AddTires(0, "noop");
            Assert.That(s.Ledger().Count, Is.EqualTo(before));
        }

        [Test]
        public void Ledger_AddTiresWithoutReasonChangesWalletButLogsNothing()
        {
            SaveState s = Fresh();
            int before = s.Ledger().Count;
            int w = s.Wallet();
            s.AddTires(3); // no reason -> silent
            Assert.That(s.Wallet(), Is.EqualTo(w + 3));
            Assert.That(s.Ledger().Count, Is.EqualTo(before));
        }

        [Test]
        public void RecordTxn_LogsAnEntryWithoutChangingTheWallet()
        {
            SaveState s = Fresh();
            int w = s.Wallet();
            int before = s.Ledger().Count;
            s.RecordTxn(12, "Stainless Speedway — 12 tires");
            Dictionary<string, object?> last = Last(s);
            Assert.That(s.Wallet(), Is.EqualTo(w)); // balance unchanged
            Assert.That(s.Ledger().Count, Is.EqualTo(before + 1));
            Assert.That(last["amount"], Is.EqualTo(12.0));
            Assert.That(last["reason"], Is.EqualTo("Stainless Speedway — 12 tires"));
            Assert.That(last["balance"], Is.EqualTo((double)w));
            s.RecordTxn(0, "noop"); // 0 is a no-op
            Assert.That(s.Ledger().Count, Is.EqualTo(before + 1));
        }

        [Test]
        public void Ledger_PurchaseLogsABoughtEntryWithTheNegativeAmount()
        {
            SaveState s = Fresh();
            s.AddTires(500, "topup");
            s.Purchase(new ShopItem { Id = "trail-mint", Name = "Mint", Price = 40, Kind = "trail", Value = "#00FF88" });
            Dictionary<string, object?> last = Last(s);
            Assert.That(last["reason"], Is.EqualTo("Bought Mint"));
            Assert.That(last["amount"], Is.EqualTo(-40.0));
        }

        [Test]
        public void Ledger_CappedAt50EntriesOldestDropped()
        {
            SaveState s = Fresh();
            for (int i = 0; i < 60; i++) s.AddTires(1, "spam " + i);
            Assert.That(s.Ledger().Count, Is.LessThanOrEqualTo(SaveState.LedgerMax));
            Assert.That(s.Ledger().Count, Is.EqualTo(50));
            Assert.That(Last(s)["reason"], Is.EqualTo("spam 59"));
        }

        // ── Achievements slice ────────────────────────────────────────────────

        [Test]
        public void Achievements_DefaultEmpty_AchUnlockedIsAnEmptySet()
        {
            SaveState s = Fresh();
            Assert.That(s.AchAll(), Is.EqualTo(P("{}")));
            Assert.That(s.AchUnlocked().Count, Is.EqualTo(0));
        }

        [Test]
        public void AchUnlock_IdempotentPersistsReturnsTrueOnlyTheFirstTime()
        {
            SaveState s = Fresh();
            Assert.That(s.AchUnlock("first-drift"), Is.True);
            Assert.That(s.AchUnlock("first-drift"), Is.False); // already unlocked -> no re-fire
            Assert.That(s.AchUnlocked(), Does.Contain("first-drift"));
            Assert.That(((Dictionary<string, object?>)s.AchAll()["first-drift"]!)["unlocked"], Is.EqualTo(true));
        }

        [Test]
        public void AchSetProgress_LatchesToTheMaxSeenNeverRegresses()
        {
            SaveState s = Fresh();
            double Progress() { return (double)((Dictionary<string, object?>)s.AchAll()["races-10"]!)["progress"]!; }
            s.AchSetProgress("races-10", 4);
            Assert.That(Progress(), Is.EqualTo(4.0));
            s.AchSetProgress("races-10", 7);
            Assert.That(Progress(), Is.EqualTo(7.0));
            s.AchSetProgress("races-10", 3); // lower value ignored
            Assert.That(Progress(), Is.EqualTo(7.0));
        }

        [Test]
        public void AchUnlocked_ReflectsOnlyUnlockedIdsNotMereProgress()
        {
            SaveState s = Fresh();
            s.AchSetProgress("drift-50", 12); // progress but not unlocked
            Assert.That(s.AchUnlocked(), Does.Not.Contain("drift-50"));
            s.AchUnlock("drift-50");
            Assert.That(s.AchUnlocked(), Does.Contain("drift-50"));
        }
    }
}
