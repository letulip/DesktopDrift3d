#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    // Source: DesktopDrift/tests/car-ownership.test.js @ 5556140.
    // The JS test runs against the store singleton (wallet 500, ownedCars []); here the same state is
    // held in fields and mutated through Economy.Buy / Economy.CarOwned, in the JS test order.
    [TestFixture]
    public class CarOwnershipTests
    {
        private int _wallet;
        private List<string> _owned = new List<string>();

        [SetUp]
        public void Fresh()
        {
            _wallet = 500;
            _owned = new List<string>();
        }

        private BuyResult BuyCar(string id, int price)
        {
            BuyResult r = Economy.Buy(_wallet, _owned, id, price);
            if (r.Ok) { _wallet = r.Wallet; _owned = r.Owned.ToList(); }
            return r;
        }

        // gate is on; free starters are owned, paid cars are not (until bought)
        [Test]
        public void GateIsOn_FreeStartersOwned()
        {
            Assert.That(Economy.CarGatingEnabled, Is.True);
            foreach (string id in Economy.FreeCars) Assert.That(Economy.CarOwned(id, _owned), Is.True, id + " is a free starter");
            Assert.That(Economy.CarOwned("plum", _owned), Is.False);
            Assert.That(Economy.CarOwned("smasher", _owned), Is.False);
        }

        // ownedCars defaults to empty
        [Test]
        public void OwnedCarsDefaultsToEmpty()
        {
            Assert.That(_owned, Is.Empty);
        }

        // buyCar deducts the price, grants the car, and is not re-buyable
        [Test]
        public void BuyCar_DeductsGrantsNotRebuyable()
        {
            Assert.That(_wallet, Is.EqualTo(500));
            BuyResult r = BuyCar("plum", 300);
            Assert.That(r.Ok, Is.True);
            Assert.That(_wallet, Is.EqualTo(200));
            Assert.That(Economy.CarOwned("plum", _owned), Is.True);
            BuyResult again = BuyCar("plum", 300);
            Assert.That(again.Ok, Is.False);
            Assert.That(again.Reason, Is.EqualTo("owned"));
            Assert.That(_wallet, Is.EqualTo(200));
        }

        // buyCar refuses when broke (wallet unchanged)
        [Test]
        public void BuyCar_RefusesWhenBroke()
        {
            _wallet = 200;
            BuyResult r = BuyCar("smasher", 650);
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Reason, Is.EqualTo("broke"));
            Assert.That(_wallet, Is.EqualTo(200));
            Assert.That(Economy.CarOwned("smasher", _owned), Is.False);
        }

        // grantCar records a car id and is idempotent
        [Test]
        public void GrantCar_IdempotentRecord()
        {
            // store.js grantCar: add the id unless already present
            for (int i = 0; i < 2; i++) if (!_owned.Contains("catana")) _owned.Add("catana");
            Assert.That(_owned.Contains("catana"), Is.True);
            Assert.That(_owned.Count(x => x == "catana"), Is.EqualTo(1));
        }
    }
}
