// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/achievements.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DesktopDrift.Core
{
    public static class Achievements
    {
        // ── Input / result shapes (buildContent inputs, evaluate output) ──────────
        public sealed class ContentTrack
        {
            public string Id = "";
            public string Name = "";
        }

        public sealed class ContentShopItem
        {
            public string Id = "";
            public string Kind = "";
        }

        public sealed class ContentCar
        {
            public string Id = "";
            public double? Price;   // null / 0 = free (JS: cars.filter(c => c.price))
        }

        public sealed class UnlockedEntry
        {
            public string Id = "";
            public string Name = "";
            public string Icon = "";
            public int Reward;
        }

        public sealed class ProgressEntry
        {
            public string Id = "";
            public double Value;
        }

        public sealed class EvalResult
        {
            public List<UnlockedEntry> Unlocked = new List<UnlockedEntry>();
            public List<ProgressEntry> Progress = new List<ProgressEntry>();
        }

        // ── Helpers (achievements.js lines 40-57) ─────────────────────────────────
        // Instances the player has provably finished: stats.cleared PLUS anything with a persisted best.
        private static HashSet<string> ClearedIds(AchievementContext x)
        {
            var s = new HashSet<string>(x.Cleared);
            foreach (string k in x.Records.Keys) s.Add(k);
            return s;
        }

        // Every forward/reversed/all instance is finished (and there is at least one to finish).
        private static bool AllCleared(List<string> ids, AchievementContext x)
        {
            HashSet<string> done = ClearedIds(x);
            return ids.Count > 0 && ids.All(id => done.Contains(id));
        }

        // The best star rating the player has ever recorded (from persisted bestPPS).
        private static double BestRecordStars(AchievementContext x)
        {
            double m = 0;
            foreach (double pps in x.Records.Values) m = Math.Max(m, Economy.StarsForPps(pps));
            return m;
        }

        // True if the player has ever finished a race: this run, or any evidence in the save.
        private static bool HasFinishedEver(AchievementContext x)
        {
            return (x.Run != null && x.Run.Finished) || x.Cleared.Count > 0 || x.Records.Count > 0;
        }

        private static double RunStars(AchievementContext x) { return x.Run != null ? x.Run.Stars : 0; }

        private static bool HourIn(AchievementContext x, params int[] hours)
        {
            if (x.Run == null) return false;
            foreach (int h in hours) if (x.Run.Hour == h) return true;
            return false;
        }

        private static bool CapCollected(AchievementContext x, string trackId)
        {
            return x.Caps.TryGetValue(trackId, out List<double>? a) && a != null && a.Count > 0;
        }

        // Wraps a boolean check: true -> CheckResult.True, otherwise nothing.
        private static Func<AchievementContext, CheckResult> Pred(Func<AchievementContext, bool> check)
        {
            return x => check(x) ? CheckResult.True : CheckResult.False;
        }

        // ── Static catalog (lines 60-186) ─────────────────────────────────────────
        private static readonly List<AchievementDef> Static = new List<AchievementDef>
        {
            // Progression
            new AchievementDef
            {
                Id = "first-drift", Name = "First Drift", Desc = "Finish your first race.",
                Icon = "🏁", Category = "progression", Hidden = false, Reward = 10,
                Check = Pred(x => HasFinishedEver(x)),
            },
            new AchievementDef
            {
                Id = "road-tripper", Name = "Road Tripper", Desc = "Clear every track (forward).",
                Icon = "🗺️", Category = "progression", Hidden = false, Reward = 30,
                Check = Pred(x => AllCleared(x.Content.ForwardIds, x)),
            },
            new AchievementDef
            {
                Id = "mirror-walker", Name = "Through the Looking Glass", Desc = "Clear a reversed track.",
                Icon = "🪞", Category = "progression", Hidden = false, Reward = 15,
                Check = Pred(x => { HashSet<string> done = ClearedIds(x); return x.Content.ReversedIds.Any(id => done.Contains(id)); }),
            },
            new AchievementDef
            {
                Id = "backwards", Name = "Reverse Psychology", Desc = "Clear every reversed track.",
                Icon = "↩️", Category = "progression", Hidden = false, Reward = 30,
                Check = Pred(x => AllCleared(x.Content.ReversedIds, x)),
            },
            new AchievementDef
            {
                Id = "completionist", Name = "Completionist", Desc = "Clear every track, both directions.",
                Icon = "🏆", Category = "progression", Hidden = false, Reward = 75,
                Check = Pred(x => AllCleared(x.Content.AllInstanceIds, x)),
            },

            // Skill
            new AchievementDef
            {
                Id = "three-star", Name = "Solid Run", Desc = "Earn 3 stars in a race.",
                Icon = "⭐", Category = "skill", Hidden = false, Reward = 5,
                Check = Pred(x => Math.Max(RunStars(x), BestRecordStars(x)) >= 3),
            },
            new AchievementDef
            {
                Id = "four-star", Name = "Dialed In", Desc = "Earn 4 stars in a race.",
                Icon = "🌟", Category = "skill", Hidden = false, Reward = 20,
                Check = Pred(x => Math.Max(RunStars(x), BestRecordStars(x)) >= 4),
            },
            new AchievementDef
            {
                Id = "five-star", Name = "Flawless", Desc = "Earn 5 stars in a race.",
                Icon = "💫", Category = "skill", Hidden = false, Reward = 40,
                Check = Pred(x => Math.Max(RunStars(x), BestRecordStars(x)) >= 5),
            },
            new AchievementDef
            {
                Id = "daredevil", Name = "Daredevil", Desc = "Land 10 near misses in one race.",
                Icon = "😎", Category = "skill", Hidden = false, Reward = 25,
                Check = Pred(x => (x.Run != null ? x.Run.NearMisses : 0) >= 10),
            },

            // Combo
            new AchievementDef
            {
                Id = "flow-1", Name = "In the Zone", Desc = "Hold an 8× combo for 10s total in a race.",
                Icon = "🌀", Category = "combo", Hidden = false, Reward = 20,
                Check = Pred(x => (x.Run != null ? x.Run.TimeAt8 : 0) >= 10),
            },
            new AchievementDef
            {
                Id = "flow-2", Name = "Untouchable Flow", Desc = "Hold an 8× combo for 30s total in a race.",
                Icon = "🌪️", Category = "combo", Hidden = false, Reward = 35,
                Check = Pred(x => (x.Run != null ? x.Run.TimeAt8 : 0) >= 30),
            },
            new AchievementDef
            {
                Id = "flow-3", Name = "Trance State", Desc = "Hold an 8× combo for 60s total in a race.",
                Icon = "🌈", Category = "combo", Hidden = false, Reward = 50,
                Check = Pred(x => (x.Run != null ? x.Run.TimeAt8 : 0) >= 60),
            },
            new AchievementDef
            {
                Id = "perpetual", Name = "Perpetual Motion", Desc = "Finish a race in one unbroken drift.",
                Icon = "♾️", Category = "combo", Hidden = true, Reward = 75,
                Check = Pred(x => x.Run != null && x.Run.Finished && x.Run.ComboUnbroken),
            },

            // Economy / collection
            new AchievementDef
            {
                Id = "clean-sweep", Name = "Clean Sweep", Desc = "Collect every tire on a track in one run.",
                Icon = "🧹", Category = "economy", Hidden = false, Reward = 20,
                Check = Pred(x => x.Run != null && x.Run.TireTotalOnTrack > 0 && x.Run.TiresThisRun == x.Run.TireTotalOnTrack),
            },
            new AchievementDef
            {
                Id = "soda-pop", Name = "Soda Pop", Desc = "Collect your first cola cap.",
                Icon = "🥤", Category = "economy", Hidden = false, Reward = 10,
                Check = Pred(x => x.Caps.Values.Any(a => a != null && a.Count > 0)),
            },
            new AchievementDef
            {
                Id = "big-spender", Name = "Retail Therapy", Desc = "Buy your first cosmetic.",
                Icon = "🛍️", Category = "economy", Hidden = false, Reward = 15,
                Check = Pred(x => x.Owned.Count >= 1),
            },
            new AchievementDef
            {
                Id = "fashionista", Name = "Fashionista", Desc = "Own 5 cosmetics.",
                Icon = "👗", Category = "economy", Hidden = false, Reward = 30,
                Check = Pred(x => x.Owned.Count >= 5),
            },
            new AchievementDef
            {
                Id = "well-rounded", Name = "Well Rounded", Desc = "Buy at least one item from every shop section.",
                Icon = "🧩", Category = "economy", Hidden = false, Reward = 30,
                Check = Pred(x => x.Content.ShopCategories.Count > 0 &&
                    x.Content.ShopCategories.All(cat => x.Owned.Any(id =>
                        x.Content.CatalogById.TryGetValue(id, out string? kind) && kind == cat))),
            },
            new AchievementDef
            {
                Id = "new-wheels", Name = "New Wheels", Desc = "Buy your first car.",
                Icon = "🚗", Category = "economy", Hidden = false, Reward = 25,
                Check = Pred(x => x.OwnedCars.Count >= 1),
            },
            new AchievementDef
            {
                Id = "three-car-garage", Name = "Three-Car Garage", Desc = "Own three bought cars.",
                Icon = "🏎️", Category = "economy", Hidden = false, Reward = 40,
                Check = Pred(x => x.OwnedCars.Count >= 3),
            },

            // Hidden
            new AchievementDef
            {
                Id = "full-garage", Name = "Full Garage", Desc = "Own every car in the shop.",
                Icon = "🏁", Category = "hidden", Hidden = true, Reward = 150,
                Check = Pred(x => x.Content.Cars.Count > 0 && x.Content.Cars.All(id => x.OwnedCars.Contains(id))),
            },
            new AchievementDef
            {
                Id = "glass-cannon", Name = "Living Dangerously", Desc = "5 stars and 15+ near misses in one race.",
                Icon = "💥", Category = "hidden", Hidden = true, Reward = 40,
                Check = Pred(x => (x.Run != null ? x.Run.Stars : 0) >= 5 && (x.Run != null ? x.Run.NearMisses : 0) >= 15),
            },
            // Crash-count gags: run-only (run.crashes never persisted), so they fire from a live race.
            new AchievementDef
            {
                Id = "crash-5", Name = "Fender Bender", Desc = "Crash 5 times in a single race.",
                Icon = "💢", Category = "hidden", Hidden = true, Reward = 10,
                Check = Pred(x => (x.Run != null ? x.Run.Crashes : 0) >= 5),
            },
            new AchievementDef
            {
                Id = "crash-10", Name = "Demolition Derby", Desc = "Crash 10 times in a single race.",
                Icon = "🚑", Category = "hidden", Hidden = true, Reward = 15,
                Check = Pred(x => (x.Run != null ? x.Run.Crashes : 0) >= 10),
            },
            new AchievementDef
            {
                Id = "crash-20", Name = "Wrecking Ball", Desc = "Crash 20 times in a single race.",
                Icon = "🧱", Category = "hidden", Hidden = true, Reward = 25,
                Check = Pred(x => (x.Run != null ? x.Run.Crashes : 0) >= 20),
            },
            new AchievementDef
            {
                Id = "crash-40", Name = "Bull in a China Shop", Desc = "Crash 40 times in a single race.",
                Icon = "🔨", Category = "hidden", Hidden = true, Reward = 40,
                Check = Pred(x => (x.Run != null ? x.Run.Crashes : 0) >= 40),
            },
            new AchievementDef
            {
                Id = "untouchable", Name = "Untouchable", Desc = "Finish a race without a single crash.",
                Icon = "🛡️", Category = "hidden", Hidden = true, Reward = 30,
                Check = Pred(x => x.Run != null && x.Run.Finished && x.Run.Crashes == 0),
            },
            new AchievementDef
            {
                Id = "slalom-saint", Name = "Slalom Saint", Desc = "Finish a race without knocking a single cone.",
                Icon = "🚧", Category = "hidden", Hidden = true, Reward = 60,
                Check = Pred(x => x.Run != null && x.Run.Finished && x.Run.ConesTotal > 0 && x.Run.ConesHit == 0),
            },
            new AchievementDef
            {
                Id = "bulldozer", Name = "Bulldozer", Desc = "Knock over every cone in a race.",
                Icon = "🚜", Category = "hidden", Hidden = true, Reward = 30,
                Check = Pred(x => x.Run != null && x.Run.ConesTotal > 0 && x.Run.ConesHit == x.Run.ConesTotal),
            },
            // Gag: finish a race with a rounded PPS of exactly 1 (matches the results screen).
            new AchievementDef
            {
                Id = "one-pps", Name = "Participation Trophy", Desc = "Finish a race scoring just 1 PPS.",
                Icon = "🏅", Category = "hidden", Hidden = true, Reward = 5,
                Check = Pred(x => x.Run != null && x.Run.Finished && Economy.IsOnePps(x.Run.Pps)),
            },
            new AchievementDef
            {
                Id = "cola-collector", Name = "Sugar High", Desc = "Collect the cola cap on every track.",
                Icon = "🧃", Category = "hidden", Hidden = true, Reward = 40,
                Check = Pred(x => x.Content.ForwardTrackIds.Count > 0 &&
                    x.Content.ForwardTrackIds.All(tid => CapCollected(x, tid))),
            },
            new AchievementDef
            {
                Id = "night-owl", Name = "Night Owl", Desc = "Finish a race between 22:00 and 02:00.",
                Icon = "🦉", Category = "hidden", Hidden = true, Reward = 15,
                Check = Pred(x => HourIn(x, 22, 23, 0, 1)),
            },
            new AchievementDef
            {
                Id = "midnight-drift", Name = "Midnight Drift", Desc = "Finish a race between 02:00 and 05:00.",
                Icon = "🌙", Category = "hidden", Hidden = true, Reward = 15,
                Check = Pred(x => HourIn(x, 2, 3, 4)),
            },
            new AchievementDef
            {
                Id = "early-bird", Name = "Early Bird", Desc = "Finish a race between 05:00 and 08:00.",
                Icon = "🐦", Category = "hidden", Hidden = true, Reward = 15,
                Check = Pred(x => HourIn(x, 5, 6, 7)),
            },

            // Absolute DDK (per-instance DDK crowns are generated below)
            new AchievementDef
            {
                Id = "absolute-ddk", Name = "Absolute DDK", Desc = "Earn the crown on every track, both directions.",
                Icon = "👑", Category = "ddk", Hidden = true, Reward = 1000,
                Check = Pred(x => x.Content.AllInstanceIds.Count > 0 &&
                    x.Content.AllInstanceIds.All(id => RecordOrZero(x, id) >= Economy.DdkPps)),
            },
        };

        private static double RecordOrZero(AchievementContext x, string id)
        {
            return x.Records.TryGetValue(id, out double v) ? v : 0;
        }

        // ── Generated ladder families (lines 187-234) ─────────────────────────────
        private static List<AchievementDef> Ladder(string prefix, string icon, string category, double unit,
            (int Threshold, int Reward, string Name)[] tiers, Func<AchievementContext, double> valueOf,
            Func<int, string> descOf, Func<double, string> fmt)
        {
            var list = new List<AchievementDef>();
            foreach ((int threshold, int reward, string name) in tiers)
            {
                double target = threshold * unit;
                list.Add(new AchievementDef
                {
                    Id = prefix + "-" + threshold.ToString(CultureInfo.InvariantCulture),
                    Name = name, Icon = icon, Category = category, Hidden = false, Reward = reward,
                    Desc = descOf(threshold), Bar = true, Target = target, Fmt = fmt,
                    Check = x => CheckResult.WithProgress(valueOf(x), target),
                });
            }
            return list;
        }

        private static readonly (int Threshold, int Reward, string Name)[] DriftTiers =
        {
            (10, 20, "Warm-Up"), (25, 30, "Marathon"), (50, 40, "Endurance"),
            (100, 60, "Iron Wrists"), (250, 100, "Tireless"), (500, 150, "Eternal Drift"),
        };
        private static readonly (int Threshold, int Reward, string Name)[] RacesTiers =
        {
            (10, 15, "Rookie"), (50, 30, "Regular"), (100, 50, "Veteran"),
            (250, 100, "Devotee"), (500, 150, "Legend"),
        };
        private static readonly (int Threshold, int Reward, string Name)[] HoardTiers =
        {
            (500, 25, "Tire Saver"), (1000, 40, "Tire Lover"),
            (2500, 80, "Tire Warehouse"), (5000, 150, "Tire Factory"),
        };

        private static string FmtMin(double secs)
        {
            return Math.Floor(secs / 60).ToString("R", CultureInfo.InvariantCulture) + "m";
        }

        private static string FmtCount(double n)
        {
            return n.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Int(int n) { return n.ToString(CultureInfo.InvariantCulture); }

        private static readonly List<AchievementDef> Ladders = BuildLadders();

        private static List<AchievementDef> BuildLadders()
        {
            var all = new List<AchievementDef>();
            all.AddRange(Ladder("drift", "⏱️", "endurance", 60, DriftTiers, x => x.Lifetime.DriftSecs,
                m => "Drift for " + Int(m) + " minutes total.", FmtMin));
            all.AddRange(Ladder("races", "🔁", "dedication", 1, RacesTiers, x => x.Lifetime.Runs,
                n => "Finish " + Int(n) + " races.", FmtCount));
            all.AddRange(Ladder("hoard", "🛞", "wealth", 1, HoardTiers, x => x.Wallet,
                n => "Reach " + Int(n) + " tires in the wallet.", FmtCount));
            return all;
        }

        // Display order + section labels for the achievements page.
        public static readonly IReadOnlyList<(string Key, string Label)> CategoryOrder = new (string Key, string Label)[]
        {
            ("progression", "Progression"),
            ("skill", "Skill"),
            ("combo", "Combo"),
            ("endurance", "Endurance"),
            ("dedication", "Dedication"),
            ("wealth", "Wealth"),
            ("economy", "Economy"),
            ("hidden", "Secrets"),
            ("ddk", "Mastery"),
        };

        // Per-instance DDK crowns, generated from the content instance list (lines 235-240).
        private static List<AchievementDef> DdkDefs(ContentDescriptor content)
        {
            var list = new List<AchievementDef>();
            foreach (string instanceId in content.AllInstanceIds)
            {
                string id = instanceId;
                string display = content.Names.TryGetValue(id, out string? n) ? n : id;
                list.Add(new AchievementDef
                {
                    Id = "ddk-" + id, Name = "DDK " + display,
                    Desc = "Score 600+ PPS — a 6-star crown run.",
                    Icon = "👑", Category = "ddk", Hidden = true, Reward = 60,
                    Check = x => RecordOrZero(x, id) >= Economy.DdkPps ? CheckResult.True : CheckResult.False,
                });
            }
            return list;
        }

        // Build the `content` descriptor from the live registries (lines 245-260).
        public static ContentDescriptor BuildContent(IReadOnlyList<ContentTrack> tracks,
            IReadOnlyList<ContentShopItem> catalog, IReadOnlyList<ContentCar> cars)
        {
            var c = new ContentDescriptor();
            foreach (ContentTrack t in tracks) c.ForwardIds.Add(t.Id);
            foreach (string id in c.ForwardIds) c.ReversedIds.Add(id + ":rev");
            foreach (ContentTrack t in tracks)
            {
                c.Names[t.Id] = t.Name;
                c.Names[t.Id + ":rev"] = t.Name + " (reversed)";
            }
            c.AllInstanceIds.AddRange(c.ForwardIds);
            c.AllInstanceIds.AddRange(c.ReversedIds);
            c.ForwardTrackIds.AddRange(c.ForwardIds);
            foreach (ContentShopItem i in catalog)
                if (!c.ShopCategories.Contains(i.Kind)) c.ShopCategories.Add(i.Kind);
            foreach (ContentShopItem i in catalog) c.CatalogById[i.Id] = i.Kind;
            foreach (ContentCar car in cars)
                if (car.Price.HasValue && car.Price.Value != 0 && !double.IsNaN(car.Price.Value)) c.Cars.Add(car.Id);
            return c;
        }

        // Flatten store.records() ({ [inst]: { timeattack: { bestPPS } } }) → { [inst]: bestPPS } (lines 262-270).
        public static Dictionary<string, double> FlattenRecords(IReadOnlyDictionary<string, object?>? records)
        {
            var output = new Dictionary<string, double>();
            if (records == null) return output;
            foreach (KeyValuePair<string, object?> kv in records)
            {
                var rec = kv.Value as IReadOnlyDictionary<string, object?>;
                if (rec == null || !rec.TryGetValue("timeattack", out object? ta)) continue;
                var taDict = ta as IReadOnlyDictionary<string, object?>;
                if (taDict == null || !taDict.TryGetValue("bestPPS", out object? bp) || bp == null) continue;
                if (bp is double d) output[kv.Key] = d;
            }
            return output;
        }

        // The full catalog for a given world (line 275-278).
        public static List<AchievementDef> BuildCatalog(ContentDescriptor? content)
        {
            ContentDescriptor c = content ?? new ContentDescriptor();
            var all = new List<AchievementDef>();
            all.AddRange(Static);
            all.AddRange(Ladders);
            all.AddRange(DdkDefs(c));
            return all;
        }

        // The evaluator (lines 283-297). Pure; `unlocked` = ids already unlocked (skipped entirely).
        public static EvalResult Evaluate(AchievementContext ctx, ISet<string>? unlocked)
        {
            var result = new EvalResult();
            foreach (AchievementDef a in BuildCatalog(ctx.Content))
            {
                if (unlocked != null && unlocked.Contains(a.Id)) continue;
                CheckResult r = a.Check(ctx);
                if (r.HasProgress)
                {
                    result.Progress.Add(new ProgressEntry { Id = a.Id, Value = r.Progress });
                    if (r.Progress >= r.Target)
                        result.Unlocked.Add(new UnlockedEntry { Id = a.Id, Name = a.Name, Icon = a.Icon, Reward = a.Reward });
                }
                else if (r.Unlocked)
                {
                    result.Unlocked.Add(new UnlockedEntry { Id = a.Id, Name = a.Name, Icon = a.Icon, Reward = a.Reward });
                }
            }
            return result;
        }
    }
}
