#nullable enable
using System;
using System.IO;
using DesktopDrift.Core;
using DesktopDrift.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DesktopDrift.Editor
{
    public static class GameSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Game.unity";
        const string MaterialsDir = "Assets/Art/Materials";

        [MenuItem("DesktopDrift/Scene/Build Game Scene")]
        public static void Build()
        {
            var table = Mat("Table", new Color32(0x2b, 0x2d, 0x31, 255));
            var ribbon = Mat("Ribbon", new Color32(0x6a, 0x6d, 0x73, 255));
            var cone = Mat("Cone", new Color32(0xff, 0x7a, 0x1a, 255));
            var prop = Mat("Prop", new Color32(0x9a, 0x8c, 0x7a, 255));
            var car = Mat("Car", new Color32(0xd8, 0x30, 0x30, 255));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.color = Color.white;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Camera behind the start line, looking along the heading at a point just ahead of the car.
            var t = OvalTrack.Build();
            Vector3 start = WorldMapping.ToWorld(t.StartPos);
            Vector3 fwd = WorldMapping.Yaw(t.StartAngle) * Vector3.forward;
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x14, 0x13, 0x0e, 255); // green-study theme background; TrackView sets the real one per track
            camGo.transform.position = start - fwd * 0.3f + Vector3.up * 0.5f;
            camGo.transform.LookAt(start + fwd * 0.15f);

            var track = new GameObject("Track").AddComponent<TrackView>();
            SetRefs(track, ("tableMaterial", table), ("ribbonMaterial", ribbon), ("coneMaterial", cone), ("propMaterial", prop));

            var carView = new GameObject("Car").AddComponent<CarView>();
            SetRefs(carView, ("material", car));

            var input = new GameObject("Input").AddComponent<SteerReader>();
            SetRefs(input, ("actions", AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/DesktopDrift.inputactions")));

            var race = new GameObject("Race").AddComponent<RaceLoop>();
            SetRefs(race, ("trackView", track), ("carView", carView), ("steerReader", input),
                ("carsJson", Json("cars.json")), ("registryJson", Json("car-registry.json")), ("configJson", Json("config.json")),
                ("tracksJson", Json("tracks.json")), ("shopJson", Json("shop.json")),
                ("trackCatalog", AssetDatabase.LoadAssetAtPath<TrackCatalog>("Assets/Content/Tracks/TrackCatalog.asset")));

            const string presetPath = "Assets/Content/Camera/Default.asset";
            var preset = AssetDatabase.LoadAssetAtPath<CameraPreset>(presetPath);
            if (preset == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Content/Camera")) AssetDatabase.CreateFolder("Assets/Content", "Camera");
                preset = ScriptableObject.CreateInstance<CameraPreset>();
                AssetDatabase.CreateAsset(preset, presetPath);
            }
            var rig = camGo.AddComponent<CameraRig>();
            SetRefs(rig, ("preset", preset), ("cam", cam), ("carView", carView), ("loop", race));

            var skids = new GameObject("SkidMarks").AddComponent<SkidMarks>();
            SetRefs(skids, ("loop", race), ("material", SkidMat()));

            var tires = new GameObject("Tires").AddComponent<Tires>();
            SetRefs(tires, ("loop", race), ("material", Mat("Tire", new Color32(0x2a, 0x2a, 0x2c, 255))));

            var caps = new GameObject("Caps").AddComponent<ColaCaps>();
            SetRefs(caps, ("loop", race), ("material", CapMat()));

            var cones = new GameObject("Cones").AddComponent<Cones>();
            SetRefs(cones, ("loop", race), ("trackView", track));

            var tuningGo = new GameObject("Tuning");
            var tuningDoc = tuningGo.AddComponent<UIDocument>();
            tuningDoc.panelSettings = PanelSettingsAsset();
            tuningDoc.sortingOrder = 100;
            var tuning = tuningGo.AddComponent<TuningOverlay>();
            SetRefs(tuning, ("doc", tuningDoc), ("rig", rig), ("loop", race), ("preset", preset));

            var hudGo = new GameObject("Hud");
            var hudDoc = hudGo.AddComponent<UIDocument>();
            hudDoc.panelSettings = PanelSettingsAsset();
            hudDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Hud.uxml")
                ?? throw new Exception("Missing Assets/UI/Hud.uxml");
            hudDoc.sortingOrder = 10; // below the tuning overlay (100)
            var hud = hudGo.AddComponent<HudController>();
            SetRefs(hud, ("doc", hudDoc), ("loop", race), ("input", input));

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("SaveScene failed: " + ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("GameSceneBuilder: built " + ScenePath);
        }

        // Shared with the HUD (step 11): portrait-first reference resolution (D-19).
        static PanelSettings PanelSettingsAsset()
        {
            const string path = "Assets/UI/PanelSettings.asset";
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (ps == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/UI")) AssetDatabase.CreateFolder("Assets", "UI");
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(ps, path);
            }
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1080, 1920);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI/UnityDefaultRuntimeTheme.tss")
                ?? AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
            if (theme != null) ps.themeStyleSheet = theme;
            EditorUtility.SetDirty(ps);
            return ps;
        }

        static TextAsset Json(string file)
        {
            string path = "Assets/Content/_json/" + file;
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path) ?? throw new Exception("Missing TextAsset " + path);
        }

        static Material Mat(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder(MaterialsDir)) AssetDatabase.CreateFolder("Assets/Art", "Materials");
            string path = $"{MaterialsDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material SkidMat()
        {
            string path = $"{MaterialsDir}/SkidMark.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("DesktopDrift/SkidMark") ?? throw new Exception("Missing shader DesktopDrift/SkidMark");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_Color", Color.white);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material CapMat() => ShaderMat("Cap", "DesktopDrift/CapRadialFill", m =>
        {
            m.SetColor("_BaseColor", new Color32(0xff, 0x99, 0x99, 153));
            m.SetColor("_FillColor", new Color32(0xff, 0x3b, 0x3b, 242));
            m.SetFloat("_Sweep", 0f);
        });

        static Material ShaderMat(string name, string shaderName, Action<Material> setup)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find(shaderName) ?? throw new Exception("Missing shader " + shaderName);
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            setup(m);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void SetRefs(UnityEngine.Object target, params (string field, UnityEngine.Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
            {
                var p = so.FindProperty(field) ?? throw new Exception("Missing serialized field " + field);
                p.objectReferenceValue = value;
            }
            so.ApplyModifiedProperties();
        }
    }
}
