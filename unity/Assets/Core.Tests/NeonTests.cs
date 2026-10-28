#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    // Source: DesktopDrift/tests/neon.test.js @ 5556140
    [TestFixture]
    public class NeonTests
    {
        private static string[] Fill(string c) { return new[] { c, c, c, c, c, c }; }

        // descriptors: 6 zones; layouts + anims expose ids and colour counts
        [Test]
        public void Descriptors()
        {
            Assert.That(Neon.Zones, Is.EqualTo(6));
            Assert.That(Neon.Layouts.Select(l => l.Id), Is.EqualTo(new[] { "solid", "longitudinal", "front-mid-rear", "per-zone" }));
            Assert.That(Neon.Layouts.Select(l => l.Colours), Is.EqualTo(new[] { 1, 2, 3, 6 }));
            Assert.That(Neon.Anims.Select(a => a.Id), Is.EqualTo(new[] { "none", "pulse", "rainbow", "flow" }));
        }

        // layoutColours: solid paints every zone the first colour
        [Test]
        public void LayoutColours_SolidPaintsEveryZoneFirstColour()
        {
            Assert.That(Neon.LayoutColours("solid", new[] { "#111111" }), Is.EqualTo(Fill("#111111")));
        }

        // layoutColours: longitudinal splits left trio {0,4,5} vs right trio {1,2,3}
        [Test]
        public void LayoutColours_Longitudinal()
        {
            string a = "#aaaaaa", b = "#bbbbbb";
            var z = Neon.LayoutColours("longitudinal", new[] { a, b });
            Assert.That(new[] { z[0], z[4], z[5] }, Is.EqualTo(new[] { a, a, a }));
            Assert.That(new[] { z[1], z[2], z[3] }, Is.EqualTo(new[] { b, b, b }));
        }

        // layoutColours: front-mid-rear -> front {0,1} · sides {2,5} · rear {3,4}
        [Test]
        public void LayoutColours_FrontMidRear()
        {
            string f = "#f00000", s = "#00f000", r = "#0000f0";
            var z = Neon.LayoutColours("front-mid-rear", new[] { f, s, r });
            Assert.That(new[] { z[0], z[1] }, Is.EqualTo(new[] { f, f }));
            Assert.That(new[] { z[2], z[5] }, Is.EqualTo(new[] { s, s }));
            Assert.That(new[] { z[3], z[4] }, Is.EqualTo(new[] { r, r }));
        }

        // layoutColours: per-zone maps one colour per zone in order
        [Test]
        public void LayoutColours_PerZone()
        {
            var cols = new[] { "#000001", "#000002", "#000003", "#000004", "#000005", "#000006" };
            Assert.That(Neon.LayoutColours("per-zone", cols), Is.EqualTo(cols));
        }

        // layoutColours: too-few colours fall back to the last one (never undefined)
        [Test]
        public void LayoutColours_TooFewColoursFallBackToLast()
        {
            var z = Neon.LayoutColours("per-zone", new[] { "#123456", "#654321" });
            Assert.That(z.Count, Is.EqualTo(6));
            Assert.That(z.All(c => c != null), Is.True);
            Assert.That(z[5], Is.EqualTo("#654321"));
        }

        // layoutColours: empty/absent colours -> a safe default, unknown layout -> solid
        [Test]
        public void LayoutColours_EmptyAndUnknownLayout()
        {
            Assert.That(Neon.LayoutColours("solid", new string[0]).Count, Is.EqualTo(6));
            Assert.That(Neon.LayoutColours("solid", new string[0])[0].StartsWith("#"), Is.True);
            Assert.That(Neon.LayoutColours("bogus", new[] { "#abcabc" }), Is.EqualTo(Fill("#abcabc")));
        }

        // zoneColors: static returns 6 {color,intensity:1} matching the layout
        [Test]
        public void ZoneColors_Static()
        {
            var cfg = new NeonConfig
            {
                Layout = "per-zone", Anim = "none",
                Colors = new[] { "#010101", "#020202", "#030303", "#040404", "#050505", "#060606" },
            };
            var o = Neon.ZoneColors(cfg, 5);
            Assert.That(o.Count, Is.EqualTo(6));
            Assert.That(o.Select(z => z.Color), Is.EqualTo(new[] { "#010101", "#020202", "#030303", "#040404", "#050505", "#060606" }));
            Assert.That(o.All(z => z.Intensity == 1), Is.True);
        }

        // zoneColors: missing/partial config resolves safely (defaults to solid/static)
        [Test]
        public void ZoneColors_MissingConfig()
        {
            var o = Neon.ZoneColors(null, 0);
            Assert.That(o.Count, Is.EqualTo(6));
            Assert.That(o.All(z => z.Color != null && z.Intensity == 1), Is.True);
        }

        // zoneColors: pulse breathes intensity in [0,1], shared across zones; 0.5 at t=0
        [Test]
        public void ZoneColors_Pulse()
        {
            var cfg = new NeonConfig { Layout = "solid", Anim = "pulse", Colors = new[] { "#00ffff" }, Speed = 1 };
            var at0 = Neon.ZoneColors(cfg, 0);
            Assert.That(Math.Abs(at0[0].Intensity - 0.5) < 1e-9, Is.True);
            Assert.That(at0.All(z => z.Intensity == at0[0].Intensity), Is.True);
            Assert.That(at0.All(z => z.Color == "#00ffff"), Is.True);
            double min = 1, max = 0;
            for (int i = 0; i < 60; i++)
            {
                double v = Neon.ZoneColors(cfg, i / 20.0)[0].Intensity;
                min = Math.Min(min, v); max = Math.Max(max, v);
            }
            Assert.That(min < 0.1 && max > 0.9, Is.True, "pulse should span most of [0,1] (min " + min + ", max " + max + ")");
        }

        // zoneColors: rainbow spreads distinct hues around the ring and rotates over time
        [Test]
        public void ZoneColors_Rainbow()
        {
            var cfg = new NeonConfig { Layout = "solid", Anim = "rainbow", Colors = new[] { "#ffffff" }, Speed = 1 };
            var a = Neon.ZoneColors(cfg, 0);
            Assert.That(a.All(z => z.Color.StartsWith("hsl(")), Is.True);
            Assert.That(a.Select(z => z.Color).Distinct().Count(), Is.EqualTo(6));
            Assert.That(a[1].Color, Is.EqualTo("hsl(60 100% 55%)"));
            var b = Neon.ZoneColors(cfg, 1);
            Assert.That(b[0].Color, Is.Not.EqualTo(a[0].Color));
        }

        // zoneColors: flow rotates the mapped colours around the ring and is periodic
        [Test]
        public void ZoneColors_Flow()
        {
            var cfg = new NeonConfig
            {
                Layout = "per-zone", Anim = "flow", Speed = 1,
                Colors = new[] { "#ff0000", "#ffaa00", "#ffff00", "#00ff00", "#0000ff", "#ff00ff" },
            };
            var z0 = Neon.ZoneColors(cfg, 0);
            var later = Neon.ZoneColors(cfg, 1);
            Assert.That(later[0].Color, Is.Not.EqualTo(z0[0].Color));
            double period = 1 / 0.22;
            var zP = Neon.ZoneColors(cfg, period);
            for (int i = 0; i < 6; i++) Assert.That(zP[i].Color, Is.EqualTo(z0[i].Color));
        }

        // defaultNeon: solid/static single colour
        [Test]
        public void DefaultNeon_SolidStatic()
        {
            var n = Neon.DefaultNeon("#39FF14");
            Assert.That(n.Layout, Is.EqualTo("solid"));
            Assert.That(n.Anim, Is.EqualTo("none"));
            Assert.That(n.Colors, Is.EqualTo(new[] { "#39FF14" }));
            Assert.That(n.Speed, Is.EqualTo(1.0));
        }
    }
}
