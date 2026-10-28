// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/cola.js @ 5556140 (2026-08-06), ported 2026-10-28.
using System;

namespace DesktopDrift.Core
{
    public static class Cola
    {
        // Shortest signed angle from a to b, result in (-PI, PI].
        public static double AngDelta(double a, double b)
        {
            double d = b - a;
            while (d > Math.PI) d -= 2 * Math.PI;
            while (d < -Math.PI) d += 2 * Math.PI;
            return d;
        }

        // Progress toward a full loop: |sweep| / 2PI, clamped to [0, 1].
        public static double CapProgress(double sweep)
        {
            return Math.Min(1, Math.Abs(sweep) / (2 * Math.PI));
        }

        public static double StepSweep(double sweep, double prevAng, double ang, bool engaged, double dt, double decay)
        {
            if (engaged) return sweep + AngDelta(prevAng, ang);
            if (sweep > 0) return Math.Max(0, sweep - decay * dt);
            if (sweep < 0) return Math.Min(0, sweep + decay * dt);
            return 0;
        }
    }
}
