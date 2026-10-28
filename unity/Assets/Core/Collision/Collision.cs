// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/collision.js @ 5556140 (2026-08-06), ported 2026-10-28.
using System;
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    /// <summary>Pure collision / finish-line geometry. Mutating semantics of the JS are kept.</summary>
    public static class Collision
    {
        /// <summary>Signed projection of the car position onto the start/finish axis.</summary>
        public static double FinishDot(CarState car, Vec2 c0, double cos, double sin)
        {
            return (car.X - c0.X) * cos + (car.Y - c0.Y) * sin;
        }

        /// <summary>True when the car goes from behind (prevDot &lt; 0) to at/past (dot &gt;= 0) the line.</summary>
        public static bool CrossedFinish(double prevDot, double dot)
        {
            return prevDot < 0 && dot >= 0;
        }

        /// <summary>True when the car is within nmBand of any surface while moving above the speed gate (140).</summary>
        public static bool NearMiss(CarState car, IReadOnlyList<Cone> cones, IReadOnlyList<Prop> props, TableRect table, double coneR, double cr, double nmBand)
        {
            var speed = CoreMath.Hypot(car.Vx, car.Vy);
            if (speed < 140) return false;
            if (table.Round)
            {
                double rx = table.W / 2 - cr, ry = table.H / 2 - cr;
                var r = CoreMath.Hypot(car.X / rx, car.Y / ry);
                var gap = (1 - r) * Math.Min(rx, ry);
                if (gap > 0 && gap < nmBand) return true;
            }
            else
            {
                var gx = (table.W / 2 - cr) - Math.Abs(car.X);
                var gy = (table.H / 2 - cr) - Math.Abs(car.Y);
                if ((gx > 0 && gx < nmBand) || (gy > 0 && gy < nmBand)) return true;
            }
            foreach (var c in cones)
            {
                if (c.Knocked) continue;
                var d = CoreMath.Hypot(car.X - c.X, car.Y - c.Y) - (coneR + cr);
                if (d > 0 && d < nmBand) return true;
            }
            foreach (var o in props)
            {
                double qx = o.X, qy = o.Y;
                if (o.Hl > 0)
                {
                    var lx = car.X - o.X;
                    var ly = car.Y - o.Y;
                    var t = lx * o.Cos + ly * o.Sin;
                    if (t > o.Hl) t = o.Hl; else if (t < -o.Hl) t = -o.Hl;
                    qx = o.X + o.Cos * t; qy = o.Y + o.Sin * t;
                }
                var d = CoreMath.Hypot(car.X - qx, car.Y - qy) - (o.R + cr);
                if (d > 0 && d < nmBand) return true;
            }
            return false;
        }

        /// <summary>
        /// Anti-skip checkpoint advance: next index if the car is within cpR of the CURRENT checkpoint,
        /// else nextCp unchanged (also when the index has no checkpoint, JS: undefined).
        /// </summary>
        public static int AdvanceCheckpoint(int nextCp, double carX, double carY, IReadOnlyList<Vec2> checkpoints, double cpR)
        {
            if (nextCp < 0 || nextCp >= checkpoints.Count) return nextCp;
            var cp = checkpoints[nextCp];
            if (CoreMath.Hypot(carX - cp.X, carY - cp.Y) < cpR) return (nextCp + 1) % checkpoints.Count;
            return nextCp;
        }

        /// <summary>
        /// Car vs table wall (capsule). MUTATES car.X/Y/Vx/Vy. Returns the impact magnitude along the wall normal.
        /// Engine inputs (js/game-engine.js ~425–426):
        ///   hx = cos(car.angle), hy = sin(car.angle), nose = M.len * 0.3,
        ///   bodyPts = [[car.x + hx*nose, car.y + hy*nose], [car.x, car.y], [car.x - hx*nose, car.y - hy*nose]]
        /// (front, centre, rear; the pre-collision snapshot, not recomputed mid-step).
        /// </summary>
        public static double ResolveWall(CarState car, TableRect table, double cr, double hx, double hy, double nose, Vec2[] bodyPts)
        {
            if (table.Round)
            {
                // Iterate capsule points: front → centre → rear. First violation = response.
                double rx = table.W / 2 - cr, ry = table.H / 2 - cr;
                foreach (var bp in bodyPts)
                {
                    double bpx = bp.X, bpy = bp.Y;
                    var bnx = bpx / rx;
                    var bny = bpy / ry;
                    var br = CoreMath.Hypot(bnx, bny);
                    if (br > 1)
                    {
                        car.X += bnx / br * rx - bpx;
                        car.Y += bny / br * ry - bpy;
                        double ux = bnx / br / rx, uy = bny / br / ry;
                        var ul = CoreMath.Hypot(ux, uy);
                        double px = ux / ul, py = uy / ul;
                        var vn = car.Vx * px + car.Vy * py;
                        if (vn > 0) { car.Vx -= vn * px * 1.3; car.Vy -= vn * py * 1.3; return vn; }
                        return 0; // one response per frame
                    }
                }
                return 0;
            }
            // Capsule AABB: extent along X/Y depends on car angle, not just cr.
            var absExtX = Math.Abs(hx) * nose + cr;
            var absExtY = Math.Abs(hy) * nose + cr;
            double wallW = table.W / 2, wallH = table.H / 2;
            double wallHit = 0;
            if (car.X - absExtX < -wallW) { car.X = -wallW + absExtX; if (car.Vx < 0) { wallHit = Math.Max(wallHit, -car.Vx); car.Vx *= -0.3; } car.Vy *= 0.85; }
            if (car.X + absExtX > wallW) { car.X = wallW - absExtX; if (car.Vx > 0) { wallHit = Math.Max(wallHit, car.Vx); car.Vx *= -0.3; } car.Vy *= 0.85; }
            if (car.Y - absExtY < -wallH) { car.Y = -wallH + absExtY; if (car.Vy < 0) { wallHit = Math.Max(wallHit, -car.Vy); car.Vy *= -0.3; } car.Vx *= 0.85; }
            if (car.Y + absExtY > wallH) { car.Y = wallH - absExtY; if (car.Vy > 0) { wallHit = Math.Max(wallHit, car.Vy); car.Vy *= -0.3; } car.Vx *= 0.85; }
            return wallHit;
        }

        /// <summary>
        /// Car vs props (capsule nearest-point pushback). MUTATES car.X/Y/Vx/Vy. Returns the largest
        /// inbound impact magnitude (−vn) among colliding props.
        /// </summary>
        public static double ResolveProps(CarState car, IReadOnlyList<Prop> props, double cr, Vec2[] bodyPts)
        {
            double crash = 0;
            foreach (var o in props)
            {
                // Find the closest capsule body point to the prop
                double bestD2 = double.PositiveInfinity, bestBpX = car.X, bestBpY = car.Y;
                double bestQx = o.X, bestQy = o.Y;
                foreach (var bp in bodyPts)
                {
                    double bpx = bp.X, bpy = bp.Y;
                    double qx = o.X, qy = o.Y;
                    if (o.Hl > 0)
                    {
                        var lx = bpx - o.X;
                        var ly = bpy - o.Y;
                        var t = lx * o.Cos + ly * o.Sin;
                        if (t > o.Hl) t = o.Hl; else if (t < -o.Hl) t = -o.Hl;
                        qx = o.X + o.Cos * t; qy = o.Y + o.Sin * t;
                    }
                    double dx = bpx - qx, dy = bpy - qy, d2 = dx * dx + dy * dy;
                    if (d2 < bestD2) { bestD2 = d2; bestBpX = bpx; bestBpY = bpy; bestQx = qx; bestQy = qy; }
                }
                var rr = o.R + cr;
                if (bestD2 < rr * rr)
                {
                    var d = Math.Sqrt(bestD2);
                    if (d == 0) d = 1;
                    double nx = (bestBpX - bestQx) / d, ny = (bestBpY - bestQy) / d;
                    car.X += bestQx + nx * rr - bestBpX;
                    car.Y += bestQy + ny * rr - bestBpY;
                    var vn = car.Vx * nx + car.Vy * ny;
                    if (vn < 0) { car.Vx -= vn * nx * 1.4; car.Vy -= vn * ny * 1.4; if (-vn > crash) crash = -vn; }
                }
            }
            return crash;
        }

        /// <summary>
        /// Advances a knocked cone one frame: translate, resolve prop collisions, apply decay.
        /// Mutates c in place. fAdj = dt * PHYS_HZ (frame-normalised decay exponent).
        /// </summary>
        public static void StepKnockedCone(Cone c, IReadOnlyList<Prop> props, double coneR, double dt, double fAdj)
        {
            var dAdj = Math.Pow(0.9, fAdj);
            c.X += c.Vx * dt; c.Y += c.Vy * dt;
            foreach (var o in props)
            {
                double qx = o.X, qy = o.Y;
                if (o.Hl > 0)
                {
                    var lx = c.X - o.X;
                    var ly = c.Y - o.Y;
                    var t = lx * o.Cos + ly * o.Sin;
                    if (t > o.Hl) t = o.Hl; else if (t < -o.Hl) t = -o.Hl;
                    qx = o.X + o.Cos * t; qy = o.Y + o.Sin * t;
                }
                double dx = c.X - qx, dy = c.Y - qy, minD = o.R + coneR;
                var d2 = dx * dx + dy * dy;
                if (d2 < minD * minD)
                {
                    var d = Math.Sqrt(d2);
                    if (d == 0) d = 1;
                    double nx = dx / d, ny = dy / d;
                    c.X = qx + nx * minD; c.Y = qy + ny * minD;
                    var vDotN = c.Vx * nx + c.Vy * ny;
                    if (vDotN < 0) { c.Vx -= vDotN * nx * 0.8; c.Vy -= vDotN * ny * 0.8; c.Spin *= -0.4; }
                }
            }
            c.Vx *= dAdj; c.Vy *= dAdj; c.Ang += c.Spin * dt; c.Spin *= dAdj;
        }
    }
}
