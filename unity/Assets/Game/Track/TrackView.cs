#nullable enable
using System.Collections.Generic;
using DesktopDrift.Core;
using UnityEngine;

namespace DesktopDrift.Game
{
    /// <summary>Track view: table (rect/ellipse + edge), floor, 2D round-stroke ribbon, cones, props; theme colours via MaterialPropertyBlock.</summary>
    public sealed class TrackView : MonoBehaviour
    {
        [SerializeField] Material? tableMaterial;
        [SerializeField] Material? ribbonMaterial;
        [SerializeField] Material? coneMaterial;
        [SerializeField] Material? propMaterial;
        [SerializeField] Material? overlayMaterial;   // vertex-colour transparent, queue 3001: draws over the skid marks (flag, ring)
        [SerializeField] TextAsset? renderJson;   // Content/_json/render.json: THEME_DEFAULT of the 2D render.js

        const float RibbonY = 0.0005f;
        public const float FlagY = 0.0012f;         // 1.2 mm: above the skid marks (1 mm), as 2D draws the flag after them
        public const float FlagCell = 10f;          // 2D: cell = 10 units, rows = 2
        public const int FlagRows = 2;
        const float EdgeBandY = 0.0002f;        // flat edge band: 0.2 mm above the table top
        const float TableDepthUnits = 30f;      // side faces / floor depth, 2D units
        const float EdgeBandUnits = 12f;        // 2D: tableEdge lineWidth = 12, centred on the boundary
        const int EllipseSegments = 128;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>The theme in force for the current track: `{ ...THEME_DEFAULT, ...track.Theme }` (2D render.js), never an empty string.</summary>
        public TrackTheme? EffectiveTheme { get; private set; }

        TrackTheme? defaultTheme;

        /// <summary>Merge of a track's own theme with THEME_DEFAULT parsed from the serialized render.json.</summary>
        public TrackTheme ThemeFor(TrackData track)
        {
            if (defaultTheme == null)
            {
                if (renderJson == null) throw new System.InvalidOperationException("TrackView.renderJson is not assigned");
                defaultTheme = TrackTheme.ParseDefault(renderJson.text);
            }
            return TrackTheme.Merge(track.Theme, defaultTheme);
        }

        public List<Transform> ConeViews { get; } = new List<Transform>();

        public void Build(TrackData track)
        {
            Clear();
            EffectiveTheme = ThemeFor(track);
            BuildTable(track);
            BuildRibbon(track);
            BuildFlag(track);
            BuildCones(track);
            BuildProps(track);
        }

        void Clear()
        {
            ConeViews.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }
        }

        GameObject MeshObject(string name, Mesh mesh, Material? mat, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            if (mat != null) r.sharedMaterial = mat;
            Tint(r, color);
            return go;
        }

        GameObject Primitive(PrimitiveType type, string name, Material? mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            go.transform.SetParent(transform, false);
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        void BuildTable(TrackData track)
        {
            var theme = EffectiveTheme!;
            Color table = Resolve(theme.Table);
            Color edge = Resolve(theme.TableEdge);
            Color background = Resolve(theme.Background);

            bool round = track.Table.Round;
            float hw = (float)track.Table.W * WorldMapping.UnitsToMeters * 0.5f;
            float hh = (float)track.Table.H * WorldMapping.UnitsToMeters * 0.5f;
            float depth = TableDepthUnits * WorldMapping.UnitsToMeters;

            // Top face (y = 0): a fan around the centre for the ellipse, two triangles for the rectangle.
            var top = new List<Vector3>();
            var topTris = new List<int>();
            if (round)
            {
                top.Add(Vector3.zero);
                for (int i = 0; i < EllipseSegments; i++) top.Add(EllipsePoint(hw, hh, i));
                for (int i = 0; i < EllipseSegments; i++) { topTris.Add(0); topTris.Add(1 + i); topTris.Add(1 + (i + 1) % EllipseSegments); }
            }
            else
            {
                top.AddRange(RectPoints(hw, hh));
                topTris.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            }
            OrientUp(top, topTris);
            MeshObject("Table", FlatMesh("Table", top, topTris), tableMaterial, table);

            // Edge: a vertical skirt `depth` deep around the boundary plus a flat band 12 units wide centred on it.
            float half = EdgeBandUnits * 0.5f * WorldMapping.UnitsToMeters;
            var outline = Outline(round, hw, hh, 0f);
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            int n = outline.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = outline[i], p1 = outline[(i + 1) % n];
                Vector3 outward = new Vector3(p1.z - p0.z, 0f, -(p1.x - p0.x)).normalized; // clockwise outline seen from above
                if (Vector3.Dot(outward, 0.5f * (p0 + p1)) < 0f) outward = -outward;
                int v = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p1 + Vector3.down * depth); verts.Add(p0 + Vector3.down * depth);
                for (int k = 0; k < 4; k++) normals.Add(outward);
                var q = new[] { v, v + 1, v + 2, v, v + 2, v + 3 };
                // Seen from outside the quad must be clockwise: Unity's Cross is then along the outward normal.
                if (Vector3.Dot(Vector3.Cross(verts[q[1]] - verts[q[0]], verts[q[2]] - verts[q[0]]), outward) < 0f)
                    for (int t = 0; t < 6; t += 3) (q[t + 1], q[t + 2]) = (q[t + 2], q[t + 1]);
                tris.AddRange(q);
            }
            var outer = Outline(round, hw, hh, +half);
            var inner = Outline(round, hw, hh, -half);
            int bandStart = verts.Count, bandTriStart = tris.Count;
            for (int i = 0; i < n; i++)
            {
                verts.Add(outer[i] + Vector3.up * EdgeBandY);
                verts.Add(inner[i] + Vector3.up * EdgeBandY);
                normals.Add(Vector3.up); normals.Add(Vector3.up);
            }
            for (int i = 0; i < n; i++)
            {
                int o0 = bandStart + i * 2, i0 = o0 + 1, o1 = bandStart + ((i + 1) % n) * 2, i1 = o1 + 1;
                tris.AddRange(new[] { o0, i0, o1, i0, i1, o1 });
            }
            OrientUp(verts, tris, bandTriStart);
            var edgeMesh = new Mesh { name = "TableEdge" };
            edgeMesh.SetVertices(verts);
            edgeMesh.SetNormals(normals);
            edgeMesh.SetTriangles(tris, 0);
            MeshObject("TableEdge", edgeMesh, tableMaterial, edge);

