#if UNITY_EDITOR || DEVELOPMENT_BUILD || DD_TUNING
using System.IO;
using System.Reflection;
using DesktopDrift.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DesktopDrift.Game
{
    /// <summary>Dev-only camera tuning panel (SPEC-02 step 7). The root ignores pointers so touch reaches the steering input.</summary>
    public sealed class TuningOverlay : MonoBehaviour
    {
        [SerializeField] UIDocument doc;
        [SerializeField] CameraRig rig;
        [SerializeField] RaceLoop loop;
        [SerializeField] CameraPreset preset;

        Label fpsLabel, copiedLabel;
        VisualElement panel;
        float fpsAcc, fpsTime; int fpsFrames;
        float copiedUntil;

        void Awake()
        {
            if (preset == null) return;
            try
            {
                if (!File.Exists(TuningFile.Path)) return;
                var f = TuningFile.FromJson(File.ReadAllText(TuningFile.Path));
                if (f == null) return;
                foreach (var fi in typeof(CameraParams).GetFields(BindingFlags.Public | BindingFlags.Instance))
                    if (fi.FieldType == typeof(double)) fi.SetValue(preset.c2, fi.GetValue(f.c2));
                _pendingMode = f.mode;
            }
            catch (System.Exception e) { Debug.LogWarning("TuningOverlay: bad tuning file: " + e.Message); }
        }

        string _pendingMode;

        void Start()
        {
            // DD_CAMERA_MODE wins over the saved mode (see CameraRig.Awake).
            if (rig != null && string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("DD_CAMERA_MODE"))
                && System.Enum.TryParse<CameraRig.CameraMode>(_pendingMode, out var m))
                rig.Mode = m;
        }

        void OnEnable()
        {
            if (doc == null) return;
            var root = doc.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;

            fpsLabel = new Label("-- fps") { pickingMode = PickingMode.Ignore };
            fpsLabel.style.position = Position.Absolute;
            fpsLabel.style.left = 12; fpsLabel.style.top = 12;
            fpsLabel.style.fontSize = 22; fpsLabel.style.color = Color.white;
            root.Add(fpsLabel);

            var tune = new Button(() => panel.style.display = DisplayStyle.Flex) { text = "Tune" };
            tune.style.position = Position.Absolute;
            tune.style.right = 12; tune.style.top = 12;
            tune.style.fontSize = 24;
            root.Add(tune);

            panel = new VisualElement { pickingMode = PickingMode.Position };
            panel.style.position = Position.Absolute;
            panel.style.left = 40; panel.style.right = 40; panel.style.top = 80; panel.style.bottom = 80;
            panel.style.backgroundColor = new Color(0f, 0f, 0f, 0.8f);
            panel.style.paddingLeft = panel.style.paddingRight = panel.style.paddingTop = panel.style.paddingBottom = 16;
            panel.style.display = DisplayStyle.None;
            root.Add(panel);

            var scroll = new ScrollView { pickingMode = PickingMode.Position };
            scroll.style.flexGrow = 1;
            panel.Add(scroll);

            if (preset != null)
            {
                foreach (var fi in typeof(CameraParams).GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (fi.FieldType != typeof(double)) continue;
                    AddSlider(scroll, fi);
                }
            }

            scroll.Add(Row(
                Btn("C1", () => { if (rig != null) rig.Mode = CameraRig.CameraMode.C1; }),
                Btn("C2", () => { if (rig != null) rig.Mode = CameraRig.CameraMode.C2; })));
            var modes = new VisualElement();
            modes.style.flexDirection = FlexDirection.Row;
            modes.style.flexWrap = Wrap.Wrap;
            foreach (RaceMode rm in System.Enum.GetValues(typeof(RaceMode)))
            {
                var captured = rm;
                modes.Add(Btn(RaceLaunch.ModeTitle(captured), () => { RaceLaunch.Mode = captured; if (loop != null) loop.Reset(); }));
            }
            scroll.Add(modes);

            copiedLabel = new Label("") { pickingMode = PickingMode.Ignore };
            copiedLabel.style.color = Color.white; copiedLabel.style.fontSize = 22;
            scroll.Add(Row(
                Btn("Restart", () => { if (loop != null) loop.Reset(); }),
                Btn("Save", Save),
                Btn("Export", Export),
                Btn("Close", () => panel.style.display = DisplayStyle.None),
                copiedLabel));
        }

        void AddSlider(VisualElement parent, FieldInfo fi)
        {
            var (lo, hi) = Range(fi);
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.color = Color.white; label.style.fontSize = 22;
            var slider = new Slider(lo, hi) { value = (float)(double)fi.GetValue(preset.c2) };
            void Refresh(float v) => label.text = fi.Name + ": " + v.ToString("0.###");
            Refresh(slider.value);
            slider.RegisterValueChangedCallback(e => { fi.SetValue(preset.c2, (double)e.newValue); Refresh(e.newValue); });
            parent.Add(label);
            parent.Add(slider);
        }

        static (float, float) Range(FieldInfo fi)
        {
            switch (fi.Name)
            {
                case "Distance": return (200f, 1500f);
                case "Height": return (50f, 1500f);
                case "LookAhead": return (0f, 400f);
                case "YawLag": return (0.05f, 1.5f);
                case "MaxYawRateDeg": return (30f, 720f);
                case "MinSpeedForVelocityYaw": return (0f, 300f);
                default:
                    var d = (float)(double)fi.GetValue(new CameraParams());
                    return (0f, d > 0f ? 2f * d : 1f);
            }
        }

        static Button Btn(string text, System.Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.fontSize = 24; b.style.marginRight = 8; b.style.marginTop = 8;
            return b;
        }

        static VisualElement Row(params VisualElement[] children)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.style.flexWrap = Wrap.Wrap;
            foreach (var c in children) r.Add(c);
            return r;
        }

        string CurrentJson() => TuningFile.ToJson(new TuningFile
        {
            mode = rig != null ? rig.Mode.ToString() : "C2",
            c2 = preset != null ? preset.c2 : new CameraParams(),
        });

        void Save()
        {
            File.WriteAllText(TuningFile.Path, CurrentJson());
            Flash("saved");
        }

        void Export()
        {
            GUIUtility.systemCopyBuffer = CurrentJson();
            Flash("copied");
        }

        void Flash(string text)
        {
            copiedLabel.text = text;
            copiedUntil = Time.unscaledTime + 2f;
        }

        void Update()
        {
            if (fpsLabel == null) return;
            fpsAcc += 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            fpsFrames++;
            fpsTime += Time.unscaledDeltaTime;
            if (fpsTime >= 0.5f)
            {
                fpsLabel.text = Mathf.RoundToInt(fpsAcc / fpsFrames) + " fps";
                fpsAcc = 0f; fpsFrames = 0; fpsTime = 0f;
            }
            if (copiedLabel != null && copiedLabel.text.Length > 0 && Time.unscaledTime > copiedUntil) copiedLabel.text = "";
        }
    }
}
#endif
