#nullable enable
using System;
using System.Collections.Generic;
using DesktopDrift.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DesktopDrift.Game
{
    /// <summary>
    /// SPEC-02 step 11: HUD stub over Hud.uxml (names and strings from the 2D game), pause / exit confirm / results
    /// overlays and a launch stand-in for the menu (SPEC-06 replaces it). Pause is RaceLoop.Paused, never timeScale.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField] UIDocument doc = null!;
        [SerializeField] RaceLoop loop = null!;
        [SerializeField] SteerReader input = null!;

        VisualElement? hudRoot;
        MiniMap? mini;
        Label lap = null!, last = null!, best = null!, score = null!, lapScores = null!, spd = null!, spdUnit = null!,
            wallet = null!, lapNum = null!, lapTotal = null!, combo = null!, flash = null!, count = null!;
        Button pauseBtn = null!;
        VisualElement hudLeft = null!, hudRight = null!, hint = null!, pauseOverlay = null!, confirmOverlay = null!,
            resultsOverlay = null!, launchOverlay = null!;
        bool wasPaused;
        float hintTimer;
        RaceResult? pendingResult;

        sealed class MiniMap : VisualElement
        {
            public RaceLoop? Loop;
            const float Pad = 6f;
            public MiniMap() { generateVisualContent += Draw; }

            void Draw(MeshGenerationContext mgc)
            {
                if (Loop == null || Loop.Rules == null) return;
                var track = Loop.Track;
                float w = contentRect.width, h = contentRect.height;
                if (track == null || track.Center.Count < 2 || w <= 0 || h <= 0) return;
                double ex = track.Table.W / 2, ey = track.Table.H / 2;
                float ms = (float)Math.Min((w - 2 * Pad) / (2 * ex), (h - 2 * Pad) / (2 * ey));
                Vector2 P(double x, double y) => new Vector2(w / 2 + (float)x * ms, h / 2 + (float)y * ms);
                var p = mgc.painter2D;
                p.lineWidth = 2f;
                p.strokeColor = new Color(1f, 1f, 1f, 0.7f);
                p.BeginPath();
                p.MoveTo(P(track.Center[0].X, track.Center[0].Y));
                for (int i = 1; i < track.Center.Count; i++) p.LineTo(P(track.Center[i].X, track.Center[i].Y));
                p.ClosePath();
                p.Stroke();
                var car = Loop.State.Car;
                p.fillColor = new Color(1f, 0.3f, 0.3f, 1f);
                p.BeginPath();
                p.Arc(P(car.X, car.Y), 3f, Angle.Degrees(0), Angle.Degrees(360));
                p.ClosePath();
                p.Fill();
            }
        }

        void OnEnable()
        {
            if (loop != null) { loop.OnEvent += OnRaceEvent; loop.OnReset += OnReset; }
        }

        void OnDisable()
        {
            if (loop != null) { loop.OnEvent -= OnRaceEvent; loop.OnReset -= OnReset; }
        }

        // UIDocument rebuilds its tree on its own schedule, so (re)bind lazily whenever our root went away.
        bool EnsureBound()
        {
            if (doc == null) return false;
            if (hudRoot != null && hudRoot.panel != null) return true;
            var root = doc.rootVisualElement;
            var hud = root?.Q("hud");
            if (hud == null) return false;
            hudRoot = hud;
            Bind(root!);
            return true;
        }

        void Bind(VisualElement root)
        {
            lap = root.Q<Label>("lap"); last = root.Q<Label>("last"); best = root.Q<Label>("best");
            score = root.Q<Label>("score"); lapScores = root.Q<Label>("lapScores"); spd = root.Q<Label>("spd");
            spdUnit = root.Q<Label>("spdUnit"); wallet = root.Q<Label>("wallet"); lapNum = root.Q<Label>("lapNum");
            lapTotal = root.Q<Label>("lapTotal"); combo = root.Q<Label>("combo"); flash = root.Q<Label>("flash");
            count = root.Q<Label>("count");
            hudLeft = root.Q("hud-left"); hudRight = root.Q("hud-right"); hint = root.Q("hint");
            pauseOverlay = root.Q("pauseOverlay"); confirmOverlay = root.Q("confirmExitOverlay");
            resultsOverlay = root.Q("raceResultsOverlay"); launchOverlay = root.Q("launchOverlay");
            // Touch must reach the steering input: ignore pointers everywhere except buttons and overlays.
            root.pickingMode = PickingMode.Ignore;
            root.Query<VisualElement>().ForEach(e =>
                e.pickingMode = e is Button || e.ClassListContains("overlay") ? PickingMode.Position : PickingMode.Ignore);
            var pb = root.Q<Button>("pauseBtn");
            pauseBtn = pb;

            mini = new MiniMap { Loop = loop, pickingMode = PickingMode.Ignore };
            mini.style.flexGrow = 1;
            var slot = root.Q("mini");
            slot.Add(mini);

            pb.clicked += TogglePause;
            root.Q<Button>("menuBtn").clicked += OpenConfirm;
            pauseOverlay.RegisterCallback<ClickEvent>(_ => loop.Paused = false);
            root.Q<Button>("confirmCancelBtn").clicked += () => { Hide(confirmOverlay); loop.Paused = wasPaused; };
            root.Q<Button>("confirmRestartBtn").clicked += () => { Hide(confirmOverlay); loop.Reset(); };
            root.Q<Button>("confirmExitBtn").clicked += ExitToMenu;
            root.Q<Button>("rr-restart").clicked += () => { Hide(resultsOverlay); loop.Reset(); };
            root.Q<Button>("rr-back").clicked += () => { Hide(resultsOverlay); ExitToMenu(); };

            BindLaunch(root.Q<Button>("launchTimeAttack"), RaceMode.TimeAttack);
            BindLaunch(root.Q<Button>("launchSandbox"), RaceMode.Sandbox);
            BindLaunch(root.Q<Button>("launchZen"), RaceMode.Zen);
        }

        void BindLaunch(Button b, RaceMode mode)
        {
            b.text = RaceLaunch.ModeTitle(mode);
            b.clicked += () => { Hide(launchOverlay); RaceLaunch.Mode = mode; loop.StartRace(); };
        }

        static void Show(VisualElement e) => e.style.display = DisplayStyle.Flex;
        static void Hide(VisualElement e) => e.style.display = DisplayStyle.None;
        static bool Shown(VisualElement e) => e.style.display == DisplayStyle.Flex;

        void TogglePause()
        {
            if (loop.Running) loop.Paused = !loop.Paused;
        }

        void OpenConfirm()
        {
            if (!loop.Running || Shown(confirmOverlay)) return;
            wasPaused = loop.Paused;
            loop.Paused = true;
            Show(confirmOverlay);
        }

        void ExitToMenu()
        {
            Hide(confirmOverlay);
            loop.StopRace();
            Show(launchOverlay);
        }

        void OnReset()
        {
            hintTimer = 5f;
            pendingResult = null;
            if (!EnsureBound()) return;
            Hide(pauseOverlay); Hide(confirmOverlay); Hide(resultsOverlay); Hide(launchOverlay);
        }

        void OnRaceEvent(RaceEvent e)
        {
            if (e.Kind != RaceEvent.EventKind.Finished || e.Result == null) return;
            if (!EnsureBound()) { pendingResult = e.Result; return; }
            FillResult(e.Result);
        }

        void Update()
        {
            if (!EnsureBound()) return;
            if (pendingResult != null) { FillResult(pendingResult); pendingResult = null; }
            if (input != null && input.Pause.WasPressedThisFrame()) TogglePause();
            if (input != null && input.Back.WasPressedThisFrame()) OpenConfirm();

            bool paused = loop.Paused;
            pauseBtn.text = paused ? "▶" : "⏸";
            bool overlayOnTop = Shown(confirmOverlay) || Shown(resultsOverlay) || Shown(launchOverlay);
            pauseOverlay.style.display = paused && !overlayOnTop ? DisplayStyle.Flex : DisplayStyle.None;

            if (loop.Rules == null) return;
            var s = loop.State;
            bool zen = s.Zen;
            hudLeft.style.display = zen ? DisplayStyle.None : DisplayStyle.Flex;
            hudRight.style.display = zen ? DisplayStyle.None : DisplayStyle.Flex;

            bool mph = loop.Save.Settings().TryGetValue("units", out var u) && (u as string) == "mph";
            double speed = Math.Sqrt(s.Car.Vx * s.Car.Vx + s.Car.Vy * s.Car.Vy);
            lap.text = HudFormat.LapTime(s.LapTime);
            last.text = HudFormat.LapOpt(s.LastLap);
            best.text = HudFormat.LapOpt(s.BestLap);
            score.text = HudFormat.Score(s.Score);
            lapScores.text = HudFormat.LapScores(s.LapScores);
            spd.text = HudFormat.Speed(speed, mph);
            spdUnit.text = mph ? "mph" : "km/h";
            wallet.text = loop.Save.Wallet().ToString(System.Globalization.CultureInfo.InvariantCulture);
            lapNum.text = (s.LapNum + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            lapTotal.text = s.TotalLaps > 0 ? s.TotalLaps.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-";

            if (s.ComboPoints > 0) { combo.style.opacity = 1; combo.text = HudFormat.Combo(s.ComboPoints, s.Mult); }
            else combo.style.opacity = 0;

            flash.text = s.FlashMsg;
            flash.style.opacity = (float)HudFormat.FlashOpacity(s.FlashT);
            flash.style.color = ParseColor(s.FlashColor);

            var c = HudFormat.Count(s.StartCd, s.GoT);
            count.text = c.Text;
            count.style.color = ParseColor(c.Color);
            count.style.opacity = (float)c.Opacity;

            // The 2D game hides the hint with CSS; stand-in: visible until GO! has ended plus 5 s of running time.
            if (s.StartCd > 0 || s.GoT > 0) hintTimer = 5f;
            else if (loop.Running && !loop.Paused) hintTimer -= Time.unscaledDeltaTime;
            hint.style.display = hintTimer > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            mini?.MarkDirtyRepaint();
        }

        static Color ParseColor(string hex) => ColorUtility.TryParseHtmlString(hex, out var col) ? col : Color.white;

        static Label L(string text, string cls)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            if (cls.Length > 0) l.AddToClassList(cls);
            return l;
        }

        void FillResult(RaceResult r)
        {
            var root = hudRoot!.panel.visualTree;
            var rrScore = root.Q("rr-score"); rrScore.Clear();
            rrScore.Add(L("Score", "rr-label"));
            rrScore.Add(L(HudFormat.ResultPps(r.Pps), "rr-pps"));
            if (r.IsNewRecord) rrScore.Add(L("NEW RECORD", "rr-badge"));
            var stars = new VisualElement { pickingMode = PickingMode.Ignore };
            stars.style.flexDirection = FlexDirection.Row;
            if (r.Ddk) stars.Add(L("👑", ""));
            int lit = HudFormat.Stars(r.Pps);
            for (int i = 0; i < 5; i++) stars.Add(L("★", i < lit ? "rr-star-lit" : "rr-star-dim"));
            rrScore.Add(stars);
            rrScore.Add(L(HudFormat.ResultTotal(r.Score, r.TotalTime), "rr-label"));

            var rrTires = root.Q("rr-tires"); rrTires.Clear();
            var tp = HudFormat.TireParts(r.Tires);
            if (tp.Title.Length > 0) { rrTires.Add(L(tp.Title, "")); rrTires.Add(L(tp.Parts, "rr-label")); }
            rrTires.style.display = tp.Title.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            var rrAch = root.Q("rr-achievements"); rrAch.Clear();
            if (r.Unlocked.Count > 0)
            {
                rrAch.Add(L("🏆 Unlocked", "rr-label"));
                foreach (var u in r.Unlocked) rrAch.Add(L(u.Icon + " " + u.Name + " +" + u.Reward, ""));
            }
            rrAch.style.display = r.Unlocked.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            var rrLaps = root.Q("rr-laps"); rrLaps.Clear();
            foreach (var l in r.LapScores)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("rr-lap");
                var cells = HudFormat.LapRow(l);
                row.Add(L(cells.Lap, "")); row.Add(L(cells.Time, "")); row.Add(L(cells.Pts, ""));
                if (r.BestLap != null && l.T > 0 && Math.Abs(l.T - r.BestLap.Value) < 1e-9) row.AddToClassList("rr-lap-best");
                rrLaps.Add(row);
            }
            root.Q<Label>("rr-best").text = HudFormat.BestLap(r.BestLap);
            Show(resultsOverlay);
        }
    }
}
