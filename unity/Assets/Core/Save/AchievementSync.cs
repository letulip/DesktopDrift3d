// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/ach-sync.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    public static class AchievementSync
    {
        // Evaluate state-based achievements against the live save; persist unlocks + ladder progress and
        // credit each new reward once. Loops to a fixed point (crediting rewards raises the wallet, which
        // can cross the next wallet-ladder tier). Returns the newly unlocked defs.
        public static List<Achievements.UnlockedEntry> SyncState(SaveState s, ContentDescriptor content)
        {
            Dictionary<string, object?> st = s.Stats();
            var ctx = new AchievementContext
            {
                Run = null,
                Owned = Strings(s.Owned()),
                OwnedCars = Strings(s.OwnedCars()),
                Cleared = st.TryGetValue("cleared", out object? cl) && cl is List<object?> cll ? Strings(cll) : new List<string>(),
                Records = Achievements.FlattenRecords(s.Records()),
                Caps = s.CapsMap(),
                Lifetime = new AchievementContext.LifetimeStats { Runs = Num(st, "runs"), DriftSecs = Num(st, "driftSecs") },
                Content = content,
            };
            var output = new List<Achievements.UnlockedEntry>();
            for (;;)
            {
                ctx.Wallet = s.Wallet();
                Achievements.EvalResult res = Achievements.Evaluate(ctx, s.AchUnlocked());
                foreach (Achievements.ProgressEntry p in res.Progress) s.AchSetProgress(p.Id, p.Value);
                int credited = 0;
                foreach (Achievements.UnlockedEntry u in res.Unlocked)
                {
                    if (s.AchUnlock(u.Id)) // idempotent: pays once
                    {
                        if (u.Reward != 0) s.AddTires(u.Reward, "Achievement: " + u.Name);
                        output.Add(u);
                        credited++;
                    }
                }
                if (credited == 0) break; // fixed point: nothing new this round
            }
            return output;
        }

        private static List<string> Strings(List<object?> l)
        {
            var r = new List<string>();
            foreach (object? o in l) if (o is string str) r.Add(str);
            return r;
        }

        private static double Num(Dictionary<string, object?> d, string key)
        {
            return d.TryGetValue(key, out object? v) && v is double x ? x : 0;
        }
    }
}
