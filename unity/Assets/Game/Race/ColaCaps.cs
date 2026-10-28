#nullable enable
using System.Collections.Generic;
using DesktopDrift.Core;
using UnityEngine;

namespace DesktopDrift.Game
{
    /// <summary>
    /// SPEC-02 step 10: cola caps as flat discs with a radial-fill shader. Fill = Core's RaceState.Caps[i].Sweep,
    /// pop = Caps[i].Pop (Core decays it); collection itself is Core's. Nothing is computed here.
    /// </summary>
    public sealed class ColaCaps : MonoBehaviour
    {
        static readonly int SweepId = Shader.PropertyToID("_Sweep");

        [SerializeField] RaceLoop? loop;
        [SerializeField] Material? material;

        /// <summary>By collectible index; null for collectibles that are not cola caps.</summary>
        public List<GameObject?> Views { get; } = new List<GameObject?>();

        readonly List<float> _baseScale = new List<float>();
        MaterialPropertyBlock? _block;

        void OnEnable()
        {
            if (loop == null) return;
            loop.OnStepped += OnStepped;
            loop.OnReset += OnReset;
        }

        void OnDisable()
        {
            if (loop == null) return;
            loop.OnStepped -= OnStepped;
            loop.OnReset -= OnReset;
        }

        void Start()
        {
            if (loop != null && Views.Count == 0 && loop.Track != null && loop.Rules != null) OnReset();
        }

        void OnReset()
        {
            Build(loop!.Track);
            OnStepped();
        }

        public void Build(TrackData track)
        {
            Clear();
            foreach (Collectible c in track.Collectibles)
            {
                if (c.Kind != "cola") { Views.Add(null); _baseScale.Add(0f); continue; }
                float r = (float)c.R * WorldMapping.UnitsToMeters;
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "Cap" + Views.Count;
                var col = go.GetComponent<Collider>();
                if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
                go.transform.SetParent(transform, false);
                go.transform.position = WorldMapping.ToWorld(c.X, c.Y) + Vector3.up * 0.002f;
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, normal up
                go.transform.localScale = new Vector3(2f * r, 2f * r, 1f);
                if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
                Views.Add(go);
                _baseScale.Add(2f * r);
            }
        }

        void OnStepped()
        {
            if (loop == null || loop.Rules == null) return;
            var caps = loop.State.Caps;
            _block ??= new MaterialPropertyBlock();
            for (int i = 0; i < Views.Count && i < caps.Count; i++)
            {
                var go = Views[i];
                if (go == null) continue;
                var cap = caps[i];
                // 2D render.js:521 draws the fill over the full collection sweep (CapLoops = 2 turns):
                // progress = min(1, |sweep| / (2π·CAP_LOOPS)); Cola.CapProgress is the one-turn rule helper.
                float sweep = cap.Collected ? 1f : (float)System.Math.Min(1.0, System.Math.Abs(cap.Sweep) / (2.0 * System.Math.PI * RaceRules.CapLoops));
                float s = _baseScale[i] * (cap.Collected ? 1f + (float)cap.Pop : 1f);
                var rend = go.GetComponent<MeshRenderer>();
                rend.GetPropertyBlock(_block);
                _block.SetFloat(SweepId, sweep);
                rend.SetPropertyBlock(_block);
                go.transform.localScale = new Vector3(s, s, 1f);
            }
        }

        void Clear()
        {
            Views.Clear();
            _baseScale.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }
        }
    }
}
