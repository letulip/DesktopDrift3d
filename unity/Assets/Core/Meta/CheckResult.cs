// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/achievements.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable

namespace DesktopDrift.Core
{
    // The result of one achievement check (achievements.js evaluate, lines 283-297):
    //   object with `progress` -> progress entry, unlocked when progress >= target;
    //   `true` -> unlocked; anything else -> nothing.
    public readonly struct CheckResult
    {
        public readonly bool Unlocked;
        public readonly bool HasProgress;
        public readonly double Progress;
        public readonly double Target;

        private CheckResult(bool unlocked, bool hasProgress, double progress, double target)
        {
            Unlocked = unlocked;
            HasProgress = hasProgress;
            Progress = progress;
            Target = target;
        }

        public static readonly CheckResult True = new CheckResult(true, false, 0, 0);
        public static readonly CheckResult False = new CheckResult(false, false, 0, 0);

        public static CheckResult WithProgress(double progress, double target)
        {
            return new CheckResult(false, true, progress, target);
        }

        public static CheckResult Of(bool unlocked)
        {
            return unlocked ? True : False;
        }
    }
}
