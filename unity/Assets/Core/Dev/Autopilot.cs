using System;
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    /// <summary>
    /// Dev/test tool, not game logic: mirrors the lookahead autopilot of tools/engine-harness.mjs (LOOKAHEAD = 12,
    /// DEAD_BAND = 0.05 rad), which drove the recorded traces of the real 2D engine. Call Steer once per frame with the
    /// car pose BEFORE the step; it keeps the nearest-centre index between calls like the harness's `nearIdx`.
    /// </summary>
    public sealed class Autopilot
    {
        readonly int _lookahead;
        readonly double _deadBand;
        int _nearIdx;

        /// <summary>Heading error (rad) of the last Steer call, for diagnostics.</summary>
        public double LastErr { get; private set; }

        public Autopilot(int lookahead = 12, double deadBand = 0.05)
        {
            _lookahead = lookahead;
            _deadBand = deadBand;
        }

        public void Reset() => _nearIdx = 0;

        /// <returns>-1 (left), 0 or +1 (right).</returns>
        public int Steer(double carX, double carY, double carAngle, IReadOnlyList<Vec2> center)
        {
            _nearIdx = TrackUtil.NearestCenter(carX, carY, center, _nearIdx).Idx;
            Vec2 tgt = center[(_nearIdx + _lookahead) % center.Count];
            double hx = Math.Cos(carAngle), hy = Math.Sin(carAngle);
            double tx = tgt.X - carX, ty = tgt.Y - carY;
            double err = Math.Atan2(hx * ty - hy * tx, hx * tx + hy * ty);
            LastErr = err;
            bool left = err < -_deadBand, right = err > _deadBand;
            return left ? -1 : right ? 1 : 0;
        }
    }
}
