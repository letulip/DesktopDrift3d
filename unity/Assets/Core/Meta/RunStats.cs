// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/achievements.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable

namespace DesktopDrift.Core
{
    // The `run` fields the achievement checks read. Missing JS fields (undefined) are 0 / false here.
    public sealed class RunStats
    {
        public double ConesHit;          // achievements.js: run.conesHit
        public double ConesTotal;        // achievements.js: run.conesTotal
        public double Crashes;           // achievements.js: run.crashes
        public double TireTotalOnTrack;  // achievements.js: run.tireTotalOnTrack
        public double TiresThisRun;      // achievements.js: run.tiresThisRun
        public bool ComboUnbroken;       // achievements.js: run.comboUnbroken
        public bool Finished;            // achievements.js: run.finished
        public double Hour;              // achievements.js: run.hour
        public double NearMisses;        // achievements.js: run.nearMisses
        public double Pps;               // achievements.js: run.pps
        public double Stars;             // achievements.js: run.stars
        public double TimeAt8;           // achievements.js: run.timeAt8
    }
}
