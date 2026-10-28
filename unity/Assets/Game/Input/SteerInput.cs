using System.Collections.Generic;

namespace DesktopDrift.Game
{
    /// <summary>Port of resolveSteer from the 2D input.js. Result is always -1, 0 or 1.</summary>
    public static class SteerInput
    {
        public static int Resolve(IReadOnlyList<double> pointerXs, bool keyLeft, bool keyRight, double screenWidth)
        {
            int s = 0;
            for (int i = 0; i < pointerXs.Count; i++) s += pointerXs[i] < screenWidth / 2 ? -1 : 1;
            int k = 0;
            if (keyLeft) k -= 1;
            if (keyRight) k += 1;
            if (k != 0) return k;
            return s > 0 ? 1 : (s < 0 ? -1 : 0);
        }
    }
}
