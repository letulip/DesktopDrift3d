// The track ribbon exactly as the 2D canvas draws it (js/render.js lines 261-266 and 190-194):
//   RENDER_STEP = 3; trackPath: moveTo(c[0]); lineTo(c[i]) for i = 3, 6, ... < n; closePath();
//   strokeStyle = TH.track; lineWidth = TRACK_HALF * 2; lineJoin = 'round'; lineCap = 'round'; stroke(trackPath).
// A round-joined nonzero stroke is the union of a rectangle per segment (half-width TRACK_HALF) and a disc of radius
// TRACK_HALF at every vertex, so self-crossings are solid. This class emits those pieces as triangles (overlaps are fine
// for a single flat colour). Frame: x right, y up (the track data's), all triangles counter-clockwise.
#nullable enable
using System;
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    public static class RibbonGeometry
    {
        public sealed class Ribbon
        {
            public List<Vec2> Vertices = new List<Vec2>();
            public List<int> Triangles = new List<int>();   // 3 indices per triangle
            public int PathPoints;
        }

        public static Ribbon Build(IReadOnlyList<Vec2> center, double half, int step = 3, int discSegments = 32)
        {
            if (step < 1) throw new ArgumentOutOfRangeException("step");
            if (discSegments < 3) throw new ArgumentOutOfRangeException("discSegments");
            Ribbon r = new Ribbon();
            int n = center.Count;
            List<Vec2> path = new List<Vec2>();
            for (int i = 0; i < n; i += step) path.Add(center[i]);
            r.PathPoints = path.Count;
            int m = path.Count;

            // Rectangles: one per closed-path segment (last point back to the first); zero-length segments are skipped.
            for (int i = 0; i < m && m > 1; i++)
            {
                Vec2 a = path[i];
                Vec2 b = path[(i + 1) % m];
                double dx = b.X - a.X;
                double dy = b.Y - a.Y;
                double len = CoreMath.Hypot(dx, dy);
                if (len == 0.0) continue;
                double nx = -dy / len * half;   // left normal (y up)
                double ny = dx / len * half;
                int v = r.Vertices.Count;
                r.Vertices.Add(new Vec2(a.X - nx, a.Y - ny));
                r.Vertices.Add(new Vec2(b.X - nx, b.Y - ny));
                r.Vertices.Add(new Vec2(b.X + nx, b.Y + ny));
                r.Vertices.Add(new Vec2(a.X + nx, a.Y + ny));
                AddTri(r, v, v + 1, v + 2);
                AddTri(r, v, v + 2, v + 3);
            }

            // Discs: a fan of discSegments triangles at every path point.
            for (int i = 0; i < m; i++)
            {
                Vec2 c = path[i];
                int centre = r.Vertices.Count;
                r.Vertices.Add(c);
                for (int k = 0; k < discSegments; k++)
                {
                    double ang = 2.0 * Math.PI * k / discSegments;
                    r.Vertices.Add(new Vec2(c.X + half * Math.Cos(ang), c.Y + half * Math.Sin(ang)));
                }
                for (int k = 0; k < discSegments; k++)
                {
                    AddTri(r, centre, centre + 1 + k, centre + 1 + (k + 1) % discSegments);
                }
            }
            return r;
        }

        private static void AddTri(Ribbon r, int a, int b, int c)
        {
            r.Triangles.Add(a);
            r.Triangles.Add(b);
            r.Triangles.Add(c);
        }
    }
}
