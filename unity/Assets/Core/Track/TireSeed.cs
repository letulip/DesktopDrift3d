// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/tire-seed.js @ 5556140 (2026-08-06), ported 2026-10-28.
using System;
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    public static class TireSeed
    {
        public const double CurveGain = 2.2;   // tire-seed.js: CURVE_GAIN
        public const double MaxFrac = 0.65;    // tire-seed.js: MAX_FRAC
        public const int Window = 6;           // tire-seed.js: WINDOW

        public static List<Vec2> SeedTires(IReadOnlyList<Vec2> center, IReadOnlyList<Vec2> inner, IReadOnlyList<Vec2> outer, int n)
        {
            int N = center.Count;
            if (N == 0 || n <= 0) return new List<Vec2>();

            // Cumulative arc length around the closed loop.
            var cum = new double[N + 1];
            cum[0] = 0;
            for (int i = 0; i < N; i++)
            {
                Vec2 a = center[i], b = center[(i + 1) % N];
                cum[i + 1] = cum[i] + CoreMath.Hypot(b.X - a.X, b.Y - a.Y);
            }
            double total = cum[N];

            var result = new List<Vec2>();
            for (int k = 0; k < n; k++)
            {
                double targetArc = (k + 0.5) * total / n;   // half-offset so none sits on the start line
                // Find the centerline index nearest that arc position.
                int i = 0;
                while (i < N && cum[i + 1] < targetArc) i++;
                if (i >= N) i = N - 1;

                // Local turn: signed sine of the angle between the in- and out-tangents.
                Vec2 p = center[i];
                Vec2 a = center[(i - Window + N) % N];
                Vec2 b = center[(i + Window) % N];
                double t1x = p.X - a.X, t1y = p.Y - a.Y;
                double t2x = b.X - p.X, t2y = b.Y - p.Y;
                double m1 = CoreMath.Hypot(t1x, t1y), m2 = CoreMath.Hypot(t2x, t2y);
                if (m1 == 0 || double.IsNaN(m1)) m1 = 1;
                if (m2 == 0 || double.IsNaN(m2)) m2 = 1;
                double sinT = (t1x * t2y - t1y * t2x) / (m1 * m2);   // >0 left turn, <0 right turn

                double frac = Math.Min(MaxFrac, Math.Abs(sinT) * CurveGain);
                Vec2 edge = sinT > 0 ? outer[i] : inner[i];          // concave (inner-of-corner) side
                double x = p.X + (edge.X - p.X) * frac;
                double y = p.Y + (edge.Y - p.Y) * frac;
                result.Add(new Vec2(CoreMath.JsRound(x), CoreMath.JsRound(y)));
            }
            return result;
        }
    }
}
