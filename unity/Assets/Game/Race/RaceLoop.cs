#nullable enable
using System;
using System.Collections.Generic;
using DesktopDrift.Core;
using UnityEngine;

namespace DesktopDrift.Game
{
    /// <summary>
    /// SPEC-02 step 3: thin race orchestrator. FixedUpdate -> steer -> RaceRules.Step(steer, 1/60) -> events out.
    /// No rules live here (03-architecture §12): it only drives RaceRules and dispatches its RaceEvents.
    /// </summary>
    public sealed class RaceLoop : MonoBehaviour
    {
        // Passed to Step verbatim: Time.fixedDeltaTime is a float (0.01666667f != 1/60) and would break parity.
        public const double FixedDt = 1.0 / 60.0;

        [SerializeField] TextAsset? carsJson, registryJson, configJson, tracksJson, shopJson;
        [SerializeField] TrackView? trackView;
        [SerializeField] TrackCatalog? trackCatalog;
        [SerializeField] CarView? carView;
        [SerializeField] SteerReader? steerReader;

        ISteerSource? _steerSource;
        public ISteerSource? SteerSource { get => _steerSource ?? steerReader; set => _steerSource = value; }
        public Func<double> Random { get; set; } = new System.Random().NextDouble;
        public Func<int> HourOfDay { get; set; } = () => DateTime.Now.Hour;
        public Func<double> NowMs { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>The ordered track assets (tracks.json order) for the menu / track-select screens.</summary>
        public TrackCatalog? TrackCatalog => trackCatalog;

        public TrackData Track = null!;
        public CarCatalog Catalog = null!;
        public PhysicsConstants Physics = null!;
        public ContentDescriptor Content = null!;
        public SaveState Save = null!;
        public RaceRules? Rules;
        public RaceState State => Rules!.State;
        public CarDefinition? Car;
        public RaceOptions? Options;
        public bool Running;
        public bool Paused { get; set; }
        public int StepCount;

        public event Action<RaceEvent>? OnEvent;
        public event Action? OnStepped;
        public event Action? OnReset;

        bool _initialized;
        bool _saveDirty;
        bool _trackReversed;

        public void Init(TrackData track, CarCatalog catalog, PhysicsConstants physics, ContentDescriptor content, SaveState save)
        {
            Track = track;
            _trackReversed = RaceLaunch.Mode != RaceMode.Sandbox && RaceLaunch.Reversed;
            Catalog = catalog;
            Physics = physics;
            Content = content;
            Save = save;
            trackView?.Build(track);
            // Persisting the save file is SPEC-07; until then a change is only logged.
            Save.OnChanged = () => _saveDirty = true;
            _initialized = true;
        }

        void Awake()
        {
            Time.fixedDeltaTime = (float)FixedDt;
            Time.maximumDeltaTime = 0.05f;
            Application.targetFrameRate = 60;
#if UNITY_EDITOR
            // Dev hooks for scripts/capture.sh (the editor inherits the env), read BEFORE the track is resolved.
            string? ddMode = Environment.GetEnvironmentVariable("DD_MODE");
            if (ddMode == "ta") RaceLaunch.Mode = RaceMode.TimeAttack;
            else if (ddMode == "zen") RaceLaunch.Mode = RaceMode.Zen;
            else if (ddMode == "sandbox") RaceLaunch.Mode = RaceMode.Sandbox;
            string? ddTrack = Environment.GetEnvironmentVariable("DD_TRACK");
            if (!string.IsNullOrEmpty(ddTrack)) RaceLaunch.TrackId = ddTrack;
            if (Environment.GetEnvironmentVariable("DD_REVERSED") == "1") RaceLaunch.Reversed = true;
#endif
            if (carsJson != null && !_initialized)
            {
                Init(RaceLaunch.ResolveTrack(trackCatalog),
                    CarCatalog.FromJson(carsJson.text, registryJson?.text),
                    CarCatalog.PhysicsFromConfig(configJson!.text),
                    ContentJson.Build(tracksJson!.text, shopJson!.text, carsJson.text),
                    SaveSchema.Load(null).State);
            }
#if UNITY_EDITOR
            // Scripted steering: trace = the oval trace's steer column, auto = the harness autopilot.
            string? ddSteer = Environment.GetEnvironmentVariable("DD_STEER");
            if (ddSteer == "trace") SteerSource = new TraceSteerSource(this);
            else if (ddSteer == "auto") SteerSource = new AutopilotSteerSource(this);
#endif
        }

        void Start()
        {
            if (_initialized && RaceLaunch.AutoStart) StartRace();
        }

        public void StartRace() => StartRace(RaceLaunch.BuildOptions(Content, Catalog.Cars.Count));

        public void StartRace(RaceOptions opts)
        {
            Options = opts;
            Save.NowMs = () => NowMs();
            Save.Garage()["carIndex"] = (double)RaceLaunch.CarIndex;
            // RaceRules clamps the same garage.carIndex into S.CarModel (§12 p.5), so both agree on the car.
            Car = Catalog.ByIndex(RaceLaunch.CarIndex);
            Rules = new RaceRules(Track, Car.Drive, Car.Len, Car.Wid, Physics, opts, Save, () => Random(), () => HourOfDay());
            Rules.Start();
            StepCount = 0;
            Running = true;
            Paused = false;
            carView?.Configure(Car.Len, Car.Wid);
            carView?.SetPose(State.Car.X, State.Car.Y, State.Car.Angle);
            OnReset?.Invoke();
        }

        /// <summary>
        /// Start a race from the current RaceLaunch selection (mode, track, direction): swaps the track (oval for Sandbox,
        /// a real track otherwise) when it differs from the loaded one, then starts. Views reset through OnReset.
        /// </summary>
        public void Relaunch()
        {
            if (Running) StopRace();
            TrackData next = RaceLaunch.ResolveTrack(trackCatalog);
            bool reversed = RaceLaunch.Mode != RaceMode.Sandbox && RaceLaunch.Reversed;
            if (Track == null || next.Id != Track.Id || reversed != _trackReversed)
                Init(next, Catalog, Physics, Content, Save);
            StartRace();
        }

        /// <summary>"Race again" / "Restart" without reloading the scene.</summary>
        // Also Unity's editor Reset message (AddComponent in edit mode): nothing to restart before Init.
        public void Reset()
        {
            if (!_initialized) return;
            if (Rules != null) Dispatch(Rules.Stop());
            StartRace(Options ?? RaceLaunch.BuildOptions(Content, Catalog.Cars.Count));
        }

        /// <summary>Leave the race without restarting (2D stop(true) on exit): pairs GameplayStop, ends the loop.</summary>
        public void StopRace()
        {
            if (Rules != null) Dispatch(Rules.Stop());
            Running = false;
            Paused = false;
        }

        public void StepOnce(int steer)
        {
            List<RaceEvent> ev = Rules!.Step(steer, FixedDt);
            StepCount++;
            Dispatch(ev);
            if (_saveDirty)
            {
                _saveDirty = false;
                Debug.Log("save: revision " + Save.Revision);
            }
            carView?.Push(State.Car.X, State.Car.Y, State.Car.Angle);
            OnStepped?.Invoke();
        }

        void Dispatch(List<RaceEvent> events)
        {
            foreach (RaceEvent e in events)
            {
                OnEvent?.Invoke(e);
                if (e.Kind == RaceEvent.EventKind.Finished) Running = false;
                switch (e.Kind)
                {
                    case RaceEvent.EventKind.Countdown:
                    case RaceEvent.EventKind.Go:
                    case RaceEvent.EventKind.LapDone:
                    case RaceEvent.EventKind.Finished:
                    case RaceEvent.EventKind.ComboBurn:
                    case RaceEvent.EventKind.Flash:
                        Debug.Log($"race: {e.Kind} step={StepCount} n={e.Number} text={e.Text} value={e.Value}");
                        break;
                }
            }
        }

        void FixedUpdate()
        {
            // §12 p.2: no Step while paused or after Finished.
            if (Rules == null || !Running || Paused) return;
            StepOnce(SteerSource?.Resolve() ?? 0);
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && Running && Rules != null && State.GameplayActive) Paused = true;
        }
    }
}
