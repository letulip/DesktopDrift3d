// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/track-oval.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System;
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    /// <summary>
    /// The sandbox oval (js/track-oval.js): a parametric centerline, cones on both edges, round tableware
    /// in the track pockets and a table-setting at the corners. The module exports no id, no laps and no
    /// TABLE, so Id = "", Laps = 0 (the engine falls back to opts.laps) and Table = config.js TABLE.
    /// </summary>
    public static class OvalTrack
    {
        public const double TrackHalf = 100;      // track-oval.js: TRACK_HALF
        public const double ConeR = 9;            // track-oval.js: CONE_R
        public const int K = 8;                   // track-oval.js: K
        public const double CpR = TrackHalf + 70; // track-oval.js: CP_R
        private const int Samples = 300;          // track-oval.js: SAMPLES

        // config.js: TABLE = { w: 3400, h: 2900, shape: 'rect' }
        public const double TableW = 3400;
        public const double TableH = 2900;

        public static Vec2 CenterAt(double a)
        {
            double R = 1100 + 215 * Math.Sin(3 * a) + 170 * Math.Sin(4 * a);
            return new Vec2(R * Math.Cos(a) * 1.15, R * Math.Sin(a) * 0.94);
        }

        public static Vec2 TangentAt(double a)
        {
            const double e = 0.001;
            Vec2 p1 = CenterAt(a - e), p2 = CenterAt(a + e);
            double tx = p2.X - p1.X, ty = p2.Y - p1.Y;
            double len = CoreMath.Hypot(tx, ty);
            if (len == 0 || double.IsNaN(len)) len = 1; // JS `|| 1`
            return new Vec2(tx / len, ty / len);
        }

        private sealed class Dish
        {
            public string Kind = "";
            public string C = "";
        }

        // 1) Round tableware in the track pockets
        private static readonly Dish[] Dishes =
        {
            new Dish { Kind = "plate", C = "#e6ebf0" },
            new Dish { Kind = "bowl", C = "#3a6ea5" },
            new Dish { Kind = "saucer", C = "#ead9bf" },
            new Dish { Kind = "plate", C = "#f0c419" },
        };

        // 2) Table-setting at the corners: x, y, ang, hl, r, kind (colour is render-only).
        private static readonly (double X, double Y, double Ang, double Hl, double R, string Kind)[] Setting =
        {
            (1350, -1000, 0.5, 150, 120, "board"),
            (-1350, -1000, -0.5, 150, 22, "knife"),
            (-1380, 1100, 0.6, 110, 52, "spoon"),
            (1430, 1150, -0.4, 120, 30, "fork"),
        };

        /// <summary>Builds the oval. `table` = the config TABLE (null ⇒ 3400 × 2900 rect).</summary>
        public static TrackData Build(TableRect? table = null)
        {
            TableRect TABLE = table ?? new TableRect { W = TableW, H = TableH, Round = false };
            var T = new TrackData
            {
                TrackHalf = TrackHalf,
                ConeR = ConeR,
                K = K,
                CpR = CpR,
                Table = TABLE,
                Id = "",
                Laps = 0,
                Reversed = false,
            };
            List<Vec2> center = T.Center, outer = T.Outer, inner = T.Inner;
            for (int i = 0; i < Samples; i++)
            {
                double a = ((double)i / Samples) * Math.PI * 2;
                Vec2 c = CenterAt(a), t = TangentAt(a);
                double nx = -t.Y, ny = t.X;
                center.Add(c);
                outer.Add(new Vec2(c.X + nx * TrackHalf, c.Y + ny * TrackHalf));
                inner.Add(new Vec2(c.X - nx * TrackHalf, c.Y - ny * TrackHalf));
            }

            // Cones along the edges
            T.Cones = TrackUtil.PlaceCones(outer, inner);

            // Props on the table
            double DistToTrackPoint(double x, double y)
            {
                double best = double.PositiveInfinity;
                foreach (Vec2 c in center) { double dx = x - c.X, dy = y - c.Y; double d = dx * dx + dy * dy; if (d < best) best = d; }
                return Math.Sqrt(best);
            }
            List<Prop> props = T.Props;
            {
                var rad = new double[Samples];
                for (int i = 0; i < Samples; i++) rad[i] = CoreMath.Hypot(center[i].X, center[i].Y);
                var apex = new List<int>();
                for (int i = 0; i < Samples; i++)
                {
                    double p = rad[(i - 1 + Samples) % Samples], n = rad[(i + 1) % Samples];
                    if (rad[i] < p && rad[i] <= n) apex.Add(i);
                }
                int di = 0;
                foreach (int i in apex)
                {
                    Vec2 c = center[i];
                    double r = rad[i];
                    double dirx = c.X / r, diry = c.Y / r;
                    double innerEdge = r - TrackHalf;
                    double objR = Math.Max(110, Math.Min(240, innerEdge * 0.33));
                    double cd = innerEdge - objR - 34;
                    if (cd < objR * 0.4) continue;
                    Dish d = Dishes[di++ % Dishes.Length];
                    props.Add(Prop.Prepared(dirx * cd, diry * cd, Math.Atan2(diry, dirx), 0, objR, d.Kind));
                }
            }
            foreach (var o in Setting)
            {
                if (Math.Abs(o.X) + o.Hl + o.R > TABLE.W / 2 - 60) continue;
                if (Math.Abs(o.Y) + o.Hl + o.R > TABLE.H / 2 - 60) continue;
                if (DistToTrackPoint(o.X, o.Y) > TrackHalf + o.R + 40) props.Add(Prop.Prepared(o.X, o.Y, o.Ang, o.Hl, o.R, o.Kind));
            }

            // Checkpoints
            for (int i = 0; i < K; i++) T.Checkpoints.Add(CenterAt(((double)i / K) * Math.PI * 2));

            // Starting position
            Vec2 start = CenterAt(0), startT = TangentAt(0);
            T.StartPos = new Vec2(start.X, start.Y);
            T.StartAngle = Math.Atan2(startT.Y, startT.X);
            return T;
        }
    }
}
