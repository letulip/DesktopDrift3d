#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using DesktopDrift.Core;

namespace DesktopDrift.Game
{
    /// <summary>Pure HUD / result text formats, wording verbatim from the 2D game (render.js 807-848, race-results.js).</summary>
    public static class HudFormat
    {
        const double GuToKmh = 0.023; // config GU_TO_KMH
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static string I(double v) => ((long)CoreMath.JsRound(v)).ToString(Inv);

        public static string LapTime(double t) => t.ToString("F2", Inv);

        public static string LapOpt(double? t) => t == null ? "—" : t.Value.ToString("F2", Inv) + " s";

        public static string Speed(double units, bool mph) => (units * GuToKmh * (mph ? 0.621371 : 1)).ToString("F1", Inv);

        public static string Score(double s) => I(s);

        public static string Combo(double points, double mult) => "+" + I(points) + "   ×" + mult.ToString("F1", Inv);

        /// <summary>Newest lap first, one "lap n: +pts" per line.</summary>
        public static string LapScores(IReadOnlyList<RaceState.LapScore> laps)
        {
            var lines = new List<string>(laps.Count);
            for (int i = laps.Count - 1; i >= 0; i--)
                lines.Add("lap " + laps[i].N.ToString(Inv) + ": +" + laps[i].Pts.ToString(Inv));
            return string.Join("\n", lines);
        }

        public static (string Text, string Color, double Opacity) Count(double startCd, double goT)
        {
            if (startCd > 0) return (((long)Math.Ceiling(startCd)).ToString(Inv), "#fff", 1);
            if (goT > 0) return ("GO!", "#9dff8f", Math.Min(1, goT / 0.4));
            return ("", "#fff", 0);
        }

        public static double FlashOpacity(double flashT) => Math.Max(0, flashT / 0.9);

        public static string ResultPps(double pps) => CoreMath.JsRound(pps).ToString("N0", Inv) + " PPS";

        public static string ResultTotal(int score, double totalTime) =>
            "Total: " + score.ToString(Inv) + " · " + totalTime.ToString("F1", Inv) + " s";

        public static int Stars(double pps) => (int)Math.Min(5, Math.Floor(CoreMath.JsRound(pps) / 100));

        public static (string Title, string Parts) TireParts(RaceResult.TireBreakdown t)
        {
            int total = t.Pickup + t.Cap + t.CleanSweep + t.FirstClear + t.Finish + t.Trophy + t.Unbroken;
            if (total == 0) return ("", "");
            var parts = new List<string>();
            void Add(string label, int n) { if (n != 0) parts.Add(label + " +" + n.ToString(Inv)); }
            Add("pickups", t.Pickup);
            Add("cola cap", t.Cap);
            Add("clean sweep", t.CleanSweep);
            Add("first clear", t.FirstClear);
            Add("finish", t.Finish);
            Add("🏅 trophy", t.Trophy);
            Add("♾️ unbroken", t.Unbroken);
            return ("🛞 +" + total.ToString(Inv) + " tires", string.Join(" · ", parts));
        }

        public static (string Lap, string Time, string Pts) LapRow(RaceState.LapScore l) =>
            ("Lap " + l.N.ToString(Inv), l.T > 0 ? l.T.ToString("F2", Inv) + " s" : "—", "+" + l.Pts.ToString(Inv));

        public static string BestLap(double? b) => b == null ? "" : "Best lap  " + b.Value.ToString("F2", Inv) + " s";
    }
}
