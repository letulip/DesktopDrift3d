// SPEC-03 step 6: the 2D track-select thumbnail (js/track-thumb.js drawThumb) as a UI Toolkit element for SPEC-06's track screens.
// It replays the canvas command list produced by TrackThumb.Commands (verbatim port, proven equal to the JS) with Painter2D:
// vector, crisp at any scale, uniformly scaled from the 260x140 canvas space and centred in contentRect. NO PNG, and NO mirrored
// thumbnail for reversed tracks – feed it TrackThumb.ForInstance(forwardTrack, reversed) (same list for both directions).
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using DesktopDrift.Core;
using UnityEngine;
using UnityEngine.UIElements;

[assembly: InternalsVisibleTo("DesktopDrift.Game.EditModeTests")]

namespace DesktopDrift.Game
{
    public sealed class TrackThumbElement : VisualElement
    {
        IReadOnlyList<ThumbCommand> cmds = Array.Empty<ThumbCommand>();
        double canvasW = TrackThumb.CanvasWidth, canvasH = TrackThumb.CanvasHeight;

        public TrackThumbElement() { generateVisualContent += Draw; }

        public void Set(IReadOnlyList<ThumbCommand> commands, double width, double height)
        {
            cmds = commands;
            canvasW = width;
            canvasH = height;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext mgc)
        {
            Rect r = contentRect;
            if (cmds.Count == 0 || r.width <= 0f || r.height <= 0f || canvasW <= 0 || canvasH <= 0) return;
            double scale = Math.Min(r.width / canvasW, r.height / canvasH);
            double ox = r.xMin + (r.width - canvasW * scale) / 2, oy = r.yMin + (r.height - canvasH * scale) / 2;
            Replay(cmds, scale, ox, oy, new PainterSink(mgc.painter2D));
        }

        /// <summary>The Painter2D operations the replay issues, as strings (testable without a MeshGenerationContext).</summary>
        internal static IEnumerable<string> Plan(IReadOnlyList<ThumbCommand> cmds, double scale)
        {
            var sink = new PlanSink();
            Replay(cmds, scale, 0, 0, sink);
            return sink.Ops;
        }

        // Painter2D antialiases the seams inside one stroke, so on the thumbnail's dense polyline (a point every ~3 px) the
        // table showed through as dark hairlines on the outer side of every bend (miter joins too). A round-join stroke is
        // therefore drawn as a union of opaque fills that overlap instead of abutting, so only its outline is antialiased:
        // one quad per segment, reaching past each point to the miter of a gentle turn (at most 2.4% of the half width
        // outside the round join up to 25 degrees) plus SeamOverlap, and a disc where the turn is sharper or at a round cap.
        internal const double GentleTurnDeg = 25;
        internal const double SeamOverlap = 1;   // px each quad reaches past its point so that neighbours overlap

        internal static void StrokeAsFills(IReadOnlyList<Vector2> path, bool closed, double half, bool roundCaps,
            List<Vector2[]> quads, List<Vector2> discs)
        {
            var pts = new List<Vector2>();
            foreach (Vector2 v in path)
                if (pts.Count == 0 || (v - pts[pts.Count - 1]).sqrMagnitude > 1e-6f) pts.Add(v);
            if (closed && pts.Count > 2 && (pts[0] - pts[pts.Count - 1]).sqrMagnitude <= 1e-6f) pts.RemoveAt(pts.Count - 1);
            int m = pts.Count;
            if (m < 2) return;
            bool loop = closed && m > 2;
            var ext = new double[m];
            for (int j = 0; j < m; j++)
            {
                if (!loop && (j == 0 || j == m - 1))
                {
                    if (roundCaps) discs.Add(pts[j]);
                    continue;
                }
                Vector2 a = pts[(j - 1 + m) % m], b = pts[j], c = pts[(j + 1) % m];
                double ax = b.x - a.x, ay = b.y - a.y, bx = c.x - b.x, by = c.y - b.y;
                double cos = (ax * bx + ay * by) / (Math.Sqrt(ax * ax + ay * ay) * Math.Sqrt(bx * bx + by * by));
                double turn = Math.Acos(Math.Max(-1, Math.Min(1, cos)));
                if (turn <= GentleTurnDeg * Math.PI / 180) ext[j] = half * Math.Tan(turn / 2) + SeamOverlap;
                else { ext[j] = SeamOverlap; discs.Add(b); }
            }
            int segs = loop ? m : m - 1;
            for (int i = 0; i < segs; i++)
            {
                int k = (i + 1) % m;
                Vector2 a = pts[i], b = pts[k];
                double dx = b.x - a.x, dy = b.y - a.y, len = Math.Sqrt(dx * dx + dy * dy);
                dx /= len; dy /= len;
                double nx = -dy * half, ny = dx * half;
                double sx = a.x - dx * ext[i], sy = a.y - dy * ext[i], ex = b.x + dx * ext[k], ey = b.y + dy * ext[k];
                quads.Add(new[] { P(sx + nx, sy + ny), P(ex + nx, ey + ny), P(ex - nx, ey - ny), P(sx - nx, sy - ny) });
            }
        }

