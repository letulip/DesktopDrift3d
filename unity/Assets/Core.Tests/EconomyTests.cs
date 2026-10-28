#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    [TestFixture]
    public class EconomyTests
    {
        private const double Undef = double.NaN; // JS undefined / null

        // isOnePps: true only when PPS rounds to exactly 1 (matches the results screen)
        [Test]
        public void IsOnePps_OnlyWhenRoundsToOne()
        {
            Assert.That(Economy.IsOnePps(1), Is.True);
            Assert.That(Economy.IsOnePps(1.4), Is.True);
            Assert.That(Economy.IsOnePps(0.5), Is.True);
            Assert.That(Economy.IsOnePps(1.5), Is.False);
            Assert.That(Economy.IsOnePps(0), Is.False);
            Assert.That(Economy.IsOnePps(120), Is.False);
            Assert.That(Economy.IsOnePps(Undef), Is.False);
            Assert.That(Economy.OnePpsBonus, Is.GreaterThan(0));
        }

        // DDK: crown threshold is 600 PPS; isDDK is inclusive
        [Test]
        public void Ddk_ThresholdInclusive()
        {
            Assert.That(Economy.DdkPps, Is.EqualTo(600));
            Assert.That(Economy.IsDdk(599), Is.False);
            Assert.That(Economy.IsDdk(600), Is.True);
            Assert.That(Economy.IsDdk(1200), Is.True);
            Assert.That(Economy.IsDdk(0), Is.False);
            Assert.That(Economy.IsDdk(Undef), Is.False);
        }

        // DDK does not change finish payout (stays capped at MAX_STARS)
        [Test]
        public void Ddk_DoesNotChangeFinishPayout()
        {
            Assert.That(Economy.FinishPayout(600), Is.EqualTo(Economy.FinishFlat + Economy.FinishPerStar * Economy.MaxStars));
            Assert.That(Economy.FinishPayout(9999), Is.EqualTo(Economy.FinishFlat + Economy.FinishPerStar * Economy.MaxStars));
        }

        // starsForPps: 1 star per 100 PPS, capped at MAX_STARS
        [Test]
        public void StarsForPps_OnePer100Capped()
        {
            Assert.That(Economy.StarsForPps(0), Is.EqualTo(0));
            Assert.That(Economy.StarsForPps(99), Is.EqualTo(0));
            Assert.That(Economy.StarsForPps(100), Is.EqualTo(1));
            Assert.That(Economy.StarsForPps(250), Is.EqualTo(2));
            Assert.That(Economy.StarsForPps(599), Is.EqualTo(5));
            Assert.That(Economy.StarsForPps(99999), Is.EqualTo(Economy.MaxStars));
        }

        // starsForPps: defensive on missing / negative input (undefined and null both map to NaN)
        [Test]
        public void StarsForPps_DefensiveOnMissingOrNegative()
        {
            Assert.That(Economy.StarsForPps(Undef), Is.EqualTo(0));
            Assert.That(Economy.StarsForPps(Undef), Is.EqualTo(0));
            Assert.That(Economy.StarsForPps(-50), Is.EqualTo(0));
        }

        // finishPayout: flat + per-star, 2..12 across 0..5 stars
        [Test]
        public void FinishPayout_FlatPlusPerStar()
        {
            Assert.That(Economy.FinishPayout(0), Is.EqualTo(Economy.FinishFlat));
            Assert.That(Economy.FinishPayout(100), Is.EqualTo(Economy.FinishFlat + Economy.FinishPerStar));
            Assert.That(Economy.FinishPayout(99999), Is.EqualTo(Economy.FinishFlat + Economy.FinishPerStar * Economy.MaxStars));
        }

        // FIRST_CLEAR_BONUS: the one-time per-instance reward is 20
        [Test]
        public void FirstClearBonus_Is20()
        {
            Assert.That(Economy.FirstClearBonus, Is.EqualTo(20));
        }

        // canAfford: balance must cover the price (defensive on missing balance)
        [Test]
        public void CanAfford_BalanceMustCoverPrice()
        {
            Assert.That(Economy.CanAfford(100, 80), Is.True);
            Assert.That(Economy.CanAfford(80, 80), Is.True);
            Assert.That(Economy.CanAfford(79, 80), Is.False);
            Assert.That(Economy.CanAfford(Undef, 1), Is.False);
        }

        // buy: success deducts price and extends owned (snapshot untouched)
        [Test]
        public void Buy_SuccessDeductsAndExtends()
        {
            var owned = new List<string> { "a" };
            BuyResult r = Economy.Buy(100, owned, "b", 80);
            Assert.That(r.Ok, Is.True);
            Assert.That(r.Reason, Is.Null);
            Assert.That(r.Wallet, Is.EqualTo(20));
            Assert.That(r.Owned.ToArray(), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(owned.ToArray(), Is.EqualTo(new[] { "a" })); // input not mutated
        }

        // buy: rejects when already owned (checked before affordability)
        [Test]
        public void Buy_RejectsWhenOwned()
        {
            BuyResult r = Economy.Buy(1000, new[] { "b" }, "b", 80);
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Reason, Is.EqualTo("owned"));
        }

        // buy: rejects when broke
        [Test]
        public void Buy_RejectsWhenBroke()
        {
            BuyResult r = Economy.Buy(50, new string[0], "b", 80);
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Reason, Is.EqualTo("broke"));
        }

        // buy: defaults to empty wallet/owned when snapshot fields missing
        [Test]
        public void Buy_DefaultsWhenSnapshotFieldsMissing()
        {
            BuyResult r = Economy.Buy(0, null, "x", 0);
            Assert.That(r.Ok, Is.True);
            Assert.That(r.Wallet, Is.EqualTo(0));
            Assert.That(r.Owned.ToArray(), Is.EqualTo(new[] { "x" }));
        }

        [Test]
        public void ConstantsMatchExportedJson()
        {
            var j = (Dictionary<string, object?>)Json.Parse(File.ReadAllText(
                TestPaths.Locate("unity/Assets/Content/_json/economy.json")))!;
            void Eq(string key, double actual) =>
                Assert.That(actual, Is.EqualTo((double)j[key]!), key);
            Eq("STAR_PPS", Economy.StarPps);
            Eq("MAX_STARS", Economy.MaxStars);
            Eq("FINISH_FLAT", Economy.FinishFlat);
            Eq("FINISH_PER_STAR", Economy.FinishPerStar);
            Eq("FIRST_CLEAR_BONUS", Economy.FirstClearBonus);
            Eq("DDK_PPS", Economy.DdkPps);
            Eq("ONE_PPS_BONUS", Economy.OnePpsBonus);
            Eq("UNBROKEN_BONUS", Economy.UnbrokenBonus);
            Assert.That(Economy.CarGatingEnabled, Is.EqualTo((bool)j["CAR_GATING_ENABLED"]!));
            var cars = ((List<object?>)j["FREE_CARS"]!).Select(o => (string)o!).ToArray();
            Assert.That(Economy.FreeCars.ToArray(), Is.EqualTo(cars));
        }
    }
}
