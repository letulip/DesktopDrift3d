using System;

namespace DesktopDrift.Core
{
    /// <summary>Math helpers shared by the dotnet and Unity worlds.</summary>
    public static class CoreMath
    {
        // The single hypot used by both the dotnet and Unity worlds (do not use double.Hypot or
        // Math.Sqrt(x*x + y*y)). Mirrors V8's Math.hypot (src/builtins/math.tq, MathHypot) operation
        // for operation so the parity traces and the checkpoint argmax see the same ulps as the 2D game:
        // NaN/Infinity rules, scale by the largest magnitude, Kahan-compensated sum of squares, sqrt * max.
        // Order of the two arguments matters (same as the JS call sites). Not bit-exact only where a
        // compiler contracts `n * n - compensation` into an FMA (IL2CPP/clang may); the 1e-6 parity
        // tolerance is the gate, ulps are not.
        public static double Hypot(double x, double y)
        {
            bool oneIsNaN = false;
            double ax = 0, ay = 0, max = 0;
            if (double.IsNaN(x)) oneIsNaN = true; else { ax = Math.Abs(x); if (ax > max) max = ax; }
            if (double.IsNaN(y)) oneIsNaN = true; else { ay = Math.Abs(y); if (ay > max) max = ay; }
            if (double.IsPositiveInfinity(max)) return double.PositiveInfinity;
            if (oneIsNaN) return double.NaN;
            if (max == 0) return 0;
            double sum = 0, compensation = 0;
            double n = ax / max;
            double summand = n * n - compensation;
            double preliminary = sum + summand;
            compensation = (preliminary - sum) - summand;
            sum = preliminary;
            n = ay / max;
            summand = n * n - compensation;
            preliminary = sum + summand;
            compensation = (preliminary - sum) - summand;
            sum = preliminary;
            return Math.Sqrt(sum) * max;
        }

        // JS Math.round: half rounds toward +∞ (C# Math.Round is banker's rounding – never use it for parity)
        public static double JsRound(double x)
        {
            return Math.Floor(x + 0.5);
        }
    }
}
