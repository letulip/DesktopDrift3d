#nullable enable
using System;
using System.IO;
using DesktopDrift.Core;
using DesktopDrift.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

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
            cam.backgroundColor = new Color32(0x10, 0x11, 0x14, 255);
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
                ("tracksJson", Json("tracks.json")), ("shopJson", Json("shop.json")));

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

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("SaveScene failed: " + ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("GameSceneBuilder: built " + ScenePath);
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
