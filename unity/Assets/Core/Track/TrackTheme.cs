// VERBATIM PORT – do not reorder (the class). Merge/ParseDefault are the Unity side of render.js `{ ...THEME_DEFAULT, ...T.theme }`.
// Source: DesktopDrift/js/track-factory.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    /// <summary>JS: track theme { background, table, tableEdge, track, skid, checkpoint, cone } – CSS colour strings.</summary>
    public sealed class TrackTheme
    {
        public string Background = "";
        public string Table = "";
        public string TableEdge = "";
        public string Track = "";
        public string Skid = "";
        public string Checkpoint = "";
        public string Cone = "";

        /// <summary>2D `{ ...THEME_DEFAULT, ...T.theme }`: per key a non-empty `over` value wins, else `def`; null `over` gives a copy of `def`.</summary>
        public static TrackTheme Merge(TrackTheme? over, TrackTheme def)
        {
            static string Pick(string? o, string d) => string.IsNullOrEmpty(o) ? d : o!;
            return new TrackTheme
            {
                Background = Pick(over?.Background, def.Background),
                Table = Pick(over?.Table, def.Table),
                TableEdge = Pick(over?.TableEdge, def.TableEdge),
                Track = Pick(over?.Track, def.Track),
                Skid = Pick(over?.Skid, def.Skid),
                Checkpoint = Pick(over?.Checkpoint, def.Checkpoint),
                Cone = Pick(over?.Cone, def.Cone),
            };
        }

        /// <summary>Reads THEME_DEFAULT from the exported render.json.</summary>
        public static TrackTheme ParseDefault(string renderJson)
        {
            var doc = (Dictionary<string, object?>)Json.Parse(renderJson)!;
            var d = (Dictionary<string, object?>)doc["THEME_DEFAULT"]!;
            return new TrackTheme
            {
                Background = (string)d["background"]!,
                Table = (string)d["table"]!,
                TableEdge = (string)d["tableEdge"]!,
                Track = (string)d["track"]!,
                Skid = (string)d["skid"]!,
                Checkpoint = (string)d["checkpoint"]!,
                Cone = (string)d["cone"]!,
            };
        }
    }
}
