// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/collectibles.js @ 5556140 (2026-08-06), ported 2026-10-28.
namespace DesktopDrift.Core
{
    /// <summary>
    /// JS: union of COLA_CAP { kind, hl, r, ... } and TIRE { kind, r, value, ... } plus the x, y, capId
    /// the track factory adds. Image paths (imgSrc/imgFull) and the colour are not ported.
    /// </summary>
    public sealed class Collectible
    {
        /// <summary>"cola" | "tire"</summary>
        public string Kind = "";
        public double X;
        public double Y;
        public double R;
        public int Value;
        public string CapId = "";
        public double Hl;
    }
}
