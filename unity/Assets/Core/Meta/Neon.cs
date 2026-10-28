// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/neon.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DesktopDrift.Core
{
    public static class Neon
    {
        public const int Zones = 6; // neon.js: NEON_ZONES

        public static readonly IReadOnlyList<NeonLayout> Layouts = new[]
        {
            new NeonLayout("solid", "Solid", 1),
            new NeonLayout("longitudinal", "Left / Right", 2),
            new NeonLayout("front-mid-rear", "Front / Mid / Rear", 3),
            new NeonLayout("per-zone", "Per-zone", 6),
        };

        public static readonly IReadOnlyList<NeonAnim> Anims = new[]
        {
            new NeonAnim("none", "Static"),
            new NeonAnim("pulse", "Pulse"),
            new NeonAnim("rainbow", "Rainbow"),
            new NeonAnim("flow", "Circular Flow"),
        };

        private const double PulseRate = 2.4;   // neon.js: PULSE_RATE
        private const double RainbowDeg = 70;   // neon.js: RAINBOW_DEG
        private const double FlowRev = 0.22;    // neon.js: FLOW_REV

        public const string DefaultColour = "#39FF14"; // neon.js: DEFAULT_COLOUR

        // neon.js: ZONE_MAP
        private static readonly Dictionary<string, int[]> ZoneMap = new Dictionary<string, int[]>
        {
            { "solid", new[] { 0, 0, 0, 0, 0, 0 } },
            { "longitudinal", new[] { 0, 1, 1, 1, 0, 0 } },
            { "front-mid-rear", new[] { 0, 0, 1, 2, 2, 1 } },
            { "per-zone", new[] { 0, 1, 2, 3, 4, 5 } },
        };

        private static IReadOnlyList<string> Colours(IReadOnlyList<string>? cols)
        {
            return cols != null && cols.Count > 0 ? cols : new[] { DefaultColour };
        }

        public static List<string> LayoutColours(string layout, IReadOnlyList<string>? colours)
        {
            int[] map;
            if (!ZoneMap.TryGetValue(layout, out map!)) map = ZoneMap["solid"];
            IReadOnlyList<string> c = Colours(colours);
            var result = new List<string>();
            foreach (int ci in map) result.Add(ci < c.Count ? c[ci] : c[c.Count - 1]);
            return result;
        }

        private static int HexDigit(char ch)
        {
            if (ch >= '0' && ch <= '9') return ch - '0';
            if (ch >= 'a' && ch <= 'f') return ch - 'a' + 10;
            if (ch >= 'A' && ch <= 'F') return ch - 'A' + 10;
            return -1;
        }

        // neon.js: _hex
        private static double[] Hex(string h)
        {
            int hash = h.IndexOf('#');
            string s = hash >= 0 ? h.Remove(hash, 1) : h; // JS replace('#','') removes the first match only
            string n;
            if (s.Length == 3)
            {
                n = "";
                foreach (char x in s) n += x.ToString() + x.ToString();
            }
            else
            {
                string p = s.PadRight(6, '0');
                n = p.Substring(0, 6);
            }
            // parseInt(n, 16) || 0: longest valid hex prefix, none => NaN => 0
            long v = 0;
            foreach (char ch in n)
            {
                int d = HexDigit(ch);
                if (d < 0) break;
                v = v * 16 + d;
            }
            return new double[] { (v >> 16) & 255, (v >> 8) & 255, v & 255 };
        }

        private static string Num(double x)
        {
            return x.ToString("R", CultureInfo.InvariantCulture);
        }

        // neon.js: _rgb
        private static string Rgb(double r, double g, double b)
        {
            return "rgb(" + Num(CoreMath.JsRound(r)) + "," + Num(CoreMath.JsRound(g)) + "," + Num(CoreMath.JsRound(b)) + ")";
        }

        // neon.js: _mix
        private static string Mix(string c1, string c2, double t)
        {
            double[] a = Hex(c1), b = Hex(c2);
            return Rgb(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t);
        }

        // neon.js: _ringSample
        private static string RingSample(IReadOnlyList<string> bas, double pos)
        {
            double p = (((pos % 1) + 1) % 1) * 6; // 0..6
            double i = Math.Floor(p);
            int ii = (int)i;
            return Mix(bas[ii % 6], bas[(ii + 1) % 6], p - i);
        }

        public static List<ZoneColor> ZoneColors(NeonConfig? neon, double t = 0)
        {
            string layout = neon != null ? neon.Layout : "solid";
            string anim = neon != null ? neon.Anim : "none";
            double speed = neon != null ? neon.Speed : 1;
            List<string> bas = LayoutColours(layout, neon != null ? neon.Colors : null);
            var result = new List<ZoneColor>();

            if (anim == "pulse")
            {
                double intensity = 0.5 + 0.5 * Math.Sin(t * speed * PulseRate);
                foreach (string color in bas) result.Add(new ZoneColor { Color = color, Intensity = intensity });
                return result;
            }
            if (anim == "rainbow")
            {
                double spin = t * speed * RainbowDeg;
                for (int i = 0; i < bas.Count; i++)
                {
                    double h = (((i * 60 + spin) % 360) + 360) % 360;
                    result.Add(new ZoneColor { Color = "hsl(" + Num(h) + " 100% 55%)", Intensity = 1 });
                }
                return result;
            }
            if (anim == "flow")
            {
                double phase = t * speed * FlowRev;
                for (int i = 0; i < bas.Count; i++)
                    result.Add(new ZoneColor { Color = RingSample(bas, i / 6.0 - phase), Intensity = 1 });
                return result;
            }
            foreach (string color in bas) result.Add(new ZoneColor { Color = color, Intensity = 1 });
            return result;
        }

        public static NeonConfig DefaultNeon(string colour = DefaultColour)
        {
            return new NeonConfig { Layout = "solid", Anim = "none", Colors = new[] { colour }, Speed = 1 };
        }
    }
}
