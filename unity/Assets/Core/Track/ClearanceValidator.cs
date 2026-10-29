// Verbatim port of DesktopDrift/tools/check-item-clearance.js @ 5556140 (ported for the 3D port 2026-10-29).
// Reproduces the script's stdout line by line and its exit code, INCLUDING the 2D game's own verdict
// (workbench: ITEM_COMB overlaps the track, exit 1). Do not "fix" the data here – the SVG of the 2D game is the owner's.
// Own tokenizer / point loop on purpose (not TrackUtil.ParseSvgPath): the script has its own, and parity is to the script.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace DesktopDrift.Core
{
    public static class ClearanceValidator
    {
        public sealed class Result
        {
            public string RawId = "";
            public double Gx, Gy, Dist, Need, Margin, R;
            public bool Fallback;
        }

        public sealed class Report
        {
            public List<Result> Results = new List<Result>();
            public List<string> Lines = new List<string>();
            public int ExitCode;
        }

        private const double Scale = 0.25;
        private const double TrackHalf = 100;

        private static readonly Regex ViewBoxRe = new Regex("viewBox=\"0 0 ([\\d.]+) ([\\d.]+)\"", RegexOptions.ECMAScript);
        private static readonly Regex PathRe0 = new Regex("id=\"track_path\"[^>]*d=\"([^\"]+)\"", RegexOptions.ECMAScript);
        private static readonly Regex TokenRe = new Regex("[MLHVZmlhvz]|[-+]?[\\d]*\\.?[\\d]+(?:[eE][-+]?[\\d]+)?", RegexOptions.ECMAScript);
        private static readonly Regex CmdRe = new Regex("[MLHVZmlhvz]", RegexOptions.ECMAScript);
        private static readonly Regex LineRe = new Regex("<line\\s+id=\"(ITEM_[^\"]+)\"[^/]*x1=\"([^\"]+)\"\\s*y1=\"([^\"]+)\"\\s*x2=\"([^\"]+)\"\\s*y2=\"([^\"]+)\"", RegexOptions.ECMAScript);
        private static readonly Regex PathRe = new Regex("<path\\s+id=\"(ITEM_[^\"]+)\"[^/]*d=\"M\\s*([\\d.]+)\\s+([\\d.]+)\\s*L\\s*([\\d.]+)\\s+([\\d.]+)\"", RegexOptions.ECMAScript);
        private static readonly Regex FloatPrefixRe = new Regex("^[ \\t\\n\\r\\f\\v]*([-+]?(?:Infinity|(?:[0-9]+\\.?[0-9]*|\\.[0-9]+)(?:[eE][-+]?[0-9]+)?))", RegexOptions.ECMAScript);

        // JS parseFloat: longest numeric prefix, NaN when none.
        private static double ParseFloat(string s)
        {
            Match m = FloatPrefixRe.Match(s);
            if (!m.Success) return double.NaN;
            string t = m.Groups[1].Value;
            if (t.EndsWith("Infinity", StringComparison.Ordinal)) return t[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
            return double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        // JS unary plus on a token (a number token from TokenRe); undefined (past the end) => NaN.
        private static double ToNumber(string[] tokens, int i)
        {
            if (i >= tokens.Length) return double.NaN;
            return double.Parse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        // JS Number -> string for the values this script interpolates (viewBox halves, r + 100): shortest round-trip form.
        private static string Num(double x)
        {
            if (double.IsNaN(x)) return "NaN";
            if (double.IsPositiveInfinity(x)) return "Infinity";
            if (double.IsNegativeInfinity(x)) return "-Infinity";
            if (x == 0) return "0";
            return x.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Fixed0(double x) { return CoreMath.JsToFixed(x, 0); }

        public static Report Check(string svgText, string svgPathForHeader, IReadOnlyDictionary<string, double> itemRadii)
        {
            var report = new Report();
            Match vb = ViewBoxRe.Match(svgText);
            if (!vb.Success) { report.ExitCode = 1; return report; }   // script: stderr 'No viewBox found in SVG', exit 1, empty stdout
            double svgCx = ParseFloat(vb.Groups[1].Value) / 2;
            double svgCy = ParseFloat(vb.Groups[2].Value) / 2;

            Match pm = PathRe0.Match(svgText);
            if (!pm.Success) { report.ExitCode = 1; return report; }   // script: stderr 'No track_path element found', exit 1

            var ptsX = new List<double>();
            var ptsY = new List<double>();
            double cx = 0, cy = 0;
            string cmd = "";
            string[] tokens = TokenRe.Matches(pm.Groups[1].Value).Cast<Match>().Select(m => m.Value).ToArray();
            int i = 0;
            while (i < tokens.Length)
            {
                string t = tokens[i];
                if (CmdRe.IsMatch(t)) { cmd = t; i++; continue; }
                if (cmd == "M" || cmd == "L") { cx = ToNumber(tokens, i); cy = ToNumber(tokens, i + 1); i += 2; ptsX.Add(cx); ptsY.Add(cy); }
                else if (cmd == "H") { cx = ToNumber(tokens, i); i++; ptsX.Add(cx); ptsY.Add(cy); }
                else if (cmd == "V") { cy = ToNumber(tokens, i); i++; ptsX.Add(cx); ptsY.Add(cy); }
                else i++;
            }
            int n = ptsX.Count;
            var tx = new double[n];
            var ty = new double[n];
            for (int k = 0; k < n; k++)
            {
                tx[k] = (ptsX[k] - svgCx) * Scale;
                ty[k] = -(ptsY[k] - svgCy) * Scale;
            }

            double DistToTrack(double gx, double gy)
            {
                double minD = double.PositiveInfinity;
                for (int a = 0; a < n; a++)
                {
                    int b = (a + 1) % n;
                    double dx = tx[b] - tx[a], dy = ty[b] - ty[a];
                    double len2 = dx * dx + dy * dy;
                    double tt = len2 > 0 ? ((gx - tx[a]) * dx + (gy - ty[a]) * dy) / len2 : 0;
                    tt = Math.Max(0, Math.Min(1, tt));
                    double px = tx[a] + tt * dx - gx, py = ty[a] + tt * dy - gy;
                    minD = Math.Min(minD, Math.Sqrt(px * px + py * py));
                }
                return minD;
            }

            bool anyBad = false;
            var matches = new List<Match>();
            foreach (Match m in LineRe.Matches(svgText)) matches.Add(m);
            foreach (Match m in PathRe.Matches(svgText)) matches.Add(m);

            foreach (Match m in matches)
            {
                string rawId = m.Groups[1].Value;
                if (rawId == "ITEM_COLA_CAP") continue;   // collectible, no physics collider
                bool found = itemRadii.TryGetValue(rawId, out double rv);
                if (!found)
                {
                    string stripped = Regex.Replace(rawId, "_[0-9]+$", "");
                    found = itemRadii.TryGetValue(stripped, out rv);
                }
                double r = found ? rv : 50;   // fallback conservative estimate
                double x1 = ParseFloat(m.Groups[2].Value), y1 = ParseFloat(m.Groups[3].Value);
                double x2 = ParseFloat(m.Groups[4].Value), y2 = ParseFloat(m.Groups[5].Value);
                double gx = ((x1 + x2) / 2 - svgCx) * Scale;
                double gy = -(((y1 + y2) / 2) - svgCy) * Scale;
                double dist = DistToTrack(gx, gy);
                double need = TrackHalf + r;
                double margin = dist - need;
                report.Results.Add(new Result { RawId = rawId, Gx = gx, Gy = gy, Dist = dist, Need = need, Margin = margin, R = r, Fallback = !found });
            }

            report.Results = report.Results.OrderBy(x => x.Margin).ToList();   // stable, like JS Array.prototype.sort

            string sep = new string('\u2500', 88);
            report.Lines.Add("");   // console.log(`\nItem clearance report: ...`)
            report.Lines.Add("Item clearance report: " + svgPathForHeader);
            report.Lines.Add("Scale=" + Num(Scale) + "  TRACK_HALF=" + Num(TrackHalf) + "  svgCx=" + Num(svgCx) + "  svgCy=" + Num(svgCy));
            report.Lines.Add(sep);

            int warnCount = 0;
            foreach (Result res in report.Results)
            {
                double margin = res.Margin;
                if (margin > 60 && !res.Fallback) continue;   // only show tight/bad + fallback items
                string status = margin < 0 ? "\u274C OVERLAP  " + Fixed0(-margin).PadLeft(3) + " GU"
                              : margin < 30 ? "\u26A1 tight    " + Fixed0(margin).PadLeft(3) + " GU"
                              : "\u26A0\uFE0F  watch    " + Fixed0(margin).PadLeft(3) + " GU";
                string fb = res.Fallback ? " (r=50 estimate)" : "";
                report.Lines.Add(res.RawId.PadRight(26) + " gx=" + Fixed0(res.Gx).PadLeft(6) + " gy=" + Fixed0(res.Gy).PadLeft(6) +
                                 "  dist=" + Fixed0(res.Dist).PadLeft(4) + "  need=" + Num(res.Need) + "  " + status + fb);
                if (margin < 0) { anyBad = true; warnCount++; }
            }

            report.Lines.Add(sep);
            if (anyBad)
            {
                report.Lines.Add("");
                report.Lines.Add(warnCount.ToString(CultureInfo.InvariantCulture) + " item(s) overlap the track \u2014 move them outward in the SVG.");
                report.ExitCode = 1;
            }
            else
            {
                report.Lines.Add("");
                report.Lines.Add("All items clear \u2713");
            }
            return report;
        }
    }
}
