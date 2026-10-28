// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/car-stats.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System;

namespace DesktopDrift.Core
{
    public static class CarStats
    {
        public const double GuToKmh = 0.023;     // car-stats.js: GU_TO_KMH
        public const double SpeedMaxKmh = 15;    // car-stats.js: SPEED_MAX_KMH
        public const double SteerMax = 5.0;      // car-stats.js: STEER_MAX
        public const double LowturnMax = 0.5;    // car-stats.js: LOWTURN_MAX
        public const double ThrustMax = 900;     // car-stats.js: THRUST_MAX
        public const double MaxRating = 10;      // car-stats.js: MAX_RATING

        private static double ClampRating(double n)
        {
            return Math.Max(0, Math.Min(MaxRating, CoreMath.JsRound(n)));
        }

        public static double SpeedRating(DriveProfile d)
        {
            return ClampRating(d.MaxSpeed * GuToKmh / SpeedMaxKmh * 10);
        }

        public static double AccRating(DriveProfile d)
        {
            return ClampRating(d.Thrust / ThrustMax * 10);
        }

        public static double HandlingRating(DriveProfile d)
        {
            return ClampRating((d.Steer / SteerMax * 0.7 + d.LowSpeedTurn / LowturnMax * 0.3) * 10);
        }

        // JS Number.prototype.toFixed(digits) then unary +, approximated as round-half-up on x*10^digits.
        private static double ToFixed(double x, int digits)
        {
            double k = Math.Pow(10, digits);
            return CoreMath.JsRound(x * k) / k;
        }

        public static DriveProfile DriveForRatings(double handling = 5, double accel = 5, double speed = 5)
        {
            double f = handling / 10;
            return new DriveProfile
            {
                MaxSpeed = CoreMath.JsRound(speed * SpeedMaxKmh / (GuToKmh * 10)),
                Thrust = CoreMath.JsRound(accel * ThrustMax / 10),
                Steer = ToFixed(f * SteerMax, 2),
                LowSpeedTurn = ToFixed(f * LowturnMax, 3),
            };
        }
    }
}
