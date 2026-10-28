// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/state.js + js/game-engine.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    /// <summary>
    /// All race state: the scalars of S_DEFAULTS (state.js) with the same defaults, S.lapScores, S.caps,
    /// the `car` singleton, the cones, and the closure-local state startGame keeps between frames.
    /// S.skids (skid marks) is render-only and NOT ported: the renderer builds its own trail.
    /// </summary>
    public sealed class RaceState
    {
        /// <summary>S.lapScores entry { n, pts, t }.</summary>
        public sealed class LapScore
        {
            public int N;
            public int Pts;
            public double T;
        }

        /// <summary>S.caps[i] = { trackId, sweep, prevAng, collected, pop } (game-engine.js:102).</summary>
        public sealed class CapState
        {
            public string TrackId = "";
            public double Sweep;
            public double? PrevAng;  // null = car was last outside the donut
            public bool Collected;
            public double Pop;
        }

        // ── S_DEFAULTS (state.js) ───────────────────────────────────────────────
        public int CarModel = 0;
        /// <summary>state.js steerInput – declared but never written by the engine.</summary>
        public double SteerInput = 0;

        public double Score = 0;
        public double ComboPoints = 0;
        public double Mult = 1;
        public double DriftTime = 0;
        public int Transitions = 0;
        public double DriftGrace = 0;
        public int LastSlipSign = 0;
        public double MultBuild = 0;
        public int NearMisses = 0;
        public double NearMissCd = 0;
        public double CrashCd = 0;

        public string FlashMsg = "";
        public double FlashT = 0;
        public string FlashColor = "#fff";

        public double LapTime = 0;
        public double? LastLap = null;
        public double? BestLap = null;
        public bool LapStarted = true;
        public int NextCp = 1;
        public int LapNum = 0;
        public double LapScoreStart = 0;

        public double StartCd = 3.0;
        public double GoT = 0;

        public bool Zen = false;
        public bool Reversed = false;

        /// <summary>S.steerSmooth / S.physT live here – the single source of truth CarPhysics.StepCar mutates.</summary>
        public readonly StepMotion Motion = new StepMotion();
        public double SteerSmooth { get { return Motion.SteerSmooth; } set { Motion.SteerSmooth = value; } }
        public double PhysT { get { return Motion.PhysT; } set { Motion.PhysT = value; } }

        // ── S arrays ────────────────────────────────────────────────────────────
        public readonly List<LapScore> LapScores = new List<LapScore>();
        /// <summary>S.caps by collectible index – cola caps AND tires (Collected = picked up this run / ever for caps).</summary>
        public readonly List<CapState> Caps = new List<CapState>();

        // ── state.js car + the track's cones ────────────────────────────────────
        public readonly CarState Car = new CarState();
        public List<Cone> Cones = new List<Cone>();

        // ── startGame closure (game-engine.js lines 60–67, 210–214, 266–277, 377) ─
        public int TotalLaps;          // TOTAL_LAPS (0 = infinite sandbox)
        public string Instance = "";   // INSTANCE = instanceId(T.id ?? '', REVERSED)
        public int NearIdx = 0;
        public int DriftZoneRef = 0;
        public double DriftZoneTimer = 0;
        public bool DriftZoned = false;
        public int CdBeep = 0;
        public double? PrevFinishDot = null;
        public bool RaceFinished = false;
        public bool GameplayActive = false;
        public int TiresEarned = 0;
        public int RunTirePickups = 0;
        public int RunNearMisses = 0;
        public int RunCrashes = 0;
        public double RunTimeAt8 = 0;
        public double RunDriftSecs = 0;
        public int RunCaps = 0;
        public bool ComboUnbroken = true;

        // ── drift(sliding, slip, active) – the audio layer reads these every frame ─
        public bool DriftSoundOn = false;
        public double DriftSoundLevel = 0;
        public bool DriftSoundActive = false;
    }
}
