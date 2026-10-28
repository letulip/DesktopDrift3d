// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/neon.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    public sealed class NeonConfig
    {
        public string Layout = "solid";
        public string Anim = "none";
        public IReadOnlyList<string>? Colors;
        public double Speed = 1;
    }
}
