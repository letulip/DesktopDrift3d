// Exact JSON round trip of a TrackData (every field), so tracks can be stored as data assets and rebuilt at runtime
// (decision D-25: no per-track prefabs). Doubles are written by Json.Serialize (CoreMath.JsNumberToString, shortest
// round-trip) and read back bit-identically; Serialize(Deserialize(s)) == s.
// Layout follows TrackData.cs / track-factory.js @ 5556140; written for the 3D port 2026-10-29.
#nullable enable
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    public static class TrackDataJson
    {
        public static string Serialize(TrackData t)
        {
            var d = new Dictionary<string, object?>
            {
                ["id"] = t.Id,
                ["laps"] = t.Laps,
                ["reversed"] = t.Reversed,
                ["trackHalf"] = t.TrackHalf,
                ["coneR"] = t.ConeR,
                ["k"] = t.K,
                ["cpR"] = t.CpR,
                ["table"] = new Dictionary<string, object?> { ["w"] = t.Table.W, ["h"] = t.Table.H, ["round"] = t.Table.Round },
                ["theme"] = new Dictionary<string, object?>
                {
                    ["background"] = t.Theme.Background, ["table"] = t.Theme.Table, ["tableEdge"] = t.Theme.TableEdge,
                    ["track"] = t.Theme.Track, ["skid"] = t.Theme.Skid, ["checkpoint"] = t.Theme.Checkpoint, ["cone"] = t.Theme.Cone,
                },
                ["startPos"] = Pt(t.StartPos),
                ["startAngle"] = t.StartAngle,
                ["center"] = Pts(t.Center),
                ["outer"] = Pts(t.Outer),
                ["inner"] = Pts(t.Inner),
                ["checkpoints"] = Pts(t.Checkpoints),
            };
            var cones = new List<object?>();
            foreach (Cone c in t.Cones)
                cones.Add(new Dictionary<string, object?>
                {
                    ["x"] = c.X, ["y"] = c.Y, ["x0"] = c.X0, ["y0"] = c.Y0, ["vx"] = c.Vx, ["vy"] = c.Vy,
                    ["ang"] = c.Ang, ["spin"] = c.Spin, ["knocked"] = c.Knocked, ["ci"] = c.Ci,
                });
            d["cones"] = cones;
            var props = new List<object?>();
            foreach (Prop p in t.Props)
                props.Add(new Dictionary<string, object?>
                {
                    ["x"] = p.X, ["y"] = p.Y, ["ang"] = p.Ang, ["hl"] = p.Hl, ["r"] = p.R,
                    ["kind"] = p.Kind, ["cos"] = p.Cos, ["sin"] = p.Sin,
                });
            d["props"] = props;
            var cols = new List<object?>();
            foreach (Collectible c in t.Collectibles)
                cols.Add(new Dictionary<string, object?>
                {
                    ["kind"] = c.Kind, ["x"] = c.X, ["y"] = c.Y, ["r"] = c.R, ["value"] = c.Value, ["capId"] = c.CapId, ["hl"] = c.Hl,
                });
            d["collectibles"] = cols;
            return Json.Serialize(d);
        }

        public static TrackData Deserialize(string json)
        {
            var d = O(Json.Parse(json));
            var t = new TrackData
            {
                Id = S(d["id"]),
                Laps = I(d["laps"]),
                Reversed = (bool)d["reversed"]!,
                TrackHalf = N(d["trackHalf"]),
                ConeR = N(d["coneR"]),
                K = I(d["k"]),
                CpR = N(d["cpR"]),
                StartPos = ReadPt(d["startPos"]),
                StartAngle = N(d["startAngle"]),
            };
            var tb = O(d["table"]);
            t.Table = new TableRect { W = N(tb["w"]), H = N(tb["h"]), Round = (bool)tb["round"]! };
            var th = O(d["theme"]);
            t.Theme = new TrackTheme
            {
                Background = S(th["background"]), Table = S(th["table"]), TableEdge = S(th["tableEdge"]), Track = S(th["track"]),
                Skid = S(th["skid"]), Checkpoint = S(th["checkpoint"]), Cone = S(th["cone"]),
            };
            t.Center = ReadPts(d["center"]);
            t.Outer = ReadPts(d["outer"]);
            t.Inner = ReadPts(d["inner"]);
            t.Checkpoints = ReadPts(d["checkpoints"]);
            foreach (object? o in A(d["cones"]))
            {
                var c = O(o);
                t.Cones.Add(new Cone
                {
                    X = N(c["x"]), Y = N(c["y"]), X0 = N(c["x0"]), Y0 = N(c["y0"]), Vx = N(c["vx"]), Vy = N(c["vy"]),
                    Ang = N(c["ang"]), Spin = N(c["spin"]), Knocked = (bool)c["knocked"]!, Ci = I(c["ci"]),
                });
            }
            foreach (object? o in A(d["props"]))
            {
                var p = O(o);
                t.Props.Add(new Prop
                {
                    X = N(p["x"]), Y = N(p["y"]), Ang = N(p["ang"]), Hl = N(p["hl"]), R = N(p["r"]),
                    Kind = S(p["kind"]), Cos = N(p["cos"]), Sin = N(p["sin"]),
                });
            }
            foreach (object? o in A(d["collectibles"]))
            {
                var c = O(o);
                t.Collectibles.Add(new Collectible
                {
                    Kind = S(c["kind"]), X = N(c["x"]), Y = N(c["y"]), R = N(c["r"]), Value = I(c["value"]),
                    CapId = S(c["capId"]), Hl = N(c["hl"]),
                });
            }
            return t;
        }

        private static List<object?> Pt(Vec2 v) { return new List<object?> { v.X, v.Y }; }

        private static List<object?> Pts(List<Vec2> l)
        {
            var r = new List<object?>(l.Count);
            foreach (Vec2 v in l) r.Add(Pt(v));
            return r;
        }

        private static Vec2 ReadPt(object? o)
        {
            var a = A(o);
            return new Vec2(N(a[0]), N(a[1]));
        }

        private static List<Vec2> ReadPts(object? o)
        {
            var a = A(o);
            var r = new List<Vec2>(a.Count);
            foreach (object? p in a) r.Add(ReadPt(p));
            return r;
        }

        private static double N(object? o) { return (double)o!; }
        private static int I(object? o) { return (int)(double)o!; }
        private static string S(object? o) { return (string)o!; }
        private static Dictionary<string, object?> O(object? o) { return (Dictionary<string, object?>)o!; }
        private static List<object?> A(object? o) { return (List<object?>)o!; }
    }
}
