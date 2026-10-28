#nullable enable
using DesktopDrift.Core;
using UnityEngine;

namespace DesktopDrift.Game
{
    /// <summary>Temporary bootstrap (SPEC-02 step 3 replaces it with RaceLoop).</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] TrackView? trackView;
        [SerializeField] CarView? carView;

        void Awake()
        {
            var t = OvalTrack.Build();
            trackView!.Build(t);
            carView!.Configure(82, 30.220657276995304);
            carView.SetPose(t.StartPos.X, t.StartPos.Y, t.StartAngle);
        }
    }
}
