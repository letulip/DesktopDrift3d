// SPEC-03 step 4a: runtime track loading. A reversed instance is the 2D SPA's reverseTrack(T) (TrackUtil.ReverseTrack);
// RaceRules then expects opts.Reversed = true alongside it (RaceLaunch.BuildOptions sets both).
#nullable enable
using System;
using DesktopDrift.Core;

namespace DesktopDrift.Game
{
    public static class TrackRuntime
    {
        public static TrackData Load(TrackCatalog catalog, string id, bool reversed)
        {
            TrackAsset asset = catalog.Find(id) ?? throw new ArgumentException("Unknown track id '" + id + "'", nameof(id));
            TrackData t = asset.ToTrackData();
            return reversed ? TrackUtil.ReverseTrack(t) : t;
        }
    }
}
