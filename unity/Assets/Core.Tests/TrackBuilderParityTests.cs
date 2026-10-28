#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    // Gate: TrackBuilder.FromSvg rebuilds each track from the 2D game's SVG and matches tools/tracks-ref/<id>[.rev].json
    // (produced by tools/export-tracks.mjs from the 2D game's own makeTrack) within 1e-6.
    [TestFixture]
    public class TrackBuilderParityTests
    {
        private const double Eps = 1e-6;

        private static string DesktopDriftDir()
        {
            string dd = Environment.GetEnvironmentVariable("DESKTOPDRIFT_DIR") ?? "/Users/letulip/Projects/Claude/DesktopDrift";
            if (!Directory.Exists(dd))
                Assert.Ignore("!!! DesktopDrift 2D source directory not found: '" + dd + "' (set DESKTOPDRIFT_DIR) – TRACK PARITY NOT CHECKED !!!");
            return dd;
        }

        private static Dictionary<string, object?> JsonDoc(string relative)
        {
            return (Dictionary<string, object?>)Json.Parse(File.ReadAllText(TestPaths.Locate(relative)))!;
        }

        private static double D(object? o) { return (double)o!; }
        private static string S(object? o) { return (string)o!; }
        private static Dictionary<string, object?> O(object? o) { return (Dictionary<string, object?>)o!; }
        private static List<object?> A(object? o) { return (List<object?>)o!; }


        private static TrackData Build(string id)
        {
            string dd = DesktopDriftDir();
            Dictionary<string, object?>? row = null;
            foreach (object? t in A(JsonDoc("unity/Assets/Content/_json/tracks.json")["TRACKS"]))
                if (S(O(t)["id"]) == id) row = O(t);
            Assert.That(row, Is.Not.Null, "track '" + id + "' missing in _json/tracks.json");

            var th = O(row!["theme"]);
            string Th(string k) { return th.TryGetValue(k, out object? v) ? S(v) : ""; }
            var theme = new TrackTheme
            {
                Background = Th("background"), Table = Th("table"), TableEdge = Th("tableEdge"), Track = Th("track"),
                Skid = Th("skid"), Checkpoint = Th("checkpoint"), Cone = Th("cone"),
            };

            var items = new Dictionary<string, ItemDef>();
            foreach (KeyValuePair<string, object?> kv in JsonDoc("unity/Assets/Content/_json/items.json"))
            {
                if (!kv.Key.StartsWith("ITEM_", StringComparison.Ordinal)) continue;
                var o = O(kv.Value);
                items[kv.Key] = new ItemDef { Hl = D(o["hl"]), R = D(o["r"]), Kind = S(o["kind"]) };
            }

            var col = JsonDoc("unity/Assets/Content/_json/collectibles.json");
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

            string svg = File.ReadAllText(Path.Combine(dd, "tracks", id + ".svg"));
            return TrackBuilder.FromSvg(svg, id, (int)D(row["laps"]), (int)D(row["tires"]), theme, items, Proto("COLA_CAP"), Proto("TIRE"));
        }

        private static void Near(string id, string field, int i, double expected, double actual)
        {
            Assert.That(Math.Abs(expected - actual) <= Eps,
                id + " " + field + "[" + i + "]: JS=" + expected.ToString("R", CultureInfo.InvariantCulture) +
                " C#=" + actual.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void Count(string id, string field, int expected, int actual)
        {
            Assert.That(actual, Is.EqualTo(expected), id + " " + field + ": count JS=" + expected + " C#=" + actual);
        }

        private static void Points(string id, string field, List<object?> js, IReadOnlyList<Vec2> cs)
        {
            Count(id, field, js.Count, cs.Count);
            for (int i = 0; i < js.Count; i++)
            {
                Near(id, field + ".x", i, D(O(js[i])["x"]), cs[i].X);
                Near(id, field + ".y", i, D(O(js[i])["y"]), cs[i].Y);
            }
        }

        private static void AssertMatches(string id, TrackData T, string refRelative)
        {
            var r = JsonDoc(refRelative);
            Points(id, "center", A(r["center"]), T.Center);
            Points(id, "outer", A(r["outer"]), T.Outer);
            Points(id, "inner", A(r["inner"]), T.Inner);
            Points(id, "checkpoints", A(r["checkpoints"]), T.Checkpoints);

            var table = O(r["TABLE"]);
            Near(id, "TABLE.w", 0, D(table["w"]), T.Table.W);
            Near(id, "TABLE.h", 0, D(table["h"]), T.Table.H);
            Assert.That(T.Table.Round, Is.EqualTo(S(table["shape"]) != "rect"), id + " TABLE.shape JS=" + S(table["shape"]));

            var cones = A(r["cones"]);
            Count(id, "cones", cones.Count, T.Cones.Count);
            for (int i = 0; i < cones.Count; i++)
            {
                var c = O(cones[i]);
                Near(id, "cones.x", i, D(c["x"]), T.Cones[i].X);
                Near(id, "cones.y", i, D(c["y"]), T.Cones[i].Y);
                Near(id, "cones.ci", i, D(c["ci"]), T.Cones[i].Ci);
            }

            var props = A(r["props"]);
            Count(id, "props", props.Count, T.Props.Count);
            for (int i = 0; i < props.Count; i++)
            {
                var p = O(props[i]);
                Near(id, "props.x", i, D(p["x"]), T.Props[i].X);
                Near(id, "props.y", i, D(p["y"]), T.Props[i].Y);
                Near(id, "props.ang", i, D(p["ang"]), T.Props[i].Ang);
                Near(id, "props.hl", i, D(p["hl"]), T.Props[i].Hl);
                Near(id, "props.r", i, D(p["r"]), T.Props[i].R);
                Assert.That(T.Props[i].Kind, Is.EqualTo(S(p["kind"])), id + " props.kind[" + i + "]");
                Near(id, "props._cos", i, D(p["_cos"]), T.Props[i].Cos);
                Near(id, "props._sin", i, D(p["_sin"]), T.Props[i].Sin);
            }

            var cols = A(r["collectibles"]);
            Count(id, "collectibles", cols.Count, T.Collectibles.Count);
            for (int i = 0; i < cols.Count; i++)
            {
                var c = O(cols[i]);
                Assert.That(T.Collectibles[i].Kind, Is.EqualTo(S(c["kind"])), id + " collectibles.kind[" + i + "]");
                Near(id, "collectibles.x", i, D(c["x"]), T.Collectibles[i].X);
                Near(id, "collectibles.y", i, D(c["y"]), T.Collectibles[i].Y);
                Assert.That(T.Collectibles[i].CapId, Is.EqualTo(S(c["capId"])), id + " collectibles.capId[" + i + "]");
            }

            var sp = O(r["startPos"]);
            Near(id, "startPos.x", 0, D(sp["x"]), T.StartPos.X);
            Near(id, "startPos.y", 0, D(sp["y"]), T.StartPos.Y);
            Near(id, "startAngle", 0, D(r["startAngle"]), T.StartAngle);

            var sum = O(r["summary"]);
            Count(id, "summary.center", (int)D(sum["center"]), T.Center.Count);
            Count(id, "summary.cones", (int)D(sum["cones"]), T.Cones.Count);
            Count(id, "summary.props", (int)D(sum["props"]), T.Props.Count);
            Count(id, "summary.collectibles", (int)D(sum["collectibles"]), T.Collectibles.Count);
            Count(id, "summary.checkpoints", (int)D(sum["checkpoints"]), T.Checkpoints.Count);
        }

        [TestCase("green-study")]
        [TestCase("steel-kitchen")]
        [TestCase("workbench")]
        [TestCase("cafe-marble")]
        [TestCase("dev-desk")]
        [TestCase("dining-oak")]
        public void FromSvg_MatchesJsReference(string id)
        {
            AssertMatches(id, Build(id), "tools/tracks-ref/" + id + ".json");
        }

        [TestCase("green-study")]
        [TestCase("steel-kitchen")]
        [TestCase("workbench")]
        [TestCase("cafe-marble")]
        [TestCase("dev-desk")]
        [TestCase("dining-oak")]
        public void ReverseTrack_MatchesJsReference(string id)
        {
            AssertMatches(id, TrackUtil.ReverseTrack(Build(id)), "tools/tracks-ref/" + id + ".rev.json");
        }
    }
}
