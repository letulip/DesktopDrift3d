// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/game-engine.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    /// <summary>
    /// The object raceResults.show({...}) receives at the finish (game-engine.js lines 602–604), with the
    /// newly unlocked achievements awardAchievements returned (lines 282–312).
    /// </summary>
    public sealed class RaceResult
    {
        /// <summary>The `tires` breakdown (line 604).</summary>
        public sealed class TireBreakdown
        {
            public int Pickup;      // tiresEarned (tire pickups this run)
            public int Cap;         // runCaps * CAP_TIRE_VALUE
            public int CleanSweep;  // cleanSweepBonus
            public int FirstClear;  // firstClearBonus
            public int Finish;      // finishBonus = finishPayout(pps)
            public int Trophy;      // trophyBonus (Participation Trophy, 1-PPS finish)
            public int Unbroken;    // unbrokenBonus (Perpetual Motion)
        }

        public int Score;                                   // totalScore = Math.round(S.score)
        public double? BestLap;                             // S.bestLap
        public List<RaceState.LapScore> LapScores = new List<RaceState.LapScore>(); // S.lapScores
        public bool IsNewRecord;
        public double Pps;
        public double TotalTime;
        public bool Ddk;                                    // isDDK(pps)
        public List<Achievements.UnlockedEntry> Unlocked = new List<Achievements.UnlockedEntry>();
        public int CarModel;                                // S.carModel
        public Dictionary<string, object?> Look = new Dictionary<string, object?>(); // carLook(S.carModel)
        public string TrackName = "";                       // baseName (without " (reversed)")
        public bool Reversed;
        public TireBreakdown Tires = new TireBreakdown();

        /// <summary>Not passed by the JS: race-results.js derives it as starsForPps(pps).</summary>
        public int Stars { get { return Economy.StarsForPps(Pps); } }
    }
}