            // Floor at the table bottom, 10x the table extents, in the theme background.
            float fx = 5f * hw * 2f, fz = 5f * hh * 2f;
            var floor = new List<Vector3>
            {
                new Vector3(-fx, -depth, -fz), new Vector3(-fx, -depth, fz), new Vector3(fx, -depth, fz), new Vector3(fx, -depth, -fz),
            };
            var floorTris = new List<int> { 0, 1, 2, 0, 2, 3 };
            OrientUp(floor, floorTris);
            MeshObject("Floor", FlatMesh("Floor", floor, floorTris), tableMaterial, background);

            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = background;
        }

        void BuildRibbon(TrackData track)
        {
            // The 2D renderer's round stroke (RibbonGeometry): 2D vertices -> world, y a hair above the table.
            var rib = RibbonGeometry.Build(track.Center, track.TrackHalf);
            var verts = new List<Vector3>(rib.Vertices.Count);
            foreach (var v in rib.Vertices) verts.Add(WorldMapping.ToWorld(v) + Vector3.up * RibbonY);
            var tris = new List<int>(rib.Triangles);
            OrientUp(verts, tris);
            var mesh = FlatMesh("Ribbon", verts, tris);
            MeshObject("Ribbon", mesh, ribbonMaterial, Resolve(EffectiveTheme!.Track));
        }

        /// <summary>Vertex colour for an overlay: Unity does not convert vertex colours, so in a linear project the sRGB value is linearised.</summary>
        public static Color OverlayColor(ThemeColor c)
        {
            var col = new Color((float)c.R, (float)c.G, (float)c.B, (float)c.A);
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? col.linear : col;
        }

        // The chequered flag of render.js: rows x cols cells of 10 units in the start frame (X = travel, Y = across), centred
        // on Center[0]; cells are white 0.92 / black 0.82 over the ribbon, pre-composited here over the ribbon colour.
        void BuildFlag(TrackData track)
        {
            int cols = (int)System.Math.Ceiling(track.TrackHalf * 2 / FlagCell);
            ThemeColor ribbon = ThemeColor.Parse(EffectiveTheme!.Track);
            Color white = OverlayColor(ThemeColor.Over(new ThemeColor(1, 1, 1, 0.92), ribbon));
            Color black = OverlayColor(ThemeColor.Over(new ThemeColor(0, 0, 0, 0.82), ribbon));
            var c0 = track.Center[0];
            double ca = System.Math.Cos(track.StartAngle), sa = System.Math.Sin(track.StartAngle);
            var verts = new List<Vector3>(4 * FlagRows * cols);
            var colors = new List<Color>(4 * FlagRows * cols);
            var tris = new List<int>(6 * FlagRows * cols);
            for (int r = 0; r < FlagRows; r++)
                for (int c = 0; c < cols; c++)
                {
                    double x0 = -FlagRows * FlagCell / 2 + r * FlagCell, y0 = -track.TrackHalf + c * FlagCell;
                    int v = verts.Count;
                    foreach (var (lx, ly) in new[] { (x0, y0), (x0, y0 + FlagCell), (x0 + FlagCell, y0 + FlagCell), (x0 + FlagCell, y0) })
                    {
                        verts.Add(WorldMapping.ToWorld(c0.X + lx * ca - ly * sa, c0.Y + lx * sa + ly * ca) + Vector3.up * FlagY);
                        colors.Add((r + c) % 2 == 0 ? white : black);
                    }
                    tris.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
                }
            var mesh = new Mesh { name = "Flag" };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("Flag");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            if (overlayMaterial != null) mr.sharedMaterial = overlayMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // Unity front faces are clockwise seen from the camera: flip every triangle (once) when the net normal points down.
        static void OrientUp(List<Vector3> verts, List<int> tris, int from = 0)
        {
            double sum = 0;
            for (int t = from; t < tris.Count; t += 3)
                sum += Vector3.Cross(verts[tris[t + 1]] - verts[tris[t]], verts[tris[t + 2]] - verts[tris[t]]).y;
            if (sum >= 0) return;
            for (int t = from; t < tris.Count; t += 3) (tris[t + 1], tris[t + 2]) = (tris[t + 2], tris[t + 1]);
        }

        static Mesh FlatMesh(string name, List<Vector3> verts, List<int> tris)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            var normals = new Vector3[verts.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector3 EllipsePoint(float a, float b, int i)
        {
            float t = 2f * Mathf.PI * i / EllipseSegments;
            return new Vector3(a * Mathf.Cos(t), 0f, b * Mathf.Sin(t));
        }

        static Vector3[] RectPoints(float hw, float hh)
        {
            return new[] { new Vector3(-hw, 0, -hh), new Vector3(-hw, 0, hh), new Vector3(hw, 0, hh), new Vector3(hw, 0, -hh) };
        }

        // Table boundary offset by `grow` (metres, + outward): miter corners for the rectangle, the exact offset curve for the ellipse.
        static List<Vector3> Outline(bool round, float hw, float hh, float grow)
        {
            var pts = new List<Vector3>();
            if (!round)
            {
                pts.AddRange(RectPoints(hw + grow, hh + grow));
                return pts;
            }
            for (int i = 0; i < EllipseSegments; i++)
            {
                Vector3 p = EllipsePoint(hw, hh, i);
                Vector3 n = new Vector3(p.x / (hw * hw), 0f, p.z / (hh * hh)).normalized;
                pts.Add(p + n * grow);
            }
            return pts;
        }

        static Color Resolve(string s)
        {
            ThemeColor c = ThemeColor.Parse(s);
            return new Color((float)c.R, (float)c.G, (float)c.B, 1f);
        }

        static void Tint(Renderer r, Color c)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, c);
            r.SetPropertyBlock(block);
        }

        void BuildCones(TrackData track)
        {
            float r = (float)track.ConeR * WorldMapping.UnitsToMeters;
            for (int i = 0; i < track.Cones.Count; i++)
            {
                var c = track.Cones[i];
                var go = Primitive(PrimitiveType.Cylinder, "Cone" + i, coneMaterial);
                Tint(go.GetComponent<Renderer>(), Resolve(EffectiveTheme!.Cone));
                // Cylinder primitive: diameter 1, height 2 at scale 1; height 2r needs scale.y = r; base sits on the table.
                go.transform.localScale = new Vector3(2f * r, r, 2f * r);
                go.transform.position = WorldMapping.ToWorld(c.X, c.Y) + Vector3.up * r;
                ConeViews.Add(go.transform);
            }
        }

        void BuildProps(TrackData track)
        {
            for (int i = 0; i < track.Props.Count; i++)
            {
                var p = track.Props[i];
                float r = (float)p.R * WorldMapping.UnitsToMeters;
                float hl = (float)p.Hl * WorldMapping.UnitsToMeters;
                var go = Primitive(PrimitiveType.Capsule, "Prop" + i, propMaterial);
                // Capsule primitive: radius 0.5, height 2 along Y. scale.y = hl + r gives total length 2hl + 2r
                // (Hl = 0 -> a disc of radius r). The axis is laid flat below, so local Z becomes the height:
                // flattened to h so dishes read as plates, not as balls taller than the car (placeholder until SPEC-03/05).
                float h = Mathf.Max(0.010f, 0.25f * r);
                go.transform.localScale = new Vector3(2f * r, hl + r, h);
                go.transform.position = WorldMapping.ToWorld(p.X, p.Y) + Vector3.up * (0.5f * h);
                // Euler(90,0,0) lays the capsule axis on local +Z (the car-forward convention), then yaw by Ang.
                go.transform.rotation = WorldMapping.Yaw(p.Ang) * Quaternion.Euler(90f, 0f, 0f);
            }
        }
    }
}
