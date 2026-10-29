// Parsing of the 2D theme colour strings (data/tracks.json "theme": background, table, tableEdge, track, cone, skid, checkpoint).
// js/render.js reads them as CSS colours: hex (#rgb / #rrggbb) and rgb()/rgba(); line 238 keeps only the RGB of `skid`
// (TH.skid.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)/)); the fixed overlay colours of the renderer (start flag cells
// rgba(255,255,255,0.92) / rgba(0,0,0,0.82), next-checkpoint ring rgba(0,0,0,0.55) + #7dd4ff) use the same grammar.
#nullable enable
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DesktopDrift.Core
{
    public readonly struct ThemeColor
    {
        public readonly double R;
        public readonly double G;
        public readonly double B;
        public readonly double A;

        public ThemeColor(double r, double g, double b, double a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        private static readonly Regex Rgb = new Regex(
            @"^rgb\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex Rgba = new Regex(
            @"^rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d*\.?\d+)\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static ThemeColor Parse(string s)
        {
            if (s == null) throw new FormatException("Invalid theme colour: null");
            string t = s.Trim();
            if (t.Length > 0 && t[0] == '#')
            {
                string h = t.Substring(1);
                if (h.Length == 3 && IsHex(h))
                {
                    return new ThemeColor(
                        Hex(h.Substring(0, 1)) * 17 / 255.0, Hex(h.Substring(1, 1)) * 17 / 255.0, Hex(h.Substring(2, 1)) * 17 / 255.0, 1.0);
                }
                if (h.Length == 6 && IsHex(h))
                {
                    return new ThemeColor(
                        Hex(h.Substring(0, 2)) / 255.0, Hex(h.Substring(2, 2)) / 255.0, Hex(h.Substring(4, 2)) / 255.0, 1.0);
                }
                throw new FormatException("Invalid theme colour: '" + s + "'");
            }
            Match m = Rgb.Match(t);
            if (m.Success) return FromParts(s, m, 1.0);
            m = Rgba.Match(t);
            if (m.Success)
            {
                double a = double.Parse(m.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (a > 1.0) throw new FormatException("Invalid theme colour (alpha > 1): '" + s + "'");
                return FromParts(s, m, a);
            }
            throw new FormatException("Invalid theme colour: '" + s + "'");
        }

        // Standard source-over onto an opaque bottom: c = top.c * top.A + bottom.c * (1 - top.A); result alpha 1.
        public static ThemeColor Over(ThemeColor top, ThemeColor bottom)
        {
            double a = top.A;
            return new ThemeColor(
                top.R * a + bottom.R * (1.0 - a),
                top.G * a + bottom.G * (1.0 - a),
                top.B * a + bottom.B * (1.0 - a),
                1.0);
        }

        private static ThemeColor FromParts(string s, Match m, double a)
        {
            int r = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            int g = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            int b = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            if (r > 255 || g > 255 || b > 255) throw new FormatException("Invalid theme colour (channel > 255): '" + s + "'");
            return new ThemeColor(r / 255.0, g / 255.0, b / 255.0, a);
        }

        private static bool IsHex(string h)
        {
            for (int i = 0; i < h.Length; i++)
            {
                char c = h[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!ok) return false;
            }
            return true;
        }

        private static int Hex(string h)
        {
            return int.Parse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
    }
}
