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
            public void BeginPath() { p.BeginPath(); }
            // Rounded rectangle with four quarter arcs (UI Toolkit angles: 0 = +x, clockwise in y-down space).
            public void RoundRect(double x, double y, double w, double h, double r)
            {
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
            public void MoveTo(double x, double y) { p.MoveTo(V(x, y)); }
            public void LineTo(double x, double y) { p.LineTo(V(x, y)); }
            public void ClosePath() { p.ClosePath(); }
            public void Fill() { p.Fill(); }
            public void Stroke() { p.Stroke(); }
        }
    }
}
