// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/neon.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
namespace DesktopDrift.Core
{
    // neon.js: LAYOUTS entry
    public sealed class NeonLayout
    {
        public string Id;
        public string Label;
        public int Colours;

        public NeonLayout(string id, string label, int colours)
        {
            Id = id;
            Label = label;
            Colours = colours;
        }
    }
}
