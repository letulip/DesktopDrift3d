// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/scoring.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System;

namespace DesktopDrift.Core
{
    public static class Scoring
    {
        public const double DriftMinSlip = 60;   // scoring.js: DRIFT_MIN_SLIP  (lateral speed |vS| above which a drift is counted)
        public const double DriftMinSpeed = 90;  // scoring.js: DRIFT_MIN_SPEED (total speed below which no drift is registered)

        public const double QualitySlipRef = 160;  // scoring.js: QUALITY_SLIP_REF
        public const double QualitySpeedRef = 260; // scoring.js: QUALITY_SPEED_REF
        public const double QualityMax = 1.4;      // scoring.js: QUALITY_MAX

        public const double MultGainPerS = 0.14;        // scoring.js: MULT_GAIN_PER_S (multBuild growth per second of clean drift x quality)
        public const double MultTransitionBonus = 0.3;  // scoring.js: MULT_TRANSITION_BONUS
        public const double MultNearmissBonus = 0.28;   // scoring.js: MULT_NEARMISS_BONUS
        public const double MultMax = 8;                // scoring.js: MULT_MAX

        public const double SlipSignThreshold = 50; // scoring.js: SLIP_SIGN_THRESHOLD
        public const double ComboRate = 0.0015;     // scoring.js: COMBO_RATE (slip x speed x dt x mult x COMBO_RATE)

        public static bool IsDrifting(double vS, double speed)
        {
            return Math.Abs(vS) > DriftMinSlip && speed > DriftMinSpeed;
        }

        public static double DriftQuality(double slip, double speed)
        {
            return Math.Min(QualityMax, (slip / QualitySlipRef) * (speed / QualitySpeedRef));
        }

        public static double ComboMult(double multBuild)
        {
            return Math.Min(MultMax, 1 + multBuild);
        }

        public static double ComboGain(double slip, double speed, double dt, double mult)
        {
            return slip * speed * dt * ComboRate * mult;
        }

        public static int SlipSign(double vS)
        {
            return vS > SlipSignThreshold ? 1 : (vS < -SlipSignThreshold ? -1 : 0);
        }

        public static double PointsPerSecond(double score, double totalTime)
        {
            return totalTime > 0 ? score / totalTime : 0;
        }

        // JS: prevBestPPS == null (null and undefined both mean "no prior record").
        public static bool IsNewPpsRecord(double? prevBestPps, double pps)
        {
            return prevBestPps == null || pps > prevBestPps.Value;
        }
    }
}
