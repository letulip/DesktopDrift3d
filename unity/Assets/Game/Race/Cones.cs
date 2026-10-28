#nullable enable
using DesktopDrift.Core;
using UnityEngine;

namespace DesktopDrift.Game
{
    /// <summary>
    /// SPEC-02 step 10: the cone views mirror Core's RaceState.Cones every step. A knocked cone is moved by Core
    /// (stepKnockedCone); this view copies position/angle and lays the cone on its side.
    /// </summary>
    public sealed class Cones : MonoBehaviour
    {
        [SerializeField] RaceLoop? loop;
        [SerializeField] TrackView? trackView;

        void OnEnable()
        {
            if (loop == null) return;
            loop.OnStepped += Apply;
            loop.OnReset += Apply;
        }

        void OnDisable()
        {
            if (loop == null) return;
            loop.OnStepped -= Apply;
            loop.OnReset -= Apply;
        }

        void Apply()
        {
            if (loop == null || trackView == null || loop.Rules == null) return;
            var cones = loop.State.Cones;
            var views = trackView.ConeViews;
            float r = (float)loop.Track.ConeR * WorldMapping.UnitsToMeters;
            for (int i = 0; i < cones.Count && i < views.Count; i++)
            {
                Cone c = cones[i];
                var t = views[i];
                // Same height standing (base on the table) and lying (cylinder diameter == height).
                t.position = WorldMapping.ToWorld(c.X, c.Y) + Vector3.up * r;
                t.rotation = c.Knocked ? WorldMapping.Yaw(c.Ang) * Quaternion.Euler(90f, 0f, 0f) : WorldMapping.Yaw(c.Ang);
            }
        }
    }
}
