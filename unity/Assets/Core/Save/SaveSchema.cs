// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/store.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System;
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    public sealed class LoadResult
    {
        public SaveState State = null!;
        public bool Normalized; // true => a saved object was merged/stamped; the host should persist it (JS: save())
    }

    public static class SaveSchema
    {
        // 3D edition schema v1 = the shape of store.js defaults() at VERSION 4 (D-17: 2D migrations not ported)
        public const int Version = 1;

        // store.js:27-42 defaults(). Every number is a double (the JSON tree's only numeric type).
        public static Dictionary<string, object?> Defaults()
        {
            return new Dictionary<string, object?>
            {
                ["version"] = (double)Version,
                ["settings"] = new Dictionary<string, object?>
                {
                    ["units"] = "kmh", ["haptics"] = true, ["soundEnabled"] = true, ["volume"] = 1.0,
                },
                ["garage"] = new Dictionary<string, object?>
                {
                    ["carIndex"] = 0.0, ["cars"] = new Dictionary<string, object?>(),
                },
                ["records"] = new Dictionary<string, object?>(),
                ["achievements"] = new Dictionary<string, object?>(),
                ["wallet"] = 0.0,
                ["ledger"] = new List<object?>(),
                ["owned"] = new List<object?>(),
                ["ownedCars"] = new List<object?>(),
                ["stats"] = new Dictionary<string, object?>
                {
                    ["caps"] = new Dictionary<string, object?>(),
                    ["tires"] = new Dictionary<string, object?>(),
                    ["tiresSwept"] = new List<object?>(),
                    ["trophies"] = new List<object?>(),
                    ["cleared"] = new List<object?>(),
                    ["runs"] = 0.0,
                    ["driftSecs"] = 0.0,
                },
            };
        }

        // store.js:100 _isObj
        public static bool IsObj(object? v)
        {
            return v is Dictionary<string, object?>;
        }

        // Breaking-change migrations, keyed by the target version. Append-only: new migrations are
        // appended from version 2 on (the 2D v2-v4 migrations are intentionally absent, D-17).
        public static readonly SortedDictionary<int, Func<Dictionary<string, object?>, Dictionary<string, object?>>> Migrations =
            new SortedDictionary<int, Func<Dictionary<string, object?>, Dictionary<string, object?>>>();

        // store.js:102-112 _merge. `base` defines the shape: object slot -> recurse if the save is an object,
        // else KEEP the default; leaf slot -> take the saved value; keys only in the save are preserved.
        public static object? Merge(object? @base, object? over)
        {
            var b = @base as Dictionary<string, object?>;
            var o = over as Dictionary<string, object?>;
            if (b == null || o == null) return over;
            var output = new Dictionary<string, object?>(b);
            foreach (KeyValuePair<string, object?> kv in o)
            {
                output[kv.Key] = b.TryGetValue(kv.Key, out object? slot) && IsObj(slot)
                    ? (IsObj(kv.Value) ? Merge(slot, kv.Value) : slot)
                    : kv.Value;
            }
            return output;
        }

        // store.js:114-119 _migrate: run the chain from the saved version up to Version.
        public static Dictionary<string, object?> Migrate(Dictionary<string, object?> raw)
        {
            Dictionary<string, object?> s = raw;
            int from = 0;
            if (raw.TryGetValue("version", out object? ver) && ver is double dv && dv == Math.Floor(dv)
                && Math.Abs(dv) < int.MaxValue)
                from = (int)dv;
            for (int v = from + 1; v <= Version; v++)
                if (Migrations.TryGetValue(v, out var fn)) s = fn(s);
            return s;
        }

        // store.js:123-132 _ensure. Unparseable / non-object input => fresh defaults (Normalized = false).
        // Otherwise merge over defaults, stamp Version (an unknown/future version keeps its data) and
        // flag Normalized so the host persists the normalised shape (JS: save()).
        public static LoadResult Load(string? json)
        {
            object? raw = null;
            try { raw = Json.Parse(string.IsNullOrEmpty(json) ? "null" : json!); } catch (FormatException) { }
            var rawObj = raw as Dictionary<string, object?>;
            if (rawObj == null)
                return new LoadResult { State = new SaveState(Defaults()), Normalized = false };
            var merged = (Dictionary<string, object?>)Merge(Defaults(), Migrate(rawObj))!;
            merged["version"] = (double)Version;
            return new LoadResult { State = new SaveState(merged), Normalized = true };
        }
    }
}