        static Vector2 P(double x, double y) { return new Vector2((float)x, (float)y); }

        internal interface ISink
        {
            void FillColor(string css);
            void StrokeColor(string css);
            void LineWidth(double w);
            void LineCap(string cap);
            void LineJoin(string join);
            void FillRect(double x, double y, double w, double h);
            void BeginPath();
            void RoundRect(double x, double y, double w, double h, double r);
            void MoveTo(double x, double y);
            void LineTo(double x, double y);
            void ClosePath();
            void Fill();
            void Stroke();
        }

        // The one interpreter of the command list; coordinates are canvas space * scale + (ox, oy).
        internal static void Replay(IReadOnlyList<ThumbCommand> cmds, double scale, double ox, double oy, ISink p)
        {
            foreach (ThumbCommand c in cmds)
            {
                double[] a = c.Args;
                switch (c.Name)
                {
                    case "set:fillStyle": p.FillColor(c.Value!); break;
                    case "set:strokeStyle": p.StrokeColor(c.Value!); break;
                    case "set:lineWidth": p.LineWidth(a[0] * scale); break;
                    case "set:lineCap": p.LineCap(c.Value!); break;
                    case "set:lineJoin": p.LineJoin(c.Value!); break;
                    case "fillRect": p.FillRect(a[0] * scale + ox, a[1] * scale + oy, a[2] * scale, a[3] * scale); break;
                    case "beginPath": p.BeginPath(); break;
                    case "roundRect": p.RoundRect(a[0] * scale + ox, a[1] * scale + oy, a[2] * scale, a[3] * scale, a[4] * scale); break;
                    case "moveTo": p.MoveTo(a[0] * scale + ox, a[1] * scale + oy); break;
                    case "lineTo": p.LineTo(a[0] * scale + ox, a[1] * scale + oy); break;
                    case "closePath": p.ClosePath(); break;
                    case "fill": p.Fill(); break;
                    case "stroke": p.Stroke(); break;
                    default: throw new ArgumentException("Unknown thumb command: " + c.Name);
                }
            }
        }

        sealed class PlanSink : ISink
        {
            public readonly List<string> Ops = new List<string>();
            static string N(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
            public void FillColor(string css) { Ops.Add("fillColor " + css); }
            public void StrokeColor(string css) { Ops.Add("strokeColor " + css); }
            public void LineWidth(double w) { Ops.Add("lineWidth " + N(w)); }
            public void LineCap(string cap) { Ops.Add("lineCap " + (cap == "round" ? "Round" : "Butt")); }
            public void LineJoin(string join) { Ops.Add("lineJoin " + (join == "round" ? "Round" : join == "bevel" ? "Bevel" : "Miter")); }
            public void FillRect(double x, double y, double w, double h) { Ops.Add("fillRect " + N(x) + " " + N(y) + " " + N(w) + " " + N(h)); }
            public void BeginPath() { Ops.Add("beginPath"); }
            public void RoundRect(double x, double y, double w, double h, double r) { Ops.Add("roundRect " + N(x) + " " + N(y) + " " + N(w) + " " + N(h) + " " + N(r)); }
            public void MoveTo(double x, double y) { Ops.Add("moveTo " + N(x) + " " + N(y)); }
            public void LineTo(double x, double y) { Ops.Add("lineTo " + N(x) + " " + N(y)); }
            public void ClosePath() { Ops.Add("closePath"); }
            public void Fill() { Ops.Add("fill"); }
            public void Stroke() { Ops.Add("stroke"); }
        }

