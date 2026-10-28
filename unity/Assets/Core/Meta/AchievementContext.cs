// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/achievements.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    // The normalised `ctx` (achievements.js `_norm`, lines 23-36). Defaults = the `_norm` defaults.
    public sealed class AchievementContext
    {
        public sealed class LifetimeStats
        {
            public double Runs;       // ctx.lifetime.runs
            public double DriftSecs;  // ctx.lifetime.driftSecs
        }

        public RunStats? Run;                                                        // ctx.run (null on a purchase-triggered eval)
        public double Wallet;                                                        // ctx.wallet
        public List<string> Owned = new List<string>();                              // ctx.owned
        public List<string> OwnedCars = new List<string>();                          // ctx.ownedCars
        public List<string> Cleared = new List<string>();                            // ctx.cleared
        public Dictionary<string, double> Records = new Dictionary<string, double>(); // ctx.records (FLATTENED: instance id -> bestPPS)
        public Dictionary<string, List<double>> Caps = new Dictionary<string, List<double>>(); // ctx.caps (track id -> collected cap indices)
        public LifetimeStats Lifetime = new LifetimeStats();                         // ctx.lifetime
        public ContentDescriptor Content = new ContentDescriptor();                  // ctx.content
    }
}
