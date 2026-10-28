#nullable enable
using DesktopDrift.Core;
using UnityEngine;

namespace DesktopDrift.Game
{
    /// <summary>
    /// SPEC-02 step 10: visual answer to RaceRules events only (no rules): a soft ring under the car.
    /// DriftStart = green, DriftEnd = fade out, NearMiss = yellow flash, ConeHit = orange flash (colours as the 2D flashes).
    /// </summary>
    public sealed class DriftZones : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color DriftColor = new Color32(0x9b, 0xe3, 0x7a, 255);
        static readonly Color NearMissColor = new Color32(0xff, 0xd3, 0x6a, 255);
        static readonly Color ConeHitColor = new Color32(0xff, 0xb1, 0x4d, 255);
        const float DriftAlpha = 0.5f, FlashAlpha = 0.8f, FadeTime = 0.3f, FlashTime = 0.25f;

        [SerializeField] RaceLoop? loop;
        [SerializeField] CarView? carView;
        [SerializeField] Material? material;

        MeshRenderer? _ring;
        MaterialPropertyBlock? _block;
        bool _drifting;
        float _fade;           // seconds left of the DriftEnd fade
        float _flash;          // seconds left of a flash
        Color _flashColor;

        public float CurrentAlpha { get; private set; }
        public Color CurrentColor { get; private set; }

        void OnEnable()
        {
            if (loop != null) { loop.OnEvent += OnRaceEvent; loop.OnReset += OnReset; }
        }

        void OnDisable()
        {
            if (loop != null) { loop.OnEvent -= OnRaceEvent; loop.OnReset -= OnReset; }
        }

        void OnReset() { _drifting = false; _fade = _flash = 0f; }

        public void OnRaceEvent(RaceEvent e)
        {
            switch (e.Kind)
            {
                case RaceEvent.EventKind.DriftStart: _drifting = true; _fade = 0f; break;
                case RaceEvent.EventKind.DriftEnd: if (_drifting) _fade = FadeTime; _drifting = false; break;
                case RaceEvent.EventKind.NearMiss: _flash = FlashTime; _flashColor = NearMissColor; break;
                case RaceEvent.EventKind.ConeHit: _flash = FlashTime; _flashColor = ConeHitColor; break;
            }
        }

        public void Tick(float dt)
        {
            if (_fade > 0f) _fade = Mathf.Max(0f, _fade - dt);
            if (_flash > 0f) _flash = Mathf.Max(0f, _flash - dt);
            Color c = DriftColor;
            float a = 0f;
            if (_flash > 0f) { c = _flashColor; a = FlashAlpha * (_flash / FlashTime); }
            else if (_drifting) a = DriftAlpha;
            else if (_fade > 0f) a = DriftAlpha * (_fade / FadeTime);
            CurrentColor = c;
            CurrentAlpha = a;
        }

        void Update()
        {
            Tick(Time.deltaTime);
            if (_ring == null) _ring = BuildRing();
            if (_ring == null) return;
            _ring.enabled = CurrentAlpha > 0f;
            if (!_ring.enabled) return;
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(ColorId, new Color(CurrentColor.r, CurrentColor.g, CurrentColor.b, CurrentAlpha));
            _ring.SetPropertyBlock(_block);
        }

        MeshRenderer? BuildRing()
        {
            if (carView == null) return null;
            double len = loop != null && loop.Car != null ? loop.Car.Len : 82;
            float d = 2.2f * (float)len * WorldMapping.UnitsToMeters;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "DriftRing";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(carView.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.0015f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(d, d, 1f);
            var r = go.GetComponent<MeshRenderer>();
            if (material != null) r.sharedMaterial = material;
            r.enabled = false;
            return r;
        }
    }
}
