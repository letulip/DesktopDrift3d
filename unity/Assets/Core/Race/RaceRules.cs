// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/game-engine.js @ 5556140 (2026-08-06), ported 2026-10-28.
//
// startGame (setup) + frame() (one simulation step) + the finish / awardAchievements of the 2D race
// engine, minus DOM, canvas, audio, haptics and navigation: those become RaceEvents returned by Step().
//   pause.isPaused()      ⇒ not ported: the caller does not call Step() while paused.
//   HUD writes (#lapTotal, #spdUnit, #score, toDisplaySpeed) ⇒ not ported: the view reads State.
//   performance.now()     ⇒ the caller's dt.   new Date().getHours() ⇒ hourOfDay().   Math.random() ⇒ random().
//   S.skids (skid marks)  ⇒ render-only, not ported.
//   resolveSteer(pointers, keys, W) ⇒ the caller's steerTarget.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DesktopDrift.Core
{
    public sealed class RaceRules
    {
        // ─── Engine constants ────────────────────────────────────────────────────
        public const double CapInnerR = 40;              // game-engine.js:74 CAP_INNER_R
        public const double CapOuterR = 160;             // game-engine.js:75 CAP_OUTER_R
        public const double CapDecay = Math.PI * 2 / 6;  // game-engine.js:76 CAP_DECAY
        public const int CapTireValue = 15;              // game-engine.js:77 CAP_TIRE_VALUE
        public const double CapLoops = 2;                // game-engine.js:78 CAP_LOOPS
        public const double TireCr = 35;                 // game-engine.js:79 TIRE_CR
        public const double MultResetGrace = 1.2;        // game-engine.js:117 MULT_RESET_GRACE
        public const int ZoneAdv = 8;                    // game-engine.js:207 ZONE_ADV
        public const double ZoneStall = 3.0;             // game-engine.js:208 ZONE_STALL
        public const double WallCrash = 120;             // game-engine.js:437 wallHit threshold
        public const double PropCrash = 100;             // game-engine.js:439 propHit threshold
        public const double NmBand = 42;                 // config.js: NM_BAND
        public const double MaxDt = 0.05;                // game-engine.js:394 dt clamp

        private readonly TrackData _t;
        private readonly DriveProfile _p;
        private readonly double _carLen;
        private readonly double _carWid;
        private readonly PhysicsConstants _k;
        private readonly RaceOptions _opts;
        private readonly SaveState _save;
        private readonly Func<double> _random;
        private readonly Func<int> _hourOfDay;

        private bool _zen;
        private bool _reversed;
        private TableRect _table = new TableRect();
        private double _finishCos, _finishSin;
        private Vec2 _c0;
        private int _nCtr;
        private List<RaceEvent> _ev = new List<RaceEvent>();

        public RaceState State { get; private set; } = new RaceState();
        public TrackData Track { get { return _t; } }

        public RaceRules(TrackData track, DriveProfile drive, double carLen, double carWid, PhysicsConstants k,
            RaceOptions opts, SaveState save, Func<double> random, Func<int> hourOfDay)
        {
            _t = track;
            _p = drive;
            _carLen = carLen;
            _carWid = carWid;
            _k = k;
            _opts = opts;
            _save = save;
            _random = random;
            _hourOfDay = hourOfDay;
        }

        // ─── startGame setup (lines 52–103, 206–214, 266–277, 343–344, 375–377) ─
        public void Start()
        {
            // resetState(): a fresh S with the S_DEFAULTS
            var S = new RaceState();
            State = S;
            _ev = new List<RaceEvent>();
            S.Cones = _t.Cones;
            TrackUtil.ResetCones(S.Cones);
            _table = _t.Table;
            S.TotalLaps = _t.Laps != 0 ? _t.Laps : (_opts.Laps ?? 0); // T.laps ?? opts.laps ?? 0
            _zen = _opts.Zen;
            S.Zen = _zen;
            _reversed = _opts.Reversed;
            S.Reversed = _reversed;
            S.Instance = TrackUtil.InstanceId(_t.Id ?? "", _reversed);

            // initCar(T)
            S.Car.X = _t.StartPos.X; S.Car.Y = _t.StartPos.Y; S.Car.Angle = _t.StartAngle;
            S.Car.Vx = 0; S.Car.Vy = 0;

            // S.caps: restore previously collected CAPS (tires respawn every race). Multi-format lookup
            // (lines 99-101) so no save format silently loses collected state: c.capId ?? i, the old
            // `${c.x},${c.y}` coordinate key, the oldest numeric index i. The store keeps the raw entries
            // (strings and numbers), Set.has = SameValueZero = object.Equals on boxed double / string.
            List<object?> prevCollected = _save.CollectedCaps(S.Instance);
            for (int i = 0; i < _t.Collectibles.Count; i++)
            {
                Collectible c = _t.Collectibles[i];
                bool wasCollected = prevCollected.Contains(CapKey(c, i))
                    || prevCollected.Contains(TrackUtil.JsNumber(c.X) + "," + TrackUtil.JsNumber(c.Y))
                    || prevCollected.Contains((double)i);
                S.Caps.Add(new RaceState.CapState { TrackId = S.Instance, Sweep = 0, PrevAng = null, Collected = wasCollected, Pop = 0 });
            }

            _nCtr = _t.Center.Count;

            // garage(): S.carModel = clamp(g.carIndex ?? 0)
            Dictionary<string, object?> g = _save.Garage();
            double carIndex = g.TryGetValue("carIndex", out object? ci) && ci is double cd ? cd : 0;
            S.CarModel = (int)Math.Max(0, Math.Min(carIndex, _opts.CarCount - 1));

            // Finish line
            _finishCos = Math.Cos(_t.StartAngle);
            _finishSin = Math.Sin(_t.StartAngle);
            _c0 = _t.Checkpoints[0];
            S.PrevFinishDot = null;
        }

        // JS `c.capId ?? i`: TrackBuilder always sets capId; an empty CapId stands for an absent one.
        private static object CapKey(Collectible c, int i)
        {
            return c.CapId.Length > 0 ? (object)c.CapId : (double)i;
        }

        // ─── Helpers (lines 108–156) ─────────────────────────────────────────────
        private RaceEvent Emit(RaceEvent.EventKind kind)
        {
            var e = new RaceEvent(kind);
            _ev.Add(e);
            return e;
        }

        private void Sfx(string id, double value = 0)
        {
            RaceEvent e = Emit(RaceEvent.EventKind.Sfx);
            e.Id = id;
            e.Value = value;
        }

        private void Haptic(string id)
        {
            Emit(RaceEvent.EventKind.Haptic).Id = id;
        }

        private static string Num(double v)
        {
            return CoreMath.JsNumberToString(v);   // JS string concatenation of a number
        }

        private void Flash(string msg, string color)
        {
            RaceState S = State;
            S.FlashMsg = msg;
            S.FlashColor = string.IsNullOrEmpty(color) ? "#fff" : color;
            S.FlashT = 0.9;
            RaceEvent e = Emit(RaceEvent.EventKind.Flash);
            e.Text = msg;
            e.Color = S.FlashColor;
        }

        private void ResetCombo()
        {
            RaceState S = State;
            S.ComboPoints = 0; S.Mult = 1; S.DriftTime = 0;
            S.Transitions = 0; S.LastSlipSign = 0; S.MultBuild = 0; S.NearMisses = 0;
            S.DriftZoneRef = S.NearIdx; S.DriftZoneTimer = 0; S.DriftZoned = false;
        }

        private void BankPoints()
        {
            RaceState S = State;
            if (S.ComboPoints >= 1)
            {
                if (!_zen) S.Score += CoreMath.JsRound(S.ComboPoints);
                Flash("+" + Num(CoreMath.JsRound(S.ComboPoints)) + " banked", "#9be37a");
            }
            S.ComboPoints = 0; S.DriftTime = 0;
        }

        private void ResetMult()
        {
            RaceState S = State;
            S.Mult = 1; S.MultBuild = 0; S.Transitions = 0; S.LastSlipSign = 0; S.NearMisses = 0;
            S.DriftZoneRef = S.NearIdx; S.DriftZoneTimer = 0; S.DriftZoned = false;
        }

        private void BankCombo()
        {
            RaceState S = State;
            if (S.ComboPoints < 1) { ResetCombo(); return; }
            if (!_zen) S.Score += CoreMath.JsRound(S.ComboPoints);
            Flash("+" + Num(CoreMath.JsRound(S.ComboPoints)) + " banked", "#9be37a");
            ResetCombo();
        }

        private void BurnCombo(string reason)
        {
            RaceState S = State;
            RaceEvent burn = Emit(RaceEvent.EventKind.ComboBurn);
            burn.Text = reason;
            burn.Value = S.ComboPoints;
            if (S.ComboPoints >= 1) Flash(reason + "  combo " + Num(CoreMath.JsRound(S.ComboPoints)) + " lost", "#ff6a6a");
            S.ComboUnbroken = false;   // any crash / off-track ends the "one unbroken drift" run
            ResetCombo();
            S.CrashCd = 0.5; S.DriftGrace = 1;
        }

        // ─── updateCaps (lines 158–204) ──────────────────────────────────────────
        private void UpdateCaps(double dt, bool drifting)
        {
            RaceState S = State;
            CarState car = S.Car;
            List<Collectible> collectibles = _t.Collectibles;
            for (int i = 0; i < collectibles.Count; i++)
            {
                RaceState.CapState cap = S.Caps[i];
                if (cap.Collected)
                {
                    if (cap.Pop > 0) cap.Pop = Math.Max(0, cap.Pop - dt);
                    continue;
                }
                Collectible c = collectibles[i];
                double dx = car.X - c.X, dy = car.Y - c.Y;
                double dist = CoreMath.Hypot(dx, dy);

                if (c.Kind == "tire")
                {
                    if (_zen) continue;                 // no tire economy in Zen mode
                    if (dist < c.R + TireCr)
                    {
                        cap.Collected = true;
                        cap.Pop = 0.4;
                        _save.AddTires(c.Value);         // credit live (HUD); ledger logs the per-race sum
                        S.TiresEarned += c.Value;
                        S.RunTirePickups++;
                        Flash("+" + c.Value.ToString(CultureInfo.InvariantCulture) + " tire" + (c.Value != 1 ? "s" : ""), "#ffe48a");
                        RaceEvent e = Emit(RaceEvent.EventKind.TireCollected); // sfx.pickup()
                        e.Index = i;
                        e.Number = c.Value;
                    }
                    continue;
                }

                // kind === 'cola': drift-donut collection
                bool inDonut = dist > CapInnerR && dist < CapOuterR;
                double ang = Math.Atan2(dy, dx);
                bool engaged = inDonut && drifting;
                cap.Sweep = Cola.StepSweep(cap.Sweep, cap.PrevAng ?? ang, ang, engaged, dt, CapDecay);
                cap.PrevAng = engaged ? ang : (double?)null;
                if (Math.Abs(cap.Sweep) >= Math.PI * 2 * CapLoops)
                {
                    cap.Collected = true;
                    cap.Pop = 0.6;
                    cap.Sweep = 0;
                    if (!_zen)
                    {
                        _save.AddTires(CapTireValue, "Cola cap — " + CapTireValue.ToString(CultureInfo.InvariantCulture) + " tires");
                        S.RunCaps++;
                    }
                    Flash("CAP! +" + CapTireValue.ToString(CultureInfo.InvariantCulture) + " tires", "#ff9999");
                    RaceEvent e = Emit(RaceEvent.EventKind.CapCollected); // sfx.cap()
                    e.Index = i;
                    e.Number = CapTireValue;
                    _save.CapCollect(S.Instance, CapKey(c, i));   // capCollect(INSTANCE, c.capId ?? i)
                }
            }
        }

        // ─── hitConeAt (lines 216–231) ───────────────────────────────────────────
        private void HitConeAt(Cone c, int index, double px, double py, double r)
        {
            if (c.Knocked) return;
            RaceState S = State;
            CarState car = S.Car;
            double dx = px - c.X, dy = py - c.Y;
            double rr = r + _t.ConeR;
            if (dx * dx + dy * dy >= rr * rr) return;
            c.Knocked = true;
            Haptic("cone");                                   // hapticCone()
            Emit(RaceEvent.EventKind.ConeHit).Index = index;  // sfx.cone()
            double d = CoreMath.Hypot(dx, dy);
            if (d == 0 || double.IsNaN(d)) d = 1;             // JS `|| 1`
            c.Vx = car.Vx * 0.6 - (dx / d) * 80;
            c.Vy = car.Vy * 0.6 - (dy / d) * 80;
            c.Spin = (_random() - 0.5) * 18;
            car.Vx *= 0.96; car.Vy *= 0.96;
            if (!_zen) S.Score = Math.Max(0, S.Score - 100);
            Flash("Cone!  -100", "#ffb14d");
        }

        // drift(sliding, slip, active) / stopDrift(): state for the audio layer + edge events.
        private void Drift(bool on, double level, bool active)
        {
            RaceState S = State;
            bool was = S.DriftSoundOn;
            S.DriftSoundOn = on;
            S.DriftSoundLevel = level;
            S.DriftSoundActive = active;
            if (on && !was) Emit(RaceEvent.EventKind.DriftStart).Value = level;
            else if (!on && was) Emit(RaceEvent.EventKind.DriftEnd).Value = level;
        }

        // ─── awardAchievements (lines 282–312) ───────────────────────────────────
        private static double StatNum(Dictionary<string, object?> d, string key)
        {
            return d.TryGetValue(key, out object? v) && v is double x ? x : 0;
        }

        private static List<string> Strings(List<object?> l)
        {
            var r = new List<string>();
            foreach (object? o in l) if (o is string s) r.Add(s);
            return r;
        }

        private List<Achievements.UnlockedEntry> AwardAchievements(double pps)
        {
            RaceState S = State;
            Dictionary<string, object?> st = _save.Stats();
            int conesHit = 0;
            foreach (Cone c in S.Cones) if (c.Knocked) conesHit++;
            int tireTotal = 0;
            foreach (Collectible c in _t.Collectibles) if (c.Kind == "tire") tireTotal++;
            Dictionary<string, List<object?>> caps = _save.CapsMap();
            var ctx = new AchievementContext
            {
                Run = new RunStats
                {
                    Finished = true,
                    Pps = pps,
                    Stars = Economy.StarsForPps(pps),
                    NearMisses = S.RunNearMisses,
                    Crashes = S.RunCrashes,
                    ConesHit = conesHit,
                    ConesTotal = S.Cones.Count,
                    TimeAt8 = S.RunTimeAt8,
                    ComboUnbroken = S.ComboUnbroken,
                    TiresThisRun = S.RunTirePickups,
                    TireTotalOnTrack = tireTotal,
                    Hour = _hourOfDay(),
                },
                Wallet = _save.Wallet(),
                Owned = Strings(_save.Owned()),
                OwnedCars = Strings(_save.OwnedCars()),
                Cleared = st.TryGetValue("cleared", out object? cl) && cl is List<object?> cll ? Strings(cll) : new List<string>(),
                Records = Achievements.FlattenRecords(_save.Records()),
                Caps = caps,
                Lifetime = new AchievementContext.LifetimeStats { Runs = StatNum(st, "runs"), DriftSecs = StatNum(st, "driftSecs") },
                Content = _opts.Content,
            };
            Achievements.EvalResult res = Achievements.Evaluate(ctx, _save.AchUnlocked());
            foreach (Achievements.ProgressEntry p in res.Progress) _save.AchSetProgress(p.Id, p.Value);
            var output = new List<Achievements.UnlockedEntry>();
            foreach (Achievements.UnlockedEntry u in res.Unlocked)
            {
                if (_save.AchUnlock(u.Id))                 // idempotent — the reward is paid once
                {
                    if (u.Reward != 0) _save.AddTires(u.Reward, "Achievement: " + u.Name);
                    output.Add(u);
                }
            }
            return output;
        }

        // ─── frame() (lines 390–634) ─────────────────────────────────────────────
        /// <summary>One frame. steerTarget = resolveSteer(...) (-1 | 0 | 1); dt in seconds (clamped to 0.05).</summary>
        public List<RaceEvent> Step(int steerTarget, double dt)
        {
            _ev = new List<RaceEvent>();
            RaceState S = State;
            // Line 596: the finish calls stop() and cancels the rAF – no frame ever runs after it.
            if (S.RaceFinished) return _ev;
            CarState car = S.Car;
            if (dt > MaxDt) dt = MaxDt;

            if (S.StartCd > 0)
            {
                S.StartCd -= dt;
                int cd = (int)Math.Ceiling(Math.Max(0, S.StartCd));
                if (cd >= 1 && cd != S.CdBeep) { S.CdBeep = cd; Emit(RaceEvent.EventKind.Countdown).Number = cd; }   // 3-2-1 pips
                if (S.StartCd <= 0)
                {
                    S.GoT = 1.0;
                    Emit(RaceEvent.EventKind.Go);              // sfx.go()
                    Emit(RaceEvent.EventKind.GameplayStart);   // gameplayStart()
                    S.GameplayActive = true;
                }
                return _ev;
            }
            if (S.GoT > 0) S.GoT -= dt;

            StepResult r = CarPhysics.StepCar(car, S.Motion, steerTarget, _p, _k, dt);
            bool drifting = r.Drifting;
            double speed = r.Speed, vS = r.VS;
            UpdateCaps(dt, drifting);

            // Frame-normalised decay factor for knocked-cone motion below.
            double fAdj = dt * _k.PhysHz;

            double CR = _carWid * 0.55;
            double hx = Math.Cos(car.Angle), hy = Math.Sin(car.Angle), nose = _carLen * 0.3;
            var bodyPts = new[]
            {
                new Vec2(car.X + hx * nose, car.Y + hy * nose),
                new Vec2(car.X, car.Y),
                new Vec2(car.X - hx * nose, car.Y - hy * nose),
            };

            List<Cone> cones = S.Cones;
            for (int ci = 0; ci < cones.Count; ci++)
            {
                Cone c = cones[ci];
                foreach (Vec2 p in bodyPts) HitConeAt(c, ci, p.X, p.Y, CR);
                if (c.Knocked) Collision.StepKnockedCone(c, _t.Props, _t.ConeR, dt, fAdj);
            }

            double wallHit = Collision.ResolveWall(car, _table, CR, hx, hy, nose, bodyPts);
            if (wallHit > WallCrash)
            {
                Haptic("crash");
                Sfx("crash", 0.55 + Math.Min(0.45, (wallHit - 120) / 400));
                BurnCombo("WALL!");
                S.RunCrashes++;
            }
            double propHit = Collision.ResolveProps(car, _t.Props, CR, bodyPts);
            if (propHit > PropCrash)
            {
                Haptic("crash");
                Sfx("crash", 0.55 + Math.Min(0.45, (propHit - 100) / 400));
                BurnCombo("CRASH!");
                S.RunCrashes++;
            }

            if (S.CrashCd > 0) S.CrashCd -= dt;
            double slip = Math.Abs(vS);
            (double distTrk, int nearIdx) = TrackUtil.NearestCenter(car.X, car.Y, _t.Center, S.NearIdx);
            S.NearIdx = nearIdx;
            bool onTrack = distTrk < _t.TrackHalf + 90;

            if (S.ComboPoints >= 1 && distTrk > _t.TrackHalf + 260) BurnCombo("OFF TRACK!");
            if (S.NearMissCd > 0) S.NearMissCd -= dt;

            if (drifting && onTrack && S.CrashCd <= 0)
            {
                S.DriftTime += dt;
                S.RunDriftSecs += dt;
                double quality = Scoring.DriftQuality(slip, speed);
                if (TrackUtil.CircularAdvance(S.NearIdx, S.DriftZoneRef, _nCtr) >= ZoneAdv)
                {
                    S.DriftZoneRef = S.NearIdx; S.DriftZoneTimer = 0; S.DriftZoned = false;
                }
                else
                {
                    S.DriftZoneTimer += dt;
                    if (S.DriftZoneTimer >= ZoneStall && !S.DriftZoned)
                    {
                        S.DriftZoned = true;
                        S.DriftZoneRef = S.NearIdx; // anchor to current position so recovery needs only 8 forward indices
                    }
                }
                if (!S.DriftZoned)
                {
                    S.MultBuild += dt * Scoring.MultGainPerS * quality;
                    int sgn = Scoring.SlipSign(vS);
                    if (sgn != 0)
                    {
                        if (S.LastSlipSign != 0 && sgn != S.LastSlipSign)
                        {
                            S.Transitions++; S.MultBuild += Scoring.MultTransitionBonus; Flash("TRANSITION!", "#7fd4ff");
                        }
                        S.LastSlipSign = sgn;
                    }
                    if (S.NearMissCd <= 0 && Collision.NearMiss(car, cones, _t.Props, _table, _t.ConeR, CR, NmBand))
                    {
                        S.NearMisses++; S.RunNearMisses++; S.MultBuild += Scoring.MultNearmissBonus; S.NearMissCd = 0.6; Flash("NEAR MISS!", "#ffd36a");
                        Emit(RaceEvent.EventKind.NearMiss).Number = S.NearMisses;
                    }
                    S.Mult = Scoring.ComboMult(S.MultBuild);
                    if (S.Mult >= Scoring.MultMax) S.RunTimeAt8 += dt;   // time held at the ceiling (flow-1/2)
                    S.ComboPoints += Scoring.ComboGain(slip, speed, dt, S.Mult);
                }
                else
                {
                    Flash("NO PROGRESS!", "#ffa040");
                }
                S.DriftGrace = 0;
            }
            else
            {
                S.DriftGrace += dt;
                if (S.DriftGrace > 0.5 && S.ComboPoints >= 1)
                {
                    S.ComboUnbroken = false;   // an active combo streak ended mid-race
                    if (onTrack) { BankPoints(); ResetMult(); } else BurnCombo("OFF TRACK!");
                }
                // Safety net: a stop that built a multiplier but no bankable points still clears it.
                if (onTrack && S.DriftGrace > MultResetGrace && S.MultBuild > 0) ResetMult();
            }

            // (skid marks: render-only, not ported)

            if (!_zen && S.LapStarted) S.LapTime += dt;

            if (!_zen && S.NextCp == 0)
            {
                // ── Finish line: crossing by sign of projection ──
                double fDot = Collision.FinishDot(car, _c0, _finishCos, _finishSin);
                if (S.PrevFinishDot != null && Collision.CrossedFinish(S.PrevFinishDot.Value, fDot))
                {
                    S.LastLap = S.LapTime;
                    if (S.BestLap == null || S.LapTime < S.BestLap.Value) S.BestLap = S.LapTime;
                    S.LapNum++;
                    RaceEvent lap = Emit(RaceEvent.EventKind.LapDone);   // HUD lap counter (#lapNum)
                    lap.Number = S.LapNum;
                    lap.Value = S.LapTime;

                    if (S.TotalLaps > 0 && S.LapNum >= S.TotalLaps)
                    {
                        Finish();
                        return _ev;
                    }

                    S.LapScores.Add(new RaceState.LapScore { N = S.LapNum, Pts = (int)CoreMath.JsRound(S.Score - S.LapScoreStart), T = S.LapTime });
                    if (S.TotalLaps == 0 && S.LapScores.Count > 3) S.LapScores.RemoveAt(0);
                    S.LapScoreStart = S.Score;
                    Flash("LAP " + CoreMath.JsToFixed(S.LapTime, 2) + " s", "#9dff8f");   // S.lapTime.toFixed(2)
                    S.LapTime = 0; S.NextCp = 1;
                    // prevFinishDot stays null until the next approach (reset below)
                }
                else
                {
                    S.PrevFinishDot = fDot;
                }
            }
            else
            {
                // ── Intermediate checkpoints: circle CP_R ──
                int nx = Collision.AdvanceCheckpoint(S.NextCp, car.X, car.Y, _t.Checkpoints, _t.CpR);
                if (nx != S.NextCp)
                {
                    if (!_zen)
                    {
                        RaceEvent cp = Emit(RaceEvent.EventKind.Checkpoint);   // sfx.checkpoint()
                        cp.Index = S.NextCp;
                        cp.Number = nx;
                        Haptic("checkpoint");                                  // hapticCheckpoint()
                    }
                    S.NextCp = nx;
                    if (S.NextCp == 0) S.PrevFinishDot = null; // reset before the next approach to the finish
                }
            }

            if (S.FlashT > 0) S.FlashT -= dt;
            Drift(drifting, Math.Min(1, slip / 180), S.ComboPoints >= 1);
            return _ev;
        }

        // ─── Final lap → finish (lines 520–605) ──────────────────────────────────
        private void Finish()
        {
            RaceState S = State;
            // Final lap: bank combo first, then record the lap score (order is critical).
            BankCombo();
            S.LapScores.Add(new RaceState.LapScore { N = S.LapNum, Pts = (int)CoreMath.JsRound(S.Score - S.LapScoreStart), T = S.LapTime });
            S.LapTime = 0; S.NextCp = 1;

            int totalScore = (int)CoreMath.JsRound(S.Score);
            double totalTime = 0;
            foreach (RaceState.LapScore l in S.LapScores) totalTime = totalTime + l.T;
            double pps = Scoring.PointsPerSecond(totalScore, totalTime);
            string id = _t.Id ?? "";
            string baseName = id.Length > 0 && _opts.Content.Names.TryGetValue(id, out string? nm) ? nm : "Race";
            string trackName = baseName + (_reversed ? " (reversed)" : "");
            int tireTotal = 0;
            foreach (Collectible c in _t.Collectibles) if (c.Kind == "tire") tireTotal++;
            int firstClearBonus = 0, finishBonus = 0, cleanSweepBonus = 0, trophyBonus = 0, unbrokenBonus = 0;
            if (!_zen)
            {
                if (S.TiresEarned > 0)
                    _save.RecordTxn(S.TiresEarned, trackName + " — " + S.TiresEarned.ToString(CultureInfo.InvariantCulture) + " tire" + (S.TiresEarned != 1 ? "s" : ""));
                if (tireTotal > 0 && S.RunTirePickups >= tireTotal && _save.MarkTireSwept(S.Instance))
                {
                    cleanSweepBonus = tireTotal;
                    _save.AddTires(cleanSweepBonus, trackName + " — clean sweep bonus");
                }
                if (id.Length > 0 && _save.MarkCleared(S.Instance))    // first finish of this instance → bonus
                {
                    firstClearBonus = Economy.FirstClearBonus;
                    _save.AddTires(firstClearBonus, trackName + " — first clear");
                }
                finishBonus = Economy.FinishPayout(pps);
                _save.AddTires(finishBonus, trackName + " — finish bonus");
                if (Economy.IsOnePps(pps))
                {
                    trophyBonus = Economy.OnePpsBonus;
                    _save.AddTires(trophyBonus, trackName + " — Participation Trophy");
                    _save.MarkTrophy(S.Instance);
                }
                if (S.ComboUnbroken)
                {
                    unbrokenBonus = Economy.UnbrokenBonus;
                    _save.AddTires(unbrokenBonus, trackName + " — Perpetual Motion");
                    _save.MarkPerpetual(S.Instance);
                }
            }

            bool isNewRecord = false;
            if (id.Length > 0)
            {
                Dictionary<string, object?> rec = _save.Records();
                if (!(rec.TryGetValue(S.Instance, out object? so) && so is Dictionary<string, object?> slot))
                {
                    slot = new Dictionary<string, object?>();
                    rec[S.Instance] = slot;
                }
                if (!(slot.TryGetValue("timeattack", out object? tao) && tao is Dictionary<string, object?> ta))
                {
                    ta = new Dictionary<string, object?>();
                    slot["timeattack"] = ta;
                }
                double? prevBest = ta.TryGetValue("bestPPS", out object? bp) && bp is double bpd ? bpd : (double?)null;
                if (Scoring.IsNewPpsRecord(prevBest, pps))
                {
                    ta["bestPPS"] = pps;
                    ta["bestPPSTotal"] = (double)totalScore;
                    ta["bestPPSTime"] = totalTime;
                    isNewRecord = true;
                    _save.Save();
                }
            }

            // ── Achievements (Time Attack only) ──
            var unlockedNow = new List<Achievements.UnlockedEntry>();
            if (!_zen)
            {
                Dictionary<string, object?> st = _save.Stats();
                st["runs"] = StatNum(st, "runs") + 1;
                st["driftSecs"] = StatNum(st, "driftSecs") + S.RunDriftSecs;
                _save.Save();
                unlockedNow = AwardAchievements(pps);
            }

            S.RaceFinished = true;
            StopInternal();   // keeps the results (raceFinished) and emits the paired GameplayStop
            if (isNewRecord) { Sfx("record"); Emit(RaceEvent.EventKind.HappyMoment); } else Sfx("finish");
            if (unlockedNow.Count > 0) Sfx("achieve", 650);   // setTimeout(() => sfx.achieve(), 650)
            var result = new RaceResult
            {
                Score = totalScore,
                BestLap = S.BestLap,
                LapScores = S.LapScores,
                IsNewRecord = isNewRecord,
                Pps = pps,
                TotalTime = totalTime,
                Ddk = Economy.IsDdk(pps),
                Unlocked = unlockedNow,
                CarModel = S.CarModel,
                Look = _save.CarLook(S.CarModel),
                TrackName = baseName,
                Reversed = _reversed,
                Tires = new RaceResult.TireBreakdown
                {
                    Pickup = S.TiresEarned, Cap = S.RunCaps * CapTireValue, CleanSweep = cleanSweepBonus,
                    FirstClear = firstClearBonus, Finish = finishBonus, Trophy = trophyBonus, Unbroken = unbrokenBonus,
                },
            };
            Emit(RaceEvent.EventKind.Finished).Result = result;   // raceResults.show({...})
        }

        // ─── stop() (lines 639–654) minus DOM ────────────────────────────────────
        /// <summary>Teardown: hushes the drift sound and emits the paired GameplayStop once per GameplayStart.</summary>
        public List<RaceEvent> Stop()
        {
            _ev = new List<RaceEvent>();
            StopInternal();
            return _ev;
        }

        private void StopInternal()
        {
            RaceState S = State;
            Drift(false, 0, false);   // stopDrift()
            if (S.GameplayActive) { Emit(RaceEvent.EventKind.GameplayStop); S.GameplayActive = false; }
        }
    }
}
