// VERBATIM PORT of the drawing part of DesktopDrift/js/track-thumb.js `drawThumb(canvas, svgSrc, theme)` (everything after the fetch).
// Output = the canvas 2D command list the real JS issues (proven equal by TrackThumbTests against tools/thumb-ref/<id>.json).
//
// UI RULE (architect, SPEC-03 step 6): the thumbnail is the SAME vector drawing in every size (replayed by Game/Ui/TrackThumbElement
// with Painter2D, no PNG), and a reversed track does NOT get a mirrored thumbnail – the 2D screen draws "same geometry both ways – no
// mirror" (tracks.js line 181). So the track screens must call Commands with the FORWARD centreline for both directions;
// ForInstance(forward, reversed) encodes that (it ignores `reversed`).
//
// Command shape: ThumbCommand { Name, Args, Value }.
//   method call  fillRect/beginPath/roundRect/fill/stroke/moveTo/lineTo/closePath: Name = method, Args = the call arguments, Value = null.
//   property set string-valued (fillStyle, strokeStyle, lineCap, lineJoin): Name = "set:<prop>", Args = empty, Value = the CSS string.
//   property set numeric (lineWidth): Name = "set:lineWidth", Args = { value }, Value = null.
// The `centerline` is TrackData.Center (= svgToCentreline: #track_path + toGame + Chaikin x4; offsetEdges keeps the centre points unchanged).
#nullable enable
using System;
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    public sealed class ThumbCommand
    {
        public string Name;
        public double[] Args;
        public string? Value;

        public ThumbCommand(string name, double[] args, string? value)
        {
            Name = name;
            Args = args;
            Value = value;
        }
    }

    public static class TrackThumb
    {
        public const double TrackGu = 100;       // JS TRACK_GU
        public const double CanvasWidth = 260;   // tracks.js canvas.width
        public const double CanvasHeight = 140;  // tracks.js canvas.height

        /// <summary>Thumbnail commands of a track: always the FORWARD geometry (reversed tracks are not mirrored).</summary>
        public static List<ThumbCommand> ForInstance(TrackData forward, bool reversed)
        {
            return Commands(forward.Center, forward.Theme, CanvasWidth, CanvasHeight);
        }

        public static List<ThumbCommand> Commands(IReadOnlyList<Vec2> centerline, TrackTheme? theme, double width, double height)
        {
            // JS `theme?.key ?? fallback` – a missing key is "" in TrackTheme. The fallbacks differ from THEME_DEFAULT on purpose.
            string background = Or(theme?.Background, "#0b0907");
            string table = Or(theme?.Table, "#2e241a");
            string tableEdge = Or(theme?.TableEdge, "#5a4a36");
            string track = Or(theme?.Track, "#ffb14d");

            var cmds = new List<ThumbCommand>();
            var pts = centerline;
            if (pts == null || pts.Count == 0) return cmds;   // JS: `if (!pts) return` (svgToCentreline null)

            double minX = pts[0].X, maxX = pts[0].X, minY = pts[0].Y, maxY = pts[0].Y;
            for (int i = 1; i < pts.Count; i++)
            {
                minX = Math.Min(minX, pts[i].X); maxX = Math.Max(maxX, pts[i].X);
                minY = Math.Min(minY, pts[i].Y); maxY = Math.Max(maxY, pts[i].Y);
            }

            double extW = maxX - minX + TrackGu * 2;
            double extH = maxY - minY + TrackGu * 2;
            double pad = 10;
            double sx = (width - pad * 2) / extW;
            double sy = (height - pad * 2) / extH;
            double s = Math.Min(sx, sy);
            double ox = (width - extW * s) / 2 + TrackGu * s;
            double oy = (height - extH * s) / 2 + TrackGu * s;

            cmds.Add(SetStr("fillStyle", background));
            cmds.Add(Call("fillRect", 0, 0, width, height));

            double ti = 5, tr = 5;
            cmds.Add(Call("beginPath"));
            cmds.Add(Call("roundRect", ti, ti, width - ti * 2, height - ti * 2, tr));
            cmds.Add(SetStr("fillStyle", table)); cmds.Add(Call("fill"));
            cmds.Add(SetStr("strokeStyle", tableEdge)); cmds.Add(SetNum("lineWidth", 2)); cmds.Add(Call("stroke"));

            cmds.Add(SetNum("lineWidth", TrackGu * 2 * s));
            cmds.Add(SetStr("strokeStyle", track));
            cmds.Add(SetStr("lineCap", "round"));
            cmds.Add(SetStr("lineJoin", "round"));
            cmds.Add(Call("beginPath"));
            for (int idx = 0; idx < pts.Count; idx++)
            {
                double x = (pts[idx].X - minX) * s + ox;   // toC
                double y = (pts[idx].Y - minY) * s + oy;
                cmds.Add(Call(idx == 0 ? "moveTo" : "lineTo", x, y));
            }
            cmds.Add(Call("closePath"));
            cmds.Add(Call("stroke"));
            return cmds;
        }

        private static string Or(string? v, string fallback) { return string.IsNullOrEmpty(v) ? fallback : v!; }
        private static ThumbCommand Call(string name, params double[] args) { return new ThumbCommand(name, args, null); }
        private static ThumbCommand SetStr(string prop, string value) { return new ThumbCommand("set:" + prop, new double[0], value); }
        private static ThumbCommand SetNum(string prop, double value) { return new ThumbCommand("set:" + prop, new[] { value }, null); }
    }
}
