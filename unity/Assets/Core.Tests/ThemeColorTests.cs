// Gate for ThemeColor (js/render.js theme strings): every theme string of tracks.json parses; spot values; invalid input throws.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    [TestFixture]
    public class ThemeColorTests
    {
        private const double Eps = 1e-12;

        private static void Eq(ThemeColor c, double r, double g, double b, double a)
        {
            Assert.That(Math.Abs(c.R - r) <= Eps && Math.Abs(c.G - g) <= Eps && Math.Abs(c.B - b) <= Eps && Math.Abs(c.A - a) <= Eps,
                "got (" + c.R + ", " + c.G + ", " + c.B + ", " + c.A + ") expected (" + r + ", " + g + ", " + b + ", " + a + ")");
        }

        [Test]
        public void EveryThemeStringOfEveryTrackParses()
        {
            var doc = (Dictionary<string, object?>)Json.Parse(File.ReadAllText(TestPaths.Locate("unity/Assets/Content/_json/tracks.json")))!;
            var tracks = (List<object?>)doc["TRACKS"]!;
            Assert.That(tracks.Count, Is.GreaterThan(0));
            int parsed = 0;
            foreach (object? t in tracks)
            {
                var theme = (Dictionary<string, object?>)((Dictionary<string, object?>)t!)["theme"]!;
                Assert.That(theme.Count, Is.GreaterThan(0));
                foreach (KeyValuePair<string, object?> kv in theme)
                {
                    ThemeColor c = ThemeColor.Parse((string)kv.Value!);
                    Assert.That(c.R >= 0 && c.R <= 1 && c.G >= 0 && c.G <= 1 && c.B >= 0 && c.B <= 1 && c.A >= 0 && c.A <= 1, kv.Key);
                    parsed++;
                }
            }
            Assert.That(parsed, Is.GreaterThanOrEqualTo(tracks.Count * 7));
        }

        [Test]
        public void SpotValues()
        {
            Eq(ThemeColor.Parse("#14130e"), 20 / 255.0, 19 / 255.0, 14 / 255.0, 1.0);
            Eq(ThemeColor.Parse("#fff"), 1.0, 1.0, 1.0, 1.0);
            Eq(ThemeColor.Parse("#7dd4ff"), 125 / 255.0, 212 / 255.0, 1.0, 1.0);
            Eq(ThemeColor.Parse("rgba(10,16,10,0.5)"), 10 / 255.0, 16 / 255.0, 10 / 255.0, 0.5);
            Eq(ThemeColor.Parse("rgba( 255 , 255, 255 , 0.92 )"), 1.0, 1.0, 1.0, 0.92);
            Eq(ThemeColor.Parse("rgb(1,2,3)"), 1 / 255.0, 2 / 255.0, 3 / 255.0, 1.0);
        }

        [TestCase("")]
        [TestCase("red")]
        [TestCase("#12")]
        [TestCase("#12345")]
        [TestCase("#gggggg")]
        [TestCase("rgb(1,2)")]
        [TestCase("rgb(256,0,0)")]
        [TestCase("rgba(1,2,3)")]
        [TestCase("rgba(1,2,3,1.5)")]
        [TestCase("rgb(1,2,3,4)")]
        public void InvalidStringsThrow(string s)
        {
            FormatException ex = Assert.Throws<FormatException>(() => ThemeColor.Parse(s))!;
            Assert.That(ex.Message, Does.Contain("'" + s + "'"));
        }

        [Test]
        public void OverIsSourceOver()
        {
            // white 50% over black = mid grey, alpha 1
            Eq(ThemeColor.Over(ThemeColor.Parse("rgba(255,255,255,0.5)"), ThemeColor.Parse("#000")), 0.5, 0.5, 0.5, 1.0);
            // skid rgba(10,16,10,0.5) over table #2f4034
            Eq(ThemeColor.Over(ThemeColor.Parse("rgba(10,16,10,0.5)"), ThemeColor.Parse("#2f4034")),
                (10 / 255.0 + 47 / 255.0) / 2, (16 / 255.0 + 64 / 255.0) / 2, (10 / 255.0 + 52 / 255.0) / 2, 1.0);
            // opaque top wins
            Eq(ThemeColor.Over(ThemeColor.Parse("#7dd4ff"), ThemeColor.Parse("#000")), 125 / 255.0, 212 / 255.0, 1.0, 1.0);
        }
    }
}
