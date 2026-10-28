// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/collision.js @ 5556140 (2026-08-06), ported 2026-10-28.
using System;

namespace DesktopDrift.Core
{
    /// <summary>JS: prop { x, y, ang, hl, r, kind, _cos, _sin } (the cos/sin cache is filled by prepProp).</summary>
    public sealed class Prop
    {
        public double X;
        public double Y;
        public double Ang;
        public double Hl;
        public double R;
        public string Kind = "";
        public double Cos;
        public double Sin;

        /// <summary>Mirrors prepProp of js/track-util.js (hl = hl || 0; _cos/_sin cached from ang).</summary>
        public static Prop Prepared(double x, double y, double ang, double hl, double r, string kind = "")
        {
            return new Prop
            {
                X = x,
                Y = y,
                Ang = ang,
                Hl = hl,
                R = r,
                Kind = kind,
                Cos = Math.Cos(ang),
                Sin = Math.Sin(ang),
            };
        }
    }
}
