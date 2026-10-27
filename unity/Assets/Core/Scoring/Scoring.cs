// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/scoring.js @ 5556140 (2026-08-06), ported 2026-10-27.
//
// Only the drift predicate needed by the physics step; the rest of scoring.js is SPEC-01.
using System;

namespace DesktopDrift.Core
{
    public static class Scoring
    {
        public const double DriftMinSlip = 60;   // scoring.js: DRIFT_MIN_SLIP  (lateral speed |vS| above which a drift is counted)
        public const double DriftMinSpeed = 90;  // scoring.js: DRIFT_MIN_SPEED (total speed below which no drift is registered)

        public static bool IsDrifting(double vS, double speed)
        {
            return Math.Abs(vS) > DriftMinSlip && speed > DriftMinSpeed;
        }
    }
}
