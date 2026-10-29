using System;
using DesktopDrift.Core;
using UnityEngine;

namespace DesktopDrift.Game
{
    /// <summary>
    /// Rear-wheel skid marks (SPEC-02 §9): one dynamic mesh per wheel, a ring buffer of quads with a hard limit,
    /// one material, buffers uploaded only when a segment was added. Condition/placement/alpha follow the 2D engine.
    /// </summary>
    public sealed class SkidMarks : MonoBehaviour
    {
        [SerializeField] RaceLoop loop;
        [SerializeField] Material material;
        [SerializeField] TrackView trackView;   // supplies the merged theme (THEME_DEFAULT + track theme)
        [SerializeField] int maxSegmentsPerWheel = 750;

        const float MarkY = 0.001f;        // 1 mm above the table (ribbon sits at 0.0005)
        const double HalfWidth = 3.0;      // 2D: 6x6 rect per mark
        const int Levels = 6;              // 2D SKID_LEVELS

        Color32 skidRgb; // RGB of the track's theme skid (2D keeps only the RGB; alpha is per level)

        Strip left, right;

        public static bool ShouldMark(double speed, double slip) => slip > 40 && speed > 60;

        public static double SlipOf(double vx, double vy, double angle) => Math.Abs(-vx * Math.Sin(angle) + vy * Math.Cos(angle));

        public static float Alpha(double slip)
        {
            double a = Math.Min(slip / 200, 0.6);
            int b = Math.Min((int)(a * 10), Levels - 1);
            return (float)((b + 0.5) * 0.1);
        }

        public static void WheelPoints(double x, double y, double angle, out double lx, out double ly, out double rx, out double ry)
        {
            double c = Math.Cos(angle), s = Math.Sin(angle);
            double ax = x - c * 12, ay = y - s * 12;
            double sx = -s, sy = c;
            lx = ax + sx * 12; ly = ay + sy * 12;
            rx = ax - sx * 12; ry = ay - sy * 12;
        }

        void Awake()
        {
            int max = Mathf.Max(1, maxSegmentsPerWheel);
            left = new Strip(transform, "WheelL", material, max);
            right = new Strip(transform, "WheelR", material, max);
        }

        void OnEnable()
        {
            if (loop == null) return;
            loop.OnStepped += HandleStepped;
            loop.OnReset += HandleReset;
        }

        void OnDisable()
        {
            if (loop == null) return;
            loop.OnStepped -= HandleStepped;
            loop.OnReset -= HandleReset;
        }

        void OnDestroy()
        {
            left?.Dispose();
            right?.Dispose();
        }

        void HandleReset()
        {
            if (loop.Track != null)
            {
                ThemeColor c = ThemeColor.Parse(trackView.ThemeFor(loop.Track).Skid);
                // Vertex colours are not colour-space converted: in a Linear project the sRGB theme value must be
                // linearised here, like TrackView.OverlayColor does for the flag and ring (alpha stays per level).
                Color lin = TrackView.OverlayColor(new ThemeColor(c.R, c.G, c.B, 1));
                skidRgb = new Color32((byte)Mathf.RoundToInt(lin.r * 255f), (byte)Mathf.RoundToInt(lin.g * 255f),
                    (byte)Mathf.RoundToInt(lin.b * 255f), 255);
                // Table rectangle, set once per race (2x the table so either origin convention is covered).
                var size = new Vector3((float)loop.Track.Table.W, 0f, (float)loop.Track.Table.H) * (2f * WorldMapping.UnitsToMeters);
                var bounds = new Bounds(new Vector3(0f, MarkY, 0f), size + new Vector3(0f, 0.01f, 0f));
                left.SetBounds(bounds);
                right.SetBounds(bounds);
            }
            left.Clear();
            right.Clear();
        }

        void HandleStepped()
        {
            CarState c = loop.State.Car;
            double speed = Math.Sqrt(c.Vx * c.Vx + c.Vy * c.Vy);
            double slip = SlipOf(c.Vx, c.Vy, c.Angle);
            if (!ShouldMark(speed, slip))
            {
                left.Break();
                right.Break();
                return;
            }
            WheelPoints(c.X, c.Y, c.Angle, out double lx, out double ly, out double rx, out double ry);
            byte a = (byte)Mathf.RoundToInt(Alpha(slip) * 255f);
            var col = new Color32(skidRgb.r, skidRgb.g, skidRgb.b, a);
            left.Add(lx, ly, col);
            right.Add(rx, ry, col);
        }

        void LateUpdate()
        {
            left.Upload();
            right.Upload();
        }

        sealed class Strip : IDisposable
        {
            readonly Mesh mesh;
            readonly Vector3[] verts;
            readonly Color32[] colors;
            readonly int max;
            int next;
            bool hasPrev, dirty;
            double px, py;

            public Strip(Transform parent, string name, Material mat, int max)
            {
                this.max = max;
                verts = new Vector3[max * 4];
                colors = new Color32[max * 4];
                var idx = new ushort[max * 6];
                for (int i = 0; i < max; i++)
                {
                    int v = i * 4, k = i * 6;
                    idx[k] = (ushort)v; idx[k + 1] = (ushort)(v + 1); idx[k + 2] = (ushort)(v + 2);
                    idx[k + 3] = (ushort)v; idx[k + 4] = (ushort)(v + 2); idx[k + 5] = (ushort)(v + 3);
                }
                mesh = new Mesh { name = "SkidMarks" + name };
                mesh.MarkDynamic();
                mesh.SetVertices(verts);
                mesh.SetColors(colors);
                mesh.SetIndices(idx, MeshTopology.Triangles, 0, false);
                mesh.bounds = new Bounds(Vector3.zero, new Vector3(10f, 0.01f, 10f));

                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }

            public void SetBounds(Bounds b) => mesh.bounds = b;

            public void Break() => hasPrev = false;

            public void Clear()
            {
                Array.Clear(verts, 0, verts.Length);
                Array.Clear(colors, 0, colors.Length);
                next = 0;
                hasPrev = false;
                dirty = true;
            }

            /// <summary>Adds a quad from the previous wheel point; after a break only remembers the point.</summary>
            public void Add(double x, double y, Color32 col)
            {
                if (hasPrev)
                {
                    double dx = x - px, dy = y - py, len = Math.Sqrt(dx * dx + dy * dy);
                    if (len < 1e-6) return;
                    double nx = -dy / len * HalfWidth, ny = dx / len * HalfWidth;
                    int v = next * 4;
                    verts[v] = At(px + nx, py + ny);
                    verts[v + 1] = At(x + nx, y + ny);
                    verts[v + 2] = At(x - nx, y - ny);
                    verts[v + 3] = At(px - nx, py - ny);
                    colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = col;
                    next = (next + 1) % max; // full ring overwrites the oldest (2D: splice(0, 2))
                    dirty = true;
                }
                px = x; py = y; hasPrev = true;
            }

            static Vector3 At(double x, double y)
            {
                Vector3 p = WorldMapping.ToWorld(x, y);
                p.y = MarkY;
                return p;
            }

            public void Upload()
            {
                if (!dirty) return;
                dirty = false;
                mesh.SetVertices(verts);
                mesh.SetColors(colors);
            }

            public void Dispose()
            {
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            }
        }
    }
}
