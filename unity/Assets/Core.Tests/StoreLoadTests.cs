// Port of DesktopDrift/tests/store-load.test.js @ 5556140 (2026-08-06), ported 2026-10-28.
// The JS seeds localStorage with a JSON string; here the same string goes to SaveSchema.Load.
// Excluded (2D migrations are not ported, D-17 / SPEC-01 §6):
//   - "v1->v2 migration: old global look moves onto the active car" (store-load.test.js line 29)
//   - "v4 tire-sweep migration grandfathers fully-collected tracks" (store-load.test.js line 106)
#nullable enable
using System.Collections.Generic;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    [TestFixture]
    public class StoreLoadTests
    {
        private const string Seed =
            "{\"version\":1,\"settings\":{\"units\":\"mph\"},\"garage\":{\"carIndex\":3,\"bodyColor\":\"#00ff00\"}," +
            "\"records\":{\"oval\":{\"timeattack\":{\"bestPPS\":1250,\"bestPPSTotal\":45000,\"bestPPSTime\":36}}}," +
            "\"achievements\":{\"firstDrift\":{\"unlocked\":true,\"progress\":1}}}";

        private static SaveState Seeded() { return SaveSchema.Load(Seed).State; }
        private static object? P(string json) { return Json.Parse(json); }
        private const string DefaultSettings = "{\"units\":\"kmh\",\"haptics\":true,\"soundEnabled\":true,\"volume\":1.0}";

        [Test]
        public void LoadsPersistedSettings_AndFillsMissingFieldsFromDefaults()
        {
            // Saved units kept; haptics absent in the save -> filled from defaults (no data loss).
            Assert.That(Seeded().Settings(), Is.EqualTo(P("{\"units\":\"mph\",\"haptics\":true,\"soundEnabled\":true,\"volume\":1.0}")));
        }

        [Test]
        public void Owned_MissingSliceOnAnOldSaveIsFilledFromDefaults()
        {
            Assert.That(Seeded().Owned(), Is.Empty);
        }

        [Test]
        public void LoadsPersistedRecordsAndAchievements()
        {
            SaveState s = Seeded();
            var oval = (Dictionary<string, object?>)s.Records()["oval"]!;
            Assert.That(oval["timeattack"], Is.EqualTo(P("{\"bestPPS\":1250,\"bestPPSTotal\":45000,\"bestPPSTime\":36}")));
            Assert.That(((Dictionary<string, object?>)s.Achievements()["firstDrift"]!)["unlocked"], Is.EqualTo(true));
        }

        [Test]
        public void Stats_MissingSliceIsFilledFromDefaultsOnAnExistingSave()
        {
            Assert.That(Seeded().Stats(), Is.EqualTo(P(
                "{\"caps\":{},\"tires\":{},\"tiresSwept\":[],\"trophies\":[],\"cleared\":[],\"runs\":0,\"driftSecs\":0}")));
        }

        [Test]
        public void Wallet_MissingFieldOnAnOldSaveIsFilledFromDefaults()
        {
            Assert.That(Seeded().Wallet(), Is.EqualTo(0));
        }

        [Test]
        public void CollectedCaps_ReturnsEmptyWhenStatsSliceAbsentFromSave()
        {
            Assert.That(Seeded().CollectedCaps("green-study"), Is.Empty);
        }

        // Unknown / "future" version must NOT wipe data: merged over defaults like any other save.
        [Test]
        public void UnknownVersionPreservesData_NoReset()
        {
            LoadResult r = SaveSchema.Load(
                "{\"version\":999,\"settings\":{\"units\":\"mph\"},\"records\":{\"oval\":{\"timeattack\":{\"bestPPS\":42}}}}");
            Assert.That(r.Normalized, Is.True);
            Assert.That(r.State.Settings(), Is.EqualTo(P("{\"units\":\"mph\",\"haptics\":true,\"soundEnabled\":true,\"volume\":1.0}")));
            var oval = (Dictionary<string, object?>)r.State.Records()["oval"]!;
            Assert.That(((Dictionary<string, object?>)oval["timeattack"]!)["bestPPS"], Is.EqualTo(42.0));
            Assert.That(r.State.Root["version"], Is.EqualTo((double)SaveSchema.Version)); // re-stamped, data kept
        }

        // Corrupt / unparseable data is the ONLY case that resets the whole store to defaults.
        [Test]
        public void CorruptSaveFallsBackToDefaults()
        {
            LoadResult r = SaveSchema.Load("{ not valid json");
            Assert.That(r.Normalized, Is.False);
            Assert.That(r.State.Settings(), Is.EqualTo(P(DefaultSettings)));
            Assert.That(r.State.Records(), Is.EqualTo(P("{}")));
        }

        // Content validation: a present-but-corrupt slice heals to its default, other data is kept.
        [Test]
        public void CorruptSlice_WrongType_HealsToDefault_OtherDataKept()
        {
            SaveState s = SaveSchema.Load(
                "{\"version\":1,\"settings\":null,\"garage\":\"oops\",\"records\":{\"oval\":{\"timeattack\":{\"bestPPS\":7}}}}").State;
            Assert.That(s.Settings(), Is.EqualTo(P(DefaultSettings)));
            Assert.That(s.Settings()["units"], Is.EqualTo("kmh"));
            Assert.That(s.Garage(), Is.EqualTo(P("{\"carIndex\":0,\"cars\":{}}")));
            var oval = (Dictionary<string, object?>)s.Records()["oval"]!;
            Assert.That(((Dictionary<string, object?>)oval["timeattack"]!)["bestPPS"], Is.EqualTo(7.0));
        }

        // A saved wallet balance + the clean-swept-tracks list survive a load (economy persistence).
        [Test]
        public void WalletAndSweptTracksArePreservedOnLoad()
        {
            SaveState s = SaveSchema.Load(
                "{\"version\":1,\"wallet\":137,\"stats\":{\"caps\":{},\"tiresSwept\":[\"green-study\"]}}").State;
            Assert.That(s.Wallet(), Is.EqualTo(137));
            Assert.That(s.TireSwept("green-study"), Is.True);
        }
    }
}
