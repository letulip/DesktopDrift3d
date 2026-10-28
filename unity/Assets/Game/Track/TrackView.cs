#nullable enable
using System.Collections.Generic;
using DesktopDrift.Core;
using UnityEngine;

namespace DesktopDrift.Game
{
    /// <summary>Temporary gray-oval view: table, ribbon from outer/inner, cones, props (SPEC-03 replaces it).</summary>
    public sealed class TrackView : MonoBehaviour
    {
        [SerializeField] Material? tableMaterial;
        [SerializeField] Material? ribbonMaterial;
        [SerializeField] Material? coneMaterial;
        [SerializeField] Material? propMaterial;

        const float RibbonY = 0.0005f;

        public List<Transform> ConeViews { get; } = new List<Transform>();

        public void Build(TrackData track)
        {
            Clear();
            BuildTable(track);
            BuildRibbon(track);
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

        GameObject MeshObject(string name, Mesh mesh, Material? mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            if (mat != null) r.sharedMaterial = mat;
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
            float hw = (float)track.Table.W * WorldMapping.UnitsToMeters * 0.5f;
            float hh = (float)track.Table.H * WorldMapping.UnitsToMeters * 0.5f;
            var mesh = new Mesh { name = "Table" };
            mesh.vertices = new[] { new Vector3(-hw, 0, -hh), new Vector3(-hw, 0, hh), new Vector3(hw, 0, hh), new Vector3(hw, 0, -hh) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            MeshObject("Table", mesh, tableMaterial);
        }

        void BuildRibbon(TrackData track)
        {
            int n = Mathf.Min(track.Outer.Count, track.Inner.Count);
            var verts = new Vector3[n * 2];
            var normals = new Vector3[n * 2];
            for (int i = 0; i < n; i++)
            {
                verts[i * 2] = WorldMapping.ToWorld(track.Outer[i]) + Vector3.up * RibbonY;
                verts[i * 2 + 1] = WorldMapping.ToWorld(track.Inner[i]) + Vector3.up * RibbonY;
                normals[i * 2] = normals[i * 2 + 1] = Vector3.up;
            }
            var tris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                int o0 = i * 2, i0 = o0 + 1, o1 = ((i + 1) % n) * 2, i1 = o1 + 1;
                tris[i * 6 + 0] = o0; tris[i * 6 + 1] = i0; tris[i * 6 + 2] = o1;
                tris[i * 6 + 3] = i0; tris[i * 6 + 4] = i1; tris[i * 6 + 5] = o1;
            }
            // Unity front faces are clockwise seen from the camera: make the normal point up whatever the 2D winding was.
            if (n > 1 && Vector3.Cross(verts[tris[1]] - verts[tris[0]], verts[tris[2]] - verts[tris[0]]).y < 0f)
                for (int t = 0; t < tris.Length; t += 3) (tris[t + 1], tris[t + 2]) = (tris[t + 2], tris[t + 1]);
            var mesh = new Mesh { name = "Ribbon", vertices = verts, normals = normals, triangles = tris };
            MeshObject("Ribbon", mesh, ribbonMaterial);
        }

        void BuildCones(TrackData track)
        {
            float r = (float)track.ConeR * WorldMapping.UnitsToMeters;
            for (int i = 0; i < track.Cones.Count; i++)
            {
                var c = track.Cones[i];
                var go = Primitive(PrimitiveType.Cylinder, "Cone" + i, coneMaterial);
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
