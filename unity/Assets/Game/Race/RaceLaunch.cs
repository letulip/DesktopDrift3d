#nullable enable
using DesktopDrift.Core;

namespace DesktopDrift.Game
{
    public enum RaceMode { TimeAttack, Sandbox, Zen }

    /// <summary>What the menu hands the race scene (SPEC-02 step 3: Time Attack only; Sandbox/Zen are step 4).</summary>
    public static class RaceLaunch
    {
        public static RaceMode Mode = RaceMode.TimeAttack;
        public static int CarIndex = 0;
        public static int? Laps = null;
        public static bool AutoStart = true;

        // The oval has TrackData.Laps = 0, so the lap count comes from RaceOptions.Laps.
        public static RaceOptions BuildOptions(ContentDescriptor content, int carCount)
        {
            return new RaceOptions
            {
                Laps = Laps ?? 3, Zen = false, Reversed = false, InitItems = true, Stock = false,
                CarCount = carCount, Content = content,
            };
        }
    }
}
