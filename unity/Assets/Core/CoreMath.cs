using System;

namespace DesktopDrift.Core
{
    /// <summary>Math helpers shared by the dotnet and Unity worlds.</summary>
    public static class CoreMath
    {
        // The single hypot used by both the dotnet and Unity worlds (do not use double.Hypot).
        // V8's Math.hypot may differ from this by a few ulp; the 1e-6 parity tolerance absorbs it.
        public static double Hypot(double x, double y)
        {
            return Math.Sqrt(x * x + y * y);
        }
    }
}
