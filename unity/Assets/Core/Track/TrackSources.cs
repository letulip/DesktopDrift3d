// Builds a TrackData from content TEXTS (no file IO): the track SVG plus the exported _json texts
// tracks.json / items.json / collectibles.json (tools/export-data.mjs). Same logic the parity tests used to
// inline; the Unity importer (step 1b) and the tests share this one implementation.
// Source: DesktopDrift/js/track-factory.js @ 5556140 (makeTrack inputs), assembled for the 3D port 2026-10-29.
#nullable enable
using System;
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    public static class TrackSources
    {
        /// <summary>Track ids of tracks.json (the TRACKS array) in file order.</summary>
        public static IReadOnlyList<string> TrackIds(string tracksJson)
        {
            var ids = new List<string>();
            foreach (object? t in Rows(tracksJson)) ids.Add(S(O(t)["id"]));
            return ids;
        }

        /// <summary>makeTrack over texts: theme/laps/tires from the tracks.json row, ITEM_* defs from items.json,
        /// COLA_CAP and TIRE prototypes from collectibles.json. Unknown id => ArgumentException.</summary>
        public static TrackData BuildFromContent(string svgText, string id, string tracksJson, string itemsJson, string collectiblesJson)
        {
            Dictionary<string, object?>? row = null;
            foreach (object? t in Rows(tracksJson))
                if (S(O(t)["id"]) == id) { row = O(t); break; }
            if (row == null) throw new ArgumentException("track '" + id + "' missing in tracks.json", nameof(id));

            var th = O(row["theme"]);
            string Th(string k) { return th.TryGetValue(k, out object? v) ? S(v) : ""; }
            var theme = new TrackTheme
            {
                Background = Th("background"), Table = Th("table"), TableEdge = Th("tableEdge"), Track = Th("track"),
                Skid = Th("skid"), Checkpoint = Th("checkpoint"), Cone = Th("cone"),
            };

            var items = new Dictionary<string, ItemDef>();
            foreach (KeyValuePair<string, object?> kv in O(Json.Parse(itemsJson)))
            {
                if (!kv.Key.StartsWith("ITEM_", StringComparison.Ordinal)) continue;
                var o = O(kv.Value);
                items[kv.Key] = new ItemDef { Hl = D(o["hl"]), R = D(o["r"]), Kind = S(o["kind"]) };
            }

            var col = O(Json.Parse(collectiblesJson));
            Collectible Proto(string key)
            {
                var o = O(col[key]);
                return new Collectible
                {
                    Kind = S(o["kind"]),
                    R = D(o["r"]),
                    Hl = o.TryGetValue("hl", out object? hl) ? D(hl) : 0,
                    Value = o.TryGetValue("value", out object? val) ? (int)D(val) : 0,
                };
            }

            return TrackBuilder.FromSvg(svgText, id, (int)D(row["laps"]), (int)D(row["tires"]), theme, items, Proto("COLA_CAP"), Proto("TIRE"));
        }

        private static List<object?> Rows(string tracksJson)
        {
            return A(O(Json.Parse(tracksJson))["TRACKS"]);
        }

        private static double D(object? o) { return (double)o!; }
        private static string S(object? o) { return (string)o!; }
        private static Dictionary<string, object?> O(object? o) { return (Dictionary<string, object?>)o!; }
        private static List<object?> A(object? o) { return (List<object?>)o!; }
    }
}
