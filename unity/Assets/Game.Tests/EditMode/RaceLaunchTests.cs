#nullable enable
using DesktopDrift.Core;
using DesktopDrift.Game;
using NUnit.Framework;

namespace DesktopDrift.Game.EditModeTests
{
    public class RaceLaunchTests
    {
        [TearDown]
        public void TearDown()
        {
            RaceLaunch.Mode = RaceMode.TimeAttack;
            RaceLaunch.Laps = null;
            RaceLaunch.CarIndex = 0;
            RaceLaunch.AutoStart = true;
        }

        static RaceOptions Build(RaceMode m, out ContentDescriptor content)
        {
            content = new ContentDescriptor();
            RaceLaunch.Mode = m;
            return RaceLaunch.BuildOptions(content, 7);
        }

        [Test]
        public void TimeAttack_Options()
        {
            var o = Build(RaceMode.TimeAttack, out var c);
            Assert.AreEqual(3, o.Laps); Assert.IsFalse(o.Zen); Assert.IsTrue(o.InitItems);
            Assert.IsFalse(o.Stock); Assert.IsFalse(o.Reversed); Assert.AreEqual(7, o.CarCount);
            Assert.AreSame(c, o.Content);
        }

        [Test]
        public void Sandbox_Options()
        {
            var o = Build(RaceMode.Sandbox, out var c);
            Assert.AreEqual(0, o.Laps); Assert.IsFalse(o.Zen); Assert.IsFalse(o.InitItems);
            Assert.IsTrue(o.Stock); Assert.IsFalse(o.Reversed); Assert.AreEqual(7, o.CarCount);
            Assert.AreSame(c, o.Content);
        }

        [Test]
        public void Zen_Options()
        {
            var o = Build(RaceMode.Zen, out var c);
            Assert.AreEqual(3, o.Laps); Assert.IsTrue(o.Zen); Assert.IsTrue(o.InitItems);
            Assert.IsFalse(o.Stock); Assert.IsFalse(o.Reversed); Assert.AreEqual(7, o.CarCount);
            Assert.AreSame(c, o.Content);
        }

        [Test]
        public void LapsOverride_AppliesExceptSandbox()
        {
            RaceLaunch.Laps = 5;
            Assert.AreEqual(5, Build(RaceMode.TimeAttack, out _).Laps);
            Assert.AreEqual(5, Build(RaceMode.Zen, out _).Laps);
            Assert.AreEqual(0, Build(RaceMode.Sandbox, out _).Laps);
        }

        [Test]
        public void ModeTitle_Strings()
        {
            Assert.AreEqual("Time Attack", RaceLaunch.ModeTitle(RaceMode.TimeAttack));
            Assert.AreEqual("Sandbox", RaceLaunch.ModeTitle(RaceMode.Sandbox));
            Assert.AreEqual("Zen Drift", RaceLaunch.ModeTitle(RaceMode.Zen));
        }
    }
}
