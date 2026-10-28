#nullable enable
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    // Source: DesktopDrift/tests/car-stats.test.js @ 5556140
    [TestFixture]
    public class CarStatsTests
    {
        // driveForRatings round-trips for every integer rating 1..10
        [Test]
        public void DriveForRatings_RoundTripsForEveryIntegerRating()
        {
            for (int h = 1; h <= 10; h++)
                for (int a = 1; a <= 10; a++)
                    for (int s = 1; s <= 10; s++)
                    {
                        DriveProfile d = CarStats.DriveForRatings(handling: h, accel: a, speed: s);
                        Assert.That(CarStats.HandlingRating(d), Is.EqualTo((double)h), "handling " + h);
                        Assert.That(CarStats.AccRating(d), Is.EqualTo((double)a), "accel " + a);
                        Assert.That(CarStats.SpeedRating(d), Is.EqualTo((double)s), "speed " + s);
                    }
        }

        // plum 7/7/7 -> the expected drive params
        [Test]
        public void Plum777_ExpectedDriveParams()
        {
            DriveProfile d = CarStats.DriveForRatings(handling: 7, accel: 7, speed: 7);
            Assert.That(d.MaxSpeed, Is.EqualTo(457.0));
            Assert.That(d.Thrust, Is.EqualTo(630.0));
            Assert.That(d.Steer, Is.EqualTo(3.5));
            Assert.That(d.LowSpeedTurn, Is.EqualTo(0.35));
        }

        // matches the garage numbers for the legacy cars (Bismark 7/6/4, Panda 6/7/6)
        [Test]
        public void MatchesGarageNumbersForLegacyCars()
        {
            var bismark = new DriveProfile { MaxSpeed = 470, Thrust = 580, Steer = 2.2, LowSpeedTurn = 0.20 };
            Assert.That(CarStats.SpeedRating(bismark), Is.EqualTo(7.0));
            Assert.That(CarStats.AccRating(bismark), Is.EqualTo(6.0));
            Assert.That(CarStats.HandlingRating(bismark), Is.EqualTo(4.0));
            var panda = new DriveProfile { MaxSpeed = 410, Thrust = 620, Steer = 3.0, LowSpeedTurn = 0.30 };
            Assert.That(CarStats.SpeedRating(panda), Is.EqualTo(6.0));
            Assert.That(CarStats.AccRating(panda), Is.EqualTo(7.0));
            Assert.That(CarStats.HandlingRating(panda), Is.EqualTo(6.0));
        }
    }
}
