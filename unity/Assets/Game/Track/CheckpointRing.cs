#nullable enable
using DesktopDrift.Core;
using UnityEngine;

namespace DesktopDrift.Game
{
    /// <summary>
    /// The ring on the NEXT checkpoint, as js/render.js draws it: a circle of radius CP_R stroked twice - a dark band
    /// (width 9, rgba(0,0,0,0.55)) and over it a cyan band (width 5, #7dd4ff). One mesh, dark triangles first so cyan
    /// draws over dark. Shown only while `!Zen && NextCp != 0`. (theme.checkpoint is unused by the 2D renderer.)
    /// </summary>
    public sealed class CheckpointRing : MonoBehaviour
    {
        [SerializeField] RaceLoop? loop;
        [SerializeField] Material? material;

        public const float RingY = 0.0014f;          // above the flag (1.2 mm) and the skids (1 mm)
        public const float DarkWidth = 9f;           // 2D lineWidth, units
        public const float CyanWidth = 5f;
        public const int Segments = 64;

        MeshRenderer? meshRenderer;
        MeshFilter? meshFilter;
        Mesh? mesh;

        public bool Visible => meshRenderer != null && meshRenderer.enabled;

        void OnEnable()
        {
            if (loop == null) return;
            loop.OnReset += HandleReset;
            if (loop.Track != null) Rebuild(loop.Track);
        }

        void OnDisable()
        {
            if (loop != null) loop.OnReset -= HandleReset;
        }

        void OnDestroy()
        {
            if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }

        void HandleReset()
        {
            if (loop != null && loop.Track != null) Rebuild(loop.Track);
        }

        void LateUpdate()
        {
            if (loop == null || loop.Track == null) return;
            Place(loop.Track, loop.Rules != null ? loop.State : null);
        }

        /// <summary>Rebuild the ring mesh for a track (radius CP_R), centred on the object's origin.</summary>
        public void Rebuild(TrackData track)
        {
            EnsureRenderer();
            float r = (float)track.CpR * WorldMapping.UnitsToMeters;
            float hd = 0.5f * DarkWidth * WorldMapping.UnitsToMeters;
            float hc = 0.5f * CyanWidth * WorldMapping.UnitsToMeters;
            var verts = new Vector3[4 * Segments];
            var cols = new Color[verts.Length];
            var tris = new int[2 * 6 * Segments];
            Color dark = TrackView.OverlayColor(new ThemeColor(0, 0, 0, 0.55));
            Color cyan = TrackView.OverlayColor(ThemeColor.Parse("#7dd4ff"));
            AddBand(verts, cols, tris, 0, r - hd, r + hd, dark);
            AddBand(verts, cols, tris, 1, r - hc, r + hc, cyan);   // later triangles draw over the dark band
            mesh!.Clear();
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
        }

        /// <summary>Position the ring on the next checkpoint; hidden for Zen, NextCp == 0 or no race state.</summary>
        public void Place(TrackData track, RaceState? state)
        {
            EnsureRenderer();
            bool show = state != null && !state.Zen && state.NextCp != 0 && state.NextCp > 0 && state.NextCp < track.Checkpoints.Count;
            meshRenderer!.enabled = show;
            if (!show) return;
            var cp = track.Checkpoints[state!.NextCp];
            transform.position = WorldMapping.ToWorld(cp.X, cp.Y);
        }

        static void AddBand(Vector3[] verts, Color[] cols, int[] tris, int band, float rIn, float rOut, Color color)
        {
            int v0 = band * 2 * Segments, t0 = band * 6 * Segments;
            for (int i = 0; i < Segments; i++)
            {
                float a = 2f * Mathf.PI * i / Segments;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts[v0 + 2 * i] = new Vector3(c * rOut, RingY, s * rOut);
                verts[v0 + 2 * i + 1] = new Vector3(c * rIn, RingY, s * rIn);
                cols[v0 + 2 * i] = cols[v0 + 2 * i + 1] = color;
                int o0 = v0 + 2 * i, i0 = o0 + 1, o1 = v0 + 2 * ((i + 1) % Segments), i1 = o1 + 1;
                int t = t0 + 6 * i;
                tris[t] = o0; tris[t + 1] = i0; tris[t + 2] = o1;
                tris[t + 3] = i0; tris[t + 4] = i1; tris[t + 5] = o1;
            }
        }

        void EnsureRenderer()
        {
            if (mesh == null) mesh = new Mesh { name = "CheckpointRing" };
            MeshFilter filter = meshFilter != null ? meshFilter : GetOrAdd<MeshFilter>();
            MeshRenderer rend = meshRenderer != null ? meshRenderer : GetOrAdd<MeshRenderer>();
            meshFilter = filter;
            meshRenderer = rend;
            filter.sharedMesh = mesh;
            if (material != null) rend.sharedMaterial = material;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        T GetOrAdd<T>() where T : Component => TryGetComponent(out T found) ? found : gameObject.AddComponent<T>();
    }
}
