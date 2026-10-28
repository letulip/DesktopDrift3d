#nullable enable
using System.Collections.Generic;
using DesktopDrift.Core;
using DesktopDrift.Game;
using NUnit.Framework;

namespace DesktopDrift.Game.EditModeTests
{
    public class HudFormatTests
    {
        [Test] public void LapTime_TwoDecimals() => Assert.AreEqual("12.35", HudFormat.LapTime(12.346));
        [Test] public void LapOpt_NullIsDash() => Assert.AreEqual("—", HudFormat.LapOpt(null));
        [Test] public void LapOpt_Value() => Assert.AreEqual("12.30 s", HudFormat.LapOpt(12.3));
        [Test] public void Speed_Kmh() => Assert.AreEqual("23.0", HudFormat.Speed(1000, false));
        [Test] public void Speed_Mph() => Assert.AreEqual("14.3", HudFormat.Speed(1000, true));
        [Test] public void Score_Rounds() => Assert.AreEqual("1235", HudFormat.Score(1234.5));
        [Test] public void Combo_Format() => Assert.AreEqual("+12   ×2.0", HudFormat.Combo(12.4, 2.0));

        [Test]
        public void LapScores_NewestFirst()
        {
            var l = new List<RaceState.LapScore>
            {
                new RaceState.LapScore { N = 1, Pts = 100, T = 20 },
                new RaceState.LapScore { N = 2, Pts = 250, T = 19 },
            };
            Assert.AreEqual("lap 2: +250\nlap 1: +100", HudFormat.LapScores(l));
            Assert.AreEqual("", HudFormat.LapScores(new List<RaceState.LapScore>()));
        }

        [Test]
        public void Count_Digit_Go_Off()
        {
            var a = HudFormat.Count(2.2, 0);
            Assert.AreEqual("3", a.Text); Assert.AreEqual("#fff", a.Color); Assert.AreEqual(1.0, a.Opacity);
            var b = HudFormat.Count(0, 0.2);
            Assert.AreEqual("GO!", b.Text); Assert.AreEqual("#9dff8f", b.Color); Assert.AreEqual(0.5, b.Opacity, 1e-9);
            Assert.AreEqual(1.0, HudFormat.Count(0, 2).Opacity);
            Assert.AreEqual(0.0, HudFormat.Count(0, 0).Opacity);
        }

        [Test]
        public void FlashOpacity_ClampedAtZero()
        {
            Assert.AreEqual(0.0, HudFormat.FlashOpacity(-1));
            Assert.AreEqual(0.5, HudFormat.FlashOpacity(0.45), 1e-9);
        }

        [Test] public void ResultPps() { Assert.AreEqual("342 PPS", HudFormat.ResultPps(341.6)); Assert.AreEqual("1,234 PPS", HudFormat.ResultPps(1234)); }
        [Test] public void ResultTotal() => Assert.AreEqual("Total: 5000 · 61.2 s", HudFormat.ResultTotal(5000, 61.23));

        [Test]
        public void Stars_FloorHundredsCappedAtFive()
        {
            Assert.AreEqual(0, HudFormat.Stars(99.4));
            Assert.AreEqual(3, HudFormat.Stars(349));
            Assert.AreEqual(5, HudFormat.Stars(9000));
        }

        [Test]
        public void TireParts_OnlyNonZero()
        {
            var t = new RaceResult.TireBreakdown { Pickup = 3, Finish = 4 };
            var r = HudFormat.TireParts(t);
            Assert.AreEqual("🛞 +7 tires", r.Title);
            Assert.AreEqual("pickups +3 · finish +4", r.Parts);
            var all = HudFormat.TireParts(new RaceResult.TireBreakdown { Pickup = 1, Cap = 2, CleanSweep = 3, FirstClear = 4, Finish = 5, Trophy = 6, Unbroken = 7 });
            Assert.AreEqual("pickups +1 · cola cap +2 · clean sweep +3 · first clear +4 · finish +5 · 🏅 trophy +6 · ♾️ unbroken +7", all.Parts);
            Assert.AreEqual(("", ""), HudFormat.TireParts(new RaceResult.TireBreakdown()));
        }

        [Test]
        public void LapRow_Format()
        {
            Assert.AreEqual(("Lap 2", "19.50 s", "+250"), HudFormat.LapRow(new RaceState.LapScore { N = 2, Pts = 250, T = 19.5 }));
            Assert.AreEqual("—", HudFormat.LapRow(new RaceState.LapScore { N = 1, Pts = 0, T = 0 }).Time);
        }

        [Test]
        public void BestLap_Format()
        {
            Assert.AreEqual("Best lap  19.50 s", HudFormat.BestLap(19.5));
            Assert.AreEqual("", HudFormat.BestLap(null));
        }
    }
}
