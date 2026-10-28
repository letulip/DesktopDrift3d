// SPEC-02 step 3: CarCatalog (cars.json + car-registry.json + config.json) and ContentJson against the files.
#nullable enable
using System.IO;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    public class CarCatalogTests
    {
        private static string Read(string name) => File.ReadAllText(TestPaths.Locate("unity/Assets/Content/_json/" + name));

        private static CarCatalog Catalog() => CarCatalog.FromJson(Read("cars.json"), Read("car-registry.json"));

        [Test]
        public void ParsesEightCars()
        {
            Assert.AreEqual(8, Catalog().Cars.Count);
        }

        [Test]
        public void FirstCarIsBismark()
        {
            CarDefinition c = Catalog().Cars[0];
            Assert.AreEqual("bismark", c.Id);
            Assert.AreEqual(82, c.Len);
            Assert.AreEqual(30.220657276995304, c.Wid);
            Assert.AreEqual(580, c.Drive.Thrust);
            Assert.AreEqual(470, c.Drive.MaxSpeed);
            Assert.IsNull(c.Price);
        }

        [Test]
        public void PlumHasPriceAndRatings()
        {
            CarDefinition c = Catalog().Cars[2];
            Assert.AreEqual("plum", c.Id);
            Assert.AreEqual(250, c.Price);
            Assert.IsNotNull(c.Ratings);
            Assert.AreEqual(7, c.Ratings!.Accel);
            Assert.AreEqual(7, c.Ratings.Handling);
            Assert.AreEqual(7, c.Ratings.Speed);
        }

        [Test]
        public void ByIndexClampsLikeTheEngine()
        {
            CarCatalog cat = Catalog();
            Assert.AreSame(cat.Cars[0], cat.ByIndex(-1));
            Assert.AreSame(cat.Cars[7], cat.ByIndex(99));
            Assert.AreEqual("plum", cat.ByIndex(2).Id);
        }

        [Test]
        public void PhysicsFromConfig()
        {
            PhysicsConstants k = CarCatalog.PhysicsFromConfig(Read("config.json"));
            Assert.AreEqual(120, k.PhysHz);
            Assert.AreEqual(0.7, k.GripWobble);
            Assert.AreEqual(0.16, k.SteerWobble);
        }

        [Test]
        public void ContentJsonMatchesReplayContent()
        {
            ContentDescriptor built = ContentJson.Build(Read("tracks.json"), Read("shop.json"), Read("cars.json"));
            ContentDescriptor reference = RaceReplayTests.Content();
            Assert.AreEqual(reference.ForwardIds.Count, built.ForwardIds.Count);
            Assert.AreEqual(reference.Cars.Count, built.Cars.Count);
            CollectionAssert.AreEqual(reference.ForwardIds, built.ForwardIds);
            CollectionAssert.AreEqual(reference.Cars, built.Cars);
        }
    }
}
