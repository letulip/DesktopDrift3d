// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/track-util.js @ 5556140 (2026-08-06), ported 2026-10-28.
// svgToCentreline (DOM-based) is not ported here; its XML part lives with the track loader.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DesktopDrift.Core
{
    public static class TrackUtil
    {
        /// <summary>JS: { center, outer, inner } returned by offsetEdges.</summary>
        public sealed class Edges
        {
            public List<Vec2> Center = new List<Vec2>();
            public List<Vec2> Outer = new List<Vec2>();
            public List<Vec2> Inner = new List<Vec2>();
        }

        private static readonly Regex SvgToken = new Regex(@"[MLHVZmlhvz]|[-+]?[0-9]*\.?[0-9]+", RegexOptions.CultureInvariant);

        private static bool IsCommandToken(string t)
        {
            return "MLHVZmlhvz".IndexOf(t[0]) >= 0;
        }

        // JS parseFloat(tokens[i]): undefined or a command letter gives NaN.
        private static double TokenValue(List<string> tokens, int i)
        {
            if (i >= tokens.Count) return double.NaN;
            string t = tokens[i];
            if (IsCommandToken(t)) return double.NaN;
            return double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        // Parse SVG path d: supports M, L, H, V, Z (absolute coordinates only).
        // Deduplicates the closing vertex (see the JS comment): drop the last point if dist(first, last) < 0.5.
        // Like JS: the numbers of lowercase commands are skipped and letters outside the token set
        // (C, A, ... – cafe-marble's track_path has two C's) are ignored: their numbers attach to the
        // previous command. (Step 4a threw FormatException here; removed in 4b for track parity.)
        public static List<Vec2> ParseSvgPath(string d)
        {
            var pts = new List<Vec2>();
            var tokens = new List<string>();
            foreach (Match m in SvgToken.Matches(d)) tokens.Add(m.Value);
            int i = 0;
            char cmd = '\0';
            double x = 0, y = 0;
            while (i < tokens.Count)
            {
                string t = tokens[i];
                if (IsCommandToken(t)) { cmd = t[0]; i++; continue; }
                double v = double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (cmd == 'M' || cmd == 'L')
                {
                    x = v; y = TokenValue(tokens, ++i); i++;
                    pts.Add(new Vec2(x, y));
                }
                else if (cmd == 'H') { x = v; i++; pts.Add(new Vec2(x, y)); }
                else if (cmd == 'V') { y = v; i++; pts.Add(new Vec2(x, y)); }
                else i++;
            }
            // Remove duplicate closing vertex (explicit "L back-to-start Z" pattern).
            if (pts.Count > 1)
            {
                Vec2 f = pts[0], l = pts[pts.Count - 1];
                if (CoreMath.Hypot(l.X - f.X, l.Y - f.Y) < 0.5) pts.RemoveAt(pts.Count - 1);
            }
            return pts;
        }

        // Chaikin corner-cutting – one pass. n points -> 2n points. Closed contour.
        public static List<Vec2> Chaikin(IReadOnlyList<Vec2> pts)
        {
            int n = pts.Count;
            var r = new List<Vec2>();
            for (int i = 0; i < n; i++)
            {
                Vec2 a = pts[i], b = pts[(i + 1) % n];
                r.Add(new Vec2(a.X * .75 + b.X * .25, a.Y * .75 + b.Y * .25));
                r.Add(new Vec2(a.X * .25 + b.X * .75, a.Y * .25 + b.Y * .75));
            }
            return r;
        }

        // Builds { center, outer, inner } from a centerline; the inner offset is clamped on hairpins
        // to min(half, R - minInnerGap) (R = circumradius of prev/curr/next).
        public static Edges OffsetEdges(IReadOnlyList<Vec2> centerPts, double half, double minInnerGap = 10)
        {
            var res = new Edges();
            int N = centerPts.Count;
            for (int i = 0; i < N; i++)
            {
                Vec2 c = centerPts[i];
                Vec2 prev = centerPts[(i - 1 + N) % N];
                Vec2 next = centerPts[(i + 1) % N];
                double tx = next.X - prev.X, ty = next.Y - prev.Y;
                double len = CoreMath.Hypot(tx, ty);
                if (len == 0 || double.IsNaN(len)) len = 1;
                double nx = -ty / len, ny = tx / len; // left-hand normal

                // Circumradius of the prev-curr-next triangle ~ local radius of curvature.
                double ax = prev.X - c.X, ay = prev.Y - c.Y;
                double bx = next.X - c.X, by = next.Y - c.Y;
                double cross = ax * by - ay * bx;           // 2 x signed triangle area
                double R = cross == 0 ? double.PositiveInfinity
                    : (CoreMath.Hypot(ax, ay) * CoreMath.Hypot(bx, by) * CoreMath.Hypot(ax - bx, ay - by))
                      / (2 * Math.Abs(cross));
                // Clamp inner offset: never let the inner point pass the centre of curvature.
                double innerHalf = Math.Min(half, Math.Max(R - minInnerGap, minInnerGap));

                res.Center.Add(c);
                res.Outer.Add(new Vec2(c.X + nx * half, c.Y + ny * half));
                res.Inner.Add(new Vec2(c.X - nx * innerHalf, c.Y - ny * innerHalf));
            }
            return res;
        }

        // Places cones along outer and inner edges at equal arc-length intervals (independent accumulators).
        public static List<Cone> PlaceCones(IReadOnlyList<Vec2> outer, IReadOnlyList<Vec2> inner, double minSpacing = 160)
        {
            var cones = new List<Cone>();
            int N = outer.Count;
            double outerAcc = 0, innerAcc = 0;
            for (int i = 0; i < N; i++)
            {
                int next = (i + 1) % N;
                if (i == 0 || outerAcc >= minSpacing)
                {
                    // x0/y0 = the immutable standing position; ResetCones restores it on a re-entrant restart.
                    cones.Add(new Cone { X = outer[i].X, Y = outer[i].Y, X0 = outer[i].X, Y0 = outer[i].Y, Vx = 0, Vy = 0, Ang = 0, Spin = 0, Knocked = false, Ci = i });
                    outerAcc = 0;
                }
                if (i == 0 || innerAcc >= minSpacing)
                {
                    cones.Add(new Cone { X = inner[i].X, Y = inner[i].Y, X0 = inner[i].X, Y0 = inner[i].Y, Vx = 0, Vy = 0, Ang = 0, Spin = 0, Knocked = false, Ci = i });
                    innerAcc = 0;
                }
                outerAcc += CoreMath.Hypot(outer[next].X - outer[i].X, outer[next].Y - outer[i].Y);
                innerAcc += CoreMath.Hypot(inner[next].X - inner[i].X, inner[next].Y - inner[i].Y);
            }
            return cones;
        }

        // Drop cones that a self-intersection pushed into the middle of the track. Pure.
        public static List<Cone> FilterConesOnTrack(IReadOnlyList<Cone> cones, IReadOnlyList<Vec2> center, double half, int indexWindow = 24)
        {
            int N = center.Count;
            double g2 = half * half;   // within a full track-half of a foreign centreline = on that lane
            var kept = new List<Cone>();
            foreach (Cone cone in cones)
            {
                bool keep = true;
                for (int j = 0; j < N; j++)
                {
                    double d = Math.Abs(j - cone.Ci);
                    if (d > N / 2.0) d = N - d;               // circular index distance
                    if (d <= indexWindow) continue;           // the cone's own neighbourhood
                    double dx = center[j].X - cone.X, dy = center[j].Y - cone.Y;
                    if (dx * dx + dy * dy < g2) { keep = false; break; } // inside a non-adjacent lane
                }
                if (keep) kept.Add(cone);
            }
            return kept;
        }

        // K checkpoints, evenly distributed by index along the centerline.
        public static List<Vec2> SampleCheckpoints(IReadOnlyList<Vec2> center, int K)
        {
            int N = center.Count;
            var cps = new List<Vec2>();
            for (int i = 0; i < K; i++) cps.Add(center[(int)Math.Floor(((double)i / K) * N)]);
            return cps;
        }

        // K checkpoints biased toward corners (arc-length sectors, highest curvature per sector,
        // finish-line anchor, minimum spacing, oversized-gap splitting). See the JS comments.
        public static List<Vec2> SampleCheckpointsByCorner(IReadOnlyList<Vec2> center, int K)
        {
            int N = center.Count;

            // arc[i] = cumulative distance from center[0] to center[i].
            var arc = new List<double> { 0 };
            for (int i = 1; i < N; i++)
                arc.Add(arc[i - 1] + CoreMath.Hypot(center[i].X - center[i - 1].X, center[i].Y - center[i - 1].Y));
            double totalLen = arc[N - 1] + CoreMath.Hypot(center[0].X - center[N - 1].X, center[0].Y - center[N - 1].Y);

            var cpIdxs = new List<int>();
            var midIdxs = new List<int>();

            for (int i = 0; i < K; i++)
            {
                double loLen = ((double)i / K) * totalLen;
                double hiLen = ((double)(i + 1) / K) * totalLen;

                // lo = first index whose arc-length position falls at or past loLen
                int lo = 0;
                while (lo < N - 1 && arc[lo + 1] <= loLen) lo++;
                // hi = first index past hiLen (exclusive upper bound)
                int hi = lo + 1;
                while (hi < N && arc[hi] < hiLen) hi++;

                int midIdx = (lo + hi) / 2;
                midIdxs.Add(midIdx);

                int bestIdx = midIdx;
                double bestCurv = -1;
                for (int j = lo; j < hi; j++)
                {
                    Vec2 prev = center[(j - 1 + N) % N];
                    Vec2 c = center[j];
                    Vec2 next = center[(j + 1) % N];
                    double ax = prev.X - c.X, ay = prev.Y - c.Y;
                    double bx = next.X - c.X, by = next.Y - c.Y;
                    double cross = Math.Abs(ax * by - ay * bx);
                    if (cross == 0) continue;
                    double R = (CoreMath.Hypot(ax, ay) * CoreMath.Hypot(bx, by) * CoreMath.Hypot(ax - bx, ay - by))
                               / (2 * cross);
                    double curv = 1 / R;
                    if (curv > bestCurv) { bestCurv = curv; bestIdx = j; }
                }
                cpIdxs.Add(bestIdx);
            }

            // Post-process 1: anchor checkpoint[0] at center[0] (the finish line).
            cpIdxs[0] = 0;

            // Post-process 2: enforce minimum arc-length gap between consecutive checkpoints.
            double minGap = totalLen / K / 2;
            for (int i = 1; i < K; i++)
            {
                double gap = (arc[cpIdxs[i]] - arc[cpIdxs[i - 1]] + totalLen) % totalLen;
                if (gap < minGap) cpIdxs[i] = midIdxs[i];
            }
            // Also check the wrap-around gap from cps[K-1] back to cps[0].
            if ((totalLen - arc[cpIdxs[K - 1]]) % totalLen < minGap) cpIdxs[K - 1] = midIdxs[K - 1];

            // Post-process 3: split oversized gaps (> 1.5x the average sector length).
            double maxGap = (totalLen / K) * 1.5;
            Func<double, int> idxAtArc = (len) =>
            {
                double t = ((len % totalLen) + totalLen) % totalLen;
                int lo = 0;
                while (lo < N - 1 && arc[lo + 1] < t) lo++;
                return lo;
            };
            var finalIdxs = new List<int>();
            for (int i = 0; i < K; i++)
            {
                finalIdxs.Add(cpIdxs[i]);
                double a = arc[cpIdxs[i]];
                double gap = (arc[cpIdxs[(i + 1) % K]] - a + totalLen) % totalLen;
                double nInsert = Math.Floor(gap / maxGap);
                for (int k = 1; k <= nInsert; k++) finalIdxs.Add(idxAtArc(a + gap * k / (nInsert + 1)));
            }

            var result = new List<Vec2>();
            foreach (int idx in finalIdxs) result.Add(center[idx]);
            return result;
        }

        // Prepares a prop descriptor for render/physics: defaults hl to 0, caches cos/sin of angle.
        // Mutates and returns the same object.
        public static Prop PrepProp(Prop o)
        {
            o.Hl = (o.Hl == 0 || double.IsNaN(o.Hl)) ? 0 : o.Hl;
            o.Cos = Math.Cos(o.Ang);
            o.Sin = Math.Sin(o.Ang);
            return o;
        }

        // Windowed nearest-centerline scan – O(2*window+1) instead of O(N).
        public static (double Dist, int Idx) NearestCenter(double carX, double carY, IReadOnlyList<Vec2> center, int prevIdx, int window = 24)
        {
            int N = center.Count;
            double best = double.PositiveInfinity;
            int bi = prevIdx;
            for (int k = -window; k <= window; k++)
            {
                int i = (((prevIdx + k) % N) + N) % N;
                double dx = carX - center[i].X, dy = carY - center[i].Y;
                double d = dx * dx + dy * dy;
                if (d < best) { best = d; bi = i; }
            }
            return (Math.Sqrt(best), bi);
        }

        // Forward advancement from `reference` to idx on a circular track of N points (JS param name: ref).
        // Values > N/2 are treated as backward movement and return 0.
        public static int CircularAdvance(int idx, int reference, int N)
        {
            int d = (idx - reference + N) % N;
            return d <= N / 2.0 ? d : 0;
        }

        // Persistence key for a track instance: the bare track id for forward, `${id}:rev` for reversed.
        public static string InstanceId(string trackId, bool reversed)
        {
            return reversed ? trackId + ":rev" : trackId;
        }

        // JS template-literal number formatting (`${t.x}`), for the legacy "x,y" coordinate keys.
        internal static string JsNumber(double v)
        {
            return CoreMath.JsNumberToString(v);
        }

        // Prune a stored tire-collection list down to the tiles that exist on the current track,
        // re-keyed to each tile's stable capId. A tile counts as collected if the store holds its
        // current capId OR its legacy `x,y` coordinate key. Deduped (one entry per tile).
        public static List<string> ReconcileTires(IReadOnlyList<string> storedIds, IReadOnlyList<Collectible> tiles)
        {
            var have = new HashSet<string>(storedIds);
            var output = new List<string>();
            foreach (Collectible t in tiles)
            {
                if (have.Contains(t.CapId) || have.Contains(JsNumber(t.X) + "," + JsNumber(t.Y))) output.Add(t.CapId);
            }
            return output;
        }

        // Stand every cone back up at its recorded x0/y0 and zero its motion. Pure; mutates in place.
        public static void ResetCones(List<Cone> cones)
        {
            foreach (Cone c in cones)
            {
                c.Knocked = false;
                c.Vx = 0; c.Vy = 0; c.Ang = 0; c.Spin = 0;
                c.X = c.X0; c.Y = c.Y0;
            }
        }

        // Reverse a parsed track – drive the same circuit the other way. Pure: returns a new object, the
        // input is not mutated. center/inner/outer are reversed in lockstep (inner/outer intentionally NOT
        // swapped); checkpoints/startPos/startAngle are recomputed. Everything else is carried over
        // (same references for cones, props, collectibles, table, theme).
        public static TrackData ReverseTrack(TrackData T)
        {
            var center = new List<Vec2>(T.Center); center.Reverse();
            var inner = new List<Vec2>(T.Inner); inner.Reverse();
            var outer = new List<Vec2>(T.Outer); outer.Reverse();
            int K = T.K == 0 ? 8 : T.K;
            List<Vec2> checkpoints = SampleCheckpointsByCorner(center, K);
            Vec2 c0 = center[0];
            Vec2 c1 = center.Count > 1 ? center[1] : center[0];
            return new TrackData
            {
                TrackHalf = T.TrackHalf,
                ConeR = T.ConeR,
                K = T.K,
                CpR = T.CpR,
                Center = center,
                Outer = outer,
                Inner = inner,
                Cones = T.Cones,
                Table = T.Table,
                Props = T.Props,
                Collectibles = T.Collectibles,
                Checkpoints = checkpoints,
                StartPos = new Vec2(c0.X, c0.Y),
                StartAngle = Math.Atan2(c1.Y - c0.Y, c1.X - c0.X),
                Id = T.Id,
                Laps = T.Laps,
                Theme = T.Theme,
                Reversed = true,
            };
        }
    }
}
