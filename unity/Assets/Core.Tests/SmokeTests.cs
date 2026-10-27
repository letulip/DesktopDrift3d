// SPEC-00 smoke test proving the dotnet/Unity dual compile path.
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    public class SmokeTests
    {
        [Test]
        public void OnePlusOne()
        {
            Assert.AreEqual(2, 1 + 1);
        }
    }
}
