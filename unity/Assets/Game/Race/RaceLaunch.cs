#nullable enable
using DesktopDrift.Core;

namespace DesktopDrift.Game
{
    public enum RaceMode { TimeAttack, Sandbox, Zen }

    /// <summary>What the menu hands the race scene (SPEC-02 step 4: Time Attack, Sandbox, Zen).</summary>
    public static class RaceLaunch
    {
        public static RaceMode Mode = RaceMode.TimeAttack;
        public static int CarIndex = 0;
        public static int? Laps = null;
        public static bool AutoStart = true;
        // Real-track selection for Time Attack / Zen (2D game.html?track=<id>[&dir=rev]); Sandbox is always the oval.
        public static string? TrackId = null;
        public static bool Reversed = false;

        public static TrackData ResolveTrack(TrackCatalog? catalog)
        {
            if (Mode == RaceMode.Sandbox || catalog == null || catalog.Tracks.Count == 0) return OvalTrack.Build();
            return TrackRuntime.Load(catalog, TrackId ?? catalog.Tracks[0].Id, Reversed);
        }

        // The oval has TrackData.Laps = 0, so the lap count comes from RaceOptions.Laps
        // (a real track's own Laps overrides it in RaceRules.Start). Mirrors the 2D engine traces.
        public static RaceOptions BuildOptions(ContentDescriptor content, int carCount)
        {
            var o = new RaceOptions { Reversed = Mode != RaceMode.Sandbox && Reversed, CarCount = carCount, Content = content };
            switch (Mode)
            {
                case RaceMode.Sandbox:
                    o.Laps = 0; o.Zen = false; o.InitItems = false; o.Stock = true;
                    break;
                case RaceMode.Zen:
                    o.Laps = Laps ?? 3; o.Zen = true; o.InitItems = true; o.Stock = false;
                    break;
                default:
                    o.Laps = Laps ?? 3; o.Zen = false; o.InitItems = true; o.Stock = false;
                    break;
            }
            return o;
        }

        public static string ModeTitle(RaceMode m) => m switch
        {
            RaceMode.Sandbox => "Sandbox",
            RaceMode.Zen => "Zen Drift",
            _ => "Time Attack",
        };
    }
}
