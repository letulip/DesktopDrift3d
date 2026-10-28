// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/track-factory.js @ 5556140 (2026-08-06), ported 2026-10-28.
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    /// <summary>
    /// JS: the object returned by the track factory (track-factory.js lines 140–151), plus the
    /// `reversed` flag that reverseTrack (track-util.js) adds.
    /// </summary>
    public sealed class TrackData
    {
        public double TrackHalf;   // TRACK_HALF = 100
        public double ConeR;       // CONE_R = 9
        public int K;              // K = 8
        public double CpR;         // CP_R = TRACK_HALF + 70
        public List<Vec2> Center = new List<Vec2>();
        public List<Vec2> Outer = new List<Vec2>();
        public List<Vec2> Inner = new List<Vec2>();
        public List<Cone> Cones = new List<Cone>();
        public TableRect Table = new TableRect();
        public List<Prop> Props = new List<Prop>();
        public List<Collectible> Collectibles = new List<Collectible>();
        public List<Vec2> Checkpoints = new List<Vec2>();
        public Vec2 StartPos;
        public double StartAngle;
        public string Id = "";
        public int Laps;
        public TrackTheme Theme = new TrackTheme();
        public bool Reversed;
    }
}