        sealed class PainterSink : ISink
        {
            readonly Painter2D p;
            readonly List<Vector2> path = new List<Vector2>();   // the current path while it is one polyline
            bool polyline, closed;
            public PainterSink(Painter2D painter) { p = painter; }
            static Color C(string css) { ThemeColor t = ThemeColor.Parse(css); return new Color((float)t.R, (float)t.G, (float)t.B, (float)t.A); }
            static Vector2 V(double x, double y) { return new Vector2((float)x, (float)y); }
            public void FillColor(string css) { p.fillColor = C(css); }
            public void StrokeColor(string css) { p.strokeColor = C(css); }
            public void LineWidth(double w) { p.lineWidth = (float)w; }
            public void LineCap(string cap) { p.lineCap = cap == "round" ? UnityEngine.UIElements.LineCap.Round : UnityEngine.UIElements.LineCap.Butt; }
            public void LineJoin(string join)
            {
                p.lineJoin = join == "round" ? UnityEngine.UIElements.LineJoin.Round
                    : join == "bevel" ? UnityEngine.UIElements.LineJoin.Bevel : UnityEngine.UIElements.LineJoin.Miter;
            }
            public void FillRect(double x, double y, double w, double h)
            {
                p.BeginPath();
                p.MoveTo(V(x, y)); p.LineTo(V(x + w, y)); p.LineTo(V(x + w, y + h)); p.LineTo(V(x, y + h));
                p.ClosePath();
                p.Fill();
            }
            public void BeginPath() { p.BeginPath(); path.Clear(); polyline = true; closed = false; }
            // Rounded rectangle with four quarter arcs (UI Toolkit angles: 0 = +x, clockwise in y-down space).
            public void RoundRect(double x, double y, double w, double h, double r)
            {
                polyline = false;
                r = Math.Max(0, Math.Min(r, Math.Min(w, h) / 2));
                float fr = (float)r;
                p.MoveTo(V(x + r, y));
                p.LineTo(V(x + w - r, y));
                p.Arc(V(x + w - r, y + r), fr, Angle.Degrees(270), Angle.Degrees(360));
                p.LineTo(V(x + w, y + h - r));
                p.Arc(V(x + w - r, y + h - r), fr, Angle.Degrees(0), Angle.Degrees(90));
                p.LineTo(V(x + r, y + h));
                p.Arc(V(x + r, y + h - r), fr, Angle.Degrees(90), Angle.Degrees(180));
                p.LineTo(V(x, y + r));
                p.Arc(V(x + r, y + r), fr, Angle.Degrees(180), Angle.Degrees(270));
                p.ClosePath();
            }
            public void MoveTo(double x, double y) { if (path.Count > 0) polyline = false; path.Add(V(x, y)); p.MoveTo(V(x, y)); }
            public void LineTo(double x, double y) { path.Add(V(x, y)); p.LineTo(V(x, y)); }
            public void ClosePath() { closed = true; p.ClosePath(); }
            public void Fill() { p.Fill(); }
            public void Stroke()
            {
                if (!polyline || path.Count < 2 || p.lineJoin != UnityEngine.UIElements.LineJoin.Round) { p.Stroke(); return; }
                var quads = new List<Vector2[]>();
                var discs = new List<Vector2>();
                float half = p.lineWidth / 2f;
                StrokeAsFills(path, closed, half, p.lineCap == UnityEngine.UIElements.LineCap.Round, quads, discs);
                Color keep = p.fillColor;
                p.fillColor = p.strokeColor;
                foreach (Vector2[] q in quads)
                {
                    p.BeginPath();
                    p.MoveTo(q[0]); p.LineTo(q[1]); p.LineTo(q[2]); p.LineTo(q[3]);
                    p.ClosePath();
                    p.Fill();
                }
                foreach (Vector2 c in discs)
                {
                    p.BeginPath();
                    p.Arc(c, half, Angle.Degrees(0), Angle.Degrees(360));
                    p.ClosePath();
                    p.Fill();
                }
                p.fillColor = keep;
                BeginPath();
            }
        }
    }
}
