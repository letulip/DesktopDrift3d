// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/achievements.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System;

namespace DesktopDrift.Core
{
    // One catalog entry. Ladder entries also carry Bar/Target/Fmt (the `bar`, `target`, `fmt` static fields).
    public sealed class AchievementDef
    {
        public string Id = "";
        public string Name = "";
        public string Desc = "";
        public string Icon = "";
        public string Category = "";
        public bool Hidden;
        public int Reward;
        public bool Bar;
        public double Target;
        public Func<double, string>? Fmt;
        public Func<AchievementContext, CheckResult> Check = _ => CheckResult.False;
    }
}
