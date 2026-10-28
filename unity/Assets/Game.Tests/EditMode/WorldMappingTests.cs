using NUnit.Framework;
using UnityEngine;

namespace DesktopDrift.Game.Tests
{
    public class WorldMappingTests
    {
        [TestCase(0.0)]
        [TestCase(System.Math.PI / 2)]
        [TestCase(System.Math.PI)]
        [TestCase(-System.Math.PI / 2)]
        [TestCase(0.7)]
        [TestCase(2.3)]
        public void YawForwardMatches2DHeading(double a)
        {
            Vector3 f = WorldMapping.Yaw(a) * Vector3.forward;
            Assert.AreEqual((float)System.Math.Cos(a), f.x, 1e-5f);
            Assert.AreEqual(0f, f.y, 1e-5f);
            Assert.AreEqual((float)-System.Math.Sin(a), f.z, 1e-5f);
        }

        [Test]
        public void ToWorldFlipsYIntoZAndScalesToMeters()
        {
            Assert.AreEqual(new Vector3(1f, 0f, -0.5f), WorldMapping.ToWorld(1000, 500));
        }

        [Test]
        public void UnitsToMetersIsOneMillimeter()
        {
            Assert.AreEqual(0.001f, WorldMapping.UnitsToMeters);
        }
    }
}
