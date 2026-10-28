#nullable enable
using System.Collections.Generic;
using DesktopDrift.Core;
using UnityEngine;

namespace DesktopDrift.Game
{
    /// <summary>
    /// SPEC-02 step 10: tire pickups shown from Track.Collectibles. Core decides the pickup (RaceRules.UpdateCaps);
    /// this view only hides the pickup on TireCollected and rebuilds every race (2D: tires respawn each race).
    /// </summary>
    public sealed class Tires : MonoBehaviour
    {
        [SerializeField] RaceLoop? loop;
        [SerializeField] Material? material;

        /// <summary>By collectible index; null for collectibles that are not tires.</summary>
        public List<GameObject?> Views { get; } = new List<GameObject?>();

        void OnEnable()
        {
            if (loop == null) return;
            loop.OnEvent += OnRaceEvent;
            loop.OnReset += OnReset;
        }

        void OnDisable()
        {
            if (loop == null) return;
            loop.OnEvent -= OnRaceEvent;
            loop.OnReset -= OnReset;
        }

        void Start()
        {
            if (loop != null && Views.Count == 0 && loop.Track != null && loop.Rules != null) Build(loop.Track);
        }

        void OnReset() => Build(loop!.Track);

        public void OnRaceEvent(RaceEvent e)
        {
            if (e.Kind != RaceEvent.EventKind.TireCollected) return;
            if (e.Index >= 0 && e.Index < Views.Count) Views[e.Index]?.SetActive(false);
        }

        public void Build(TrackData track)
        {
            Clear();
            foreach (Collectible c in track.Collectibles)
            {
                if (c.Kind != "tire") { Views.Add(null); continue; }
                float r = (float)c.R * WorldMapping.UnitsToMeters;
                float h = 0.5f * r;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "Tire" + Views.Count;
                var col = go.GetComponent<Collider>();
                if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
                go.transform.SetParent(transform, false);
                // Cylinder primitive: diameter 1, height 2 at scale 1.
                go.transform.localScale = new Vector3(2f * r, 0.5f * h, 2f * r);
                go.transform.position = WorldMapping.ToWorld(c.X, c.Y) + Vector3.up * (0.5f * h);
                if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
                Views.Add(go);
            }
        }

        void Clear()
        {
            Views.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }
        }
    }
}
