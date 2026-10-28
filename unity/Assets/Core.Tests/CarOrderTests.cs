#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    // Source: DesktopDrift/tests/car-order.test.js @ 5556140. CARS = a(0) b(1) c(2) d(3); a car is its index.
    [TestFixture]
    public class CarOrderTests
    {
        private static readonly string[] Ids = { "a", "b", "c", "d" };

        // owned cars come first, each group in registry order
        [Test]
        public void OwnedCarsComeFirst()
        {
            var owned = new HashSet<string> { "b", "d" };
            Assert.That(CarOrder.OwnedFirstOrder(4, i => owned.Contains(Ids[i])), Is.EqualTo(new[] { 1, 3, 0, 2 }));
        }

        // all owned -> identity order
        [Test]
        public void AllOwned_IdentityOrder()
        {
            Assert.That(CarOrder.OwnedFirstOrder(4, i => true), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        // none owned -> identity order
        [Test]
        public void NoneOwned_IdentityOrder()
        {
            Assert.That(CarOrder.OwnedFirstOrder(4, i => false), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        // result is a permutation (every index once)
        [Test]
        public void ResultIsPermutation()
        {
            var order = CarOrder.OwnedFirstOrder(4, i => Ids[i] == "c");
            Assert.That(order[0], Is.EqualTo(2));
            Assert.That(order.OrderBy(x => x).ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }
    }
}
