// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/track-factory.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace DesktopDrift.Core
{
    /// <summary>JS: makeTrack (SVG text -> track), minus the fetch: the caller supplies the SVG text and the item tables.</summary>
    public static class TrackBuilder
    {
        public const double TrackHalf = 100;          // track-factory.js: TRACK_HALF
        public const double ConeR = 9;                // track-factory.js: CONE_R
        public const int K = 8;                       // track-factory.js: K
        public const double CpR = TrackHalf + 70;     // track-factory.js: CP_R
        public const double TableMargin = 250;        // track-factory.js: TABLE_MARGIN

        // JS Number(x) / parseFloat(x) on the strings the SVG gives us: anything unparsable is NaN.
        private static double Num(string? s)
        {
            if (s == null) return double.NaN;
            return double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN;
        }

        private static string Attr(XmlElement el, string name)
        {
            return el.HasAttribute(name) ? el.GetAttribute(name) : "";
        }

        // JS template-literal formatting of an integer-valued double (-0 prints "0").
        private static string JsInt(double v)
        {
            return v == 0 ? "0" : ((long)v).ToString(CultureInfo.InvariantCulture);
        }

        // Resolve a proxy-line layer ID to an items key:
        // 1) direct match; 2) strip trailing _N instance suffix (ITEM_PENCIL_2 -> ITEM_PENCIL)
        // track-factory.js: resolveKey
        private static string? ResolveKey(string rawId, IReadOnlyDictionary<string, ItemDef> items)
        {
            if (items.ContainsKey(rawId)) return rawId;
            string stripped = Regex.Replace(rawId, @"_\d+$", "", RegexOptions.ECMAScript);
            return items.ContainsKey(stripped) ? stripped : null;
        }

        private static Collectible Copy(Collectible p, double x, double y, string capId)
        {
            return new Collectible { Kind = p.Kind, X = x, Y = y, R = p.R, Value = p.Value, CapId = capId, Hl = p.Hl };
        }

        public static TrackData FromSvg(string svgText, string id, int laps, int tires, TrackTheme theme,
            IReadOnlyDictionary<string, ItemDef> items, Collectible colaProto, Collectible tireProto, double scale = 0.25)
        {
            var doc = new XmlDocument();
            doc.LoadXml(svgText);

            // Game origin – derived from viewBox
            string[] vb = Regex.Split((doc.DocumentElement!.GetAttribute("viewBox") ?? "").Trim(), @"\s+", RegexOptions.ECMAScript);
            double svgCx = Num(vb[2]) / 2, svgCy = Num(vb[3]) / 2;
            Vec2 ToGame(double x, double y) => new Vec2((x - svgCx) * scale, -(y - svgCy) * scale);

            // Document-order walk: the track path and the ITEM_ proxy elements.
            XmlElement? trackPath = null;
            var itemEls = new List<XmlElement>();
            foreach (XmlNode n in doc.GetElementsByTagName("*"))
            {
                var el = (XmlElement)n;
                string elId = el.GetAttribute("id");
                if (trackPath == null && elId == "track_path") trackPath = el;
                if ((el.LocalName == "line" || el.LocalName == "path") && elId.StartsWith("ITEM_", StringComparison.Ordinal))
                    itemEls.Add(el);
            }

            // Centreline
            List<Vec2> rawVerts = TrackUtil.ParseSvgPath(trackPath!.GetAttribute("d"));
            var smoothPoly = new List<Vec2>();
            foreach (Vec2 v in rawVerts) smoothPoly.Add(ToGame(v.X, v.Y));
            for (int i = 0; i < 4; i++) smoothPoly = TrackUtil.Chaikin(smoothPoly);

            TrackUtil.Edges e = TrackUtil.OffsetEdges(smoothPoly, TrackHalf);
            List<Vec2> center = e.Center, outer = e.Outer, inner = e.Inner;
            // Cull edge cones that a self-intersection pushed into the middle of another lane.
            List<Cone> cones = TrackUtil.FilterConesOnTrack(TrackUtil.PlaceCones(outer, inner), center, TrackHalf);

            // TABLE
            double maxX = 0, maxY = 0;
            foreach (Vec2 o in outer) { maxX = Math.Max(maxX, Math.Abs(o.X)); maxY = Math.Max(maxY, Math.Abs(o.Y)); }
            var table = new TableRect
            {
                W = CoreMath.JsRound((maxX + TableMargin) * 2),
                H = CoreMath.JsRound((maxY + TableMargin) * 2),
                Round = false,
            };

            // Items from proxy lines (line and path elements)
            var props = new List<Prop>();
            var collectibles = new List<Collectible>();

            foreach (XmlElement el in itemEls)
            {
                string rawId = el.GetAttribute("id");

                // Cola cap / tire – collectibles, not physics props
                if (rawId == "ITEM_COLA_CAP" || rawId == "ITEM_TIRE")
                {
                    double x1 = Num(Attr(el, "x1")), y1 = Num(Attr(el, "y1"));
                    double x2 = Num(Attr(el, "x2")), y2 = Num(Attr(el, "y2"));
                    Vec2 g = ToGame((x1 + x2) / 2, (y1 + y2) / 2);
                    double cx = CoreMath.JsRound(g.X), cy = CoreMath.JsRound(g.Y);
                    if (double.IsNaN(cx) || double.IsInfinity(cx) || double.IsNaN(cy) || double.IsInfinity(cy)) continue;
                    collectibles.Add(Copy(rawId == "ITEM_COLA_CAP" ? colaProto : tireProto, cx, cy, JsInt(cx) + "," + JsInt(cy)));
                    continue;
                }

                string? key = ResolveKey(rawId, items);
                if (key == null) continue;
                ItemDef item = items[key];

                // Midpoint coordinates – handle both <line> and <path d="M x1 y1 L x2 y2"> proxies
                double px1, py1, px2, py2;
                if (el.LocalName == "path")
                {
                    Match m = Regex.Match(Attr(el, "d"), @"M\s*([\d.]+)\s+([\d.]+)\s*L\s*([\d.]+)\s+([\d.]+)", RegexOptions.ECMAScript);
                    if (!m.Success) continue;
                    px1 = Num(m.Groups[1].Value); py1 = Num(m.Groups[2].Value);
                    px2 = Num(m.Groups[3].Value); py2 = Num(m.Groups[4].Value);
                }
                else
                {
                    px1 = Num(Attr(el, "x1")); py1 = Num(Attr(el, "y1"));
                    px2 = Num(Attr(el, "x2")); py2 = Num(Attr(el, "y2"));
                }

                Vec2 gp = ToGame((px1 + px2) / 2, (py1 + py2) / 2);
                // Skip malformed proxy lines whose coords aren't finite (e.g. an un-baked transform).
                if (double.IsNaN(gp.X) || double.IsInfinity(gp.X) || double.IsNaN(gp.Y) || double.IsInfinity(gp.Y)) continue;
                double ang = Math.Atan2(-(py2 - py1), px2 - px1);
                // JS parseFloat(ang.toFixed(3)) – exact toFixed on the binary value.
                double ang3 = CoreMath.JsToFixedNumber(ang, 3);
                props.Add(Prop.Prepared(CoreMath.JsRound(gp.X), CoreMath.JsRound(gp.Y), ang3, item.Hl, item.R, item.Kind));
            }

            // Tire coins – algorithmically seeded; persistence key is the stable seed INDEX (t0...).
            List<Vec2> seeded = TireSeed.SeedTires(center, inner, outer, tires);
            for (int k = 0; k < seeded.Count; k++)
                collectibles.Add(Copy(tireProto, seeded[k].X, seeded[k].Y, "t" + k.ToString(CultureInfo.InvariantCulture)));

            // Checkpoints + start position
            List<Vec2> checkpoints = TrackUtil.SampleCheckpointsByCorner(center, K);
            Vec2 c0 = center[0], c1 = center[1];

            return new TrackData
            {
                TrackHalf = TrackHalf, ConeR = ConeR, K = K, CpR = CpR,
                Center = center, Outer = outer, Inner = inner, Cones = cones, Table = table,
                Props = props, Collectibles = collectibles, Checkpoints = checkpoints,
                StartPos = new Vec2(c0.X, c0.Y),
                StartAngle = Math.Atan2(c1.Y - c0.Y, c1.X - c0.X),
                Id = id, Laps = laps, Theme = theme,
            };
        }
    }
}
