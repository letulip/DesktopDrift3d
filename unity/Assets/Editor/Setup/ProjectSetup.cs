// SPEC-00 B2: idempotent project configuration, run headless via `unity run unity -- -executeMethod DesktopDrift.Editor.ProjectSetup.Apply`
#nullable enable
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace DesktopDrift.Editor
{
    public static class ProjectSetup
    {
        const string ProductName = "Desktop Drift";                 // placeholder until SPEC-09 (owner decision)
        const string CompanyName = "letulip";                       // placeholder until SPEC-09 (owner decision)
        const string AndroidPackageId = "com.letulip.desktopdrift"; // placeholder until SPEC-09 (owner decision)
        const string ExpectedEditorVersion = "6000.3.27f1";

        [MenuItem("DesktopDrift/Project Setup/Apply")]
        public static void Apply()
        {
            SetString("productName", PlayerSettings.productName, ProductName, v => PlayerSettings.productName = v);
            SetString("companyName", PlayerSettings.companyName, CompanyName, v => PlayerSettings.companyName = v);
            SetString("applicationIdentifier(Android)", PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                AndroidPackageId, v => PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, v));

            Set("scriptingBackend(Android)", PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android),
                ScriptingImplementation.IL2CPP, v => PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, v));
            Set("android.targetArchitectures", PlayerSettings.Android.targetArchitectures,
                AndroidArchitecture.ARM64, v => PlayerSettings.Android.targetArchitectures = v);
            Set("android.minSdkVersion", PlayerSettings.Android.minSdkVersion,
                AndroidSdkVersions.AndroidApiLevel26, v => PlayerSettings.Android.minSdkVersion = v);
            Set("android.targetSdkVersion", PlayerSettings.Android.targetSdkVersion,
                AndroidSdkVersions.AndroidApiLevelAuto, v => PlayerSettings.Android.targetSdkVersion = v);
            Debug.Log($"ProjectSetup: android.targetSdkVersion resolves to {(int)PlayerSettings.Android.targetSdkVersion} (0 = Auto = highest installed SDK)");

            // D-19: portrait is the working hypothesis (2D manifest.json: "orientation": "portrait"); the final call is the
            // SPEC-02 PoC gate, so one build must show both: autorotation to Portrait and both Landscapes, no upside-down.
            Set("defaultInterfaceOrientation", PlayerSettings.defaultInterfaceOrientation,
                UIOrientation.AutoRotation, v => PlayerSettings.defaultInterfaceOrientation = v);
            Set("allowedAutorotateToPortrait", PlayerSettings.allowedAutorotateToPortrait, true,
                v => PlayerSettings.allowedAutorotateToPortrait = v);
            Set("allowedAutorotateToPortraitUpsideDown", PlayerSettings.allowedAutorotateToPortraitUpsideDown, false,
                v => PlayerSettings.allowedAutorotateToPortraitUpsideDown = v);
            Set("allowedAutorotateToLandscapeLeft", PlayerSettings.allowedAutorotateToLandscapeLeft, true,
                v => PlayerSettings.allowedAutorotateToLandscapeLeft = v);
            Set("allowedAutorotateToLandscapeRight", PlayerSettings.allowedAutorotateToLandscapeRight, true,
                v => PlayerSettings.allowedAutorotateToLandscapeRight = v);

            Set("colorSpace", PlayerSettings.colorSpace, ColorSpace.Linear, v => PlayerSettings.colorSpace = v);

            var so = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var prop = so.FindProperty("activeInputHandler");
            if (prop.intValue != 1)
            {
                Debug.Log($"ProjectSetup: activeInputHandler {prop.intValue} -> 1");
                prop.intValue = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.Log("ProjectSetup: activeInputHandler ok (1)");
            }

            // Unity 6 stores "Fixed Timestep" as a rational: m_Count ticks of m_Rate (Hz) – 1/60 s = rate / 60 ticks.
            var tm = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("TimeManager"));
            var count = tm.FindProperty("Fixed Timestep.m_Count");
            var num = tm.FindProperty("Fixed Timestep.m_Rate.m_Numerator");
            var den = tm.FindProperty("Fixed Timestep.m_Rate.m_Denominator");
            var maxStep = tm.FindProperty("Maximum Allowed Timestep");
            long wantCount = num.longValue / (den.longValue * 60);
            if (count.longValue != wantCount || maxStep.floatValue != 0.05f)
            {
                Debug.Log($"ProjectSetup: TimeManager fixed {count.longValue}/{num.longValue} -> {wantCount}/{num.longValue}, max {maxStep.floatValue} -> 0.05");
                count.longValue = wantCount;
                maxStep.floatValue = 0.05f;
                tm.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.Log($"ProjectSetup: TimeManager ok (fixed {count.longValue}/{num.longValue} = 1/60, max 0.05)");
            }

            // matched by type name: the Editor asmdef does not reference the URP runtime assembly
            Check(GraphicsSettings.defaultRenderPipeline != null && GraphicsSettings.defaultRenderPipeline.GetType().FullName == "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset",
                "GraphicsSettings.defaultRenderPipeline is not a UniversalRenderPipelineAsset");
            for (int i = 0; i < QualitySettings.count; i++)
                Check(QualitySettings.GetRenderPipelineAssetAt(i) != null, $"quality level {i} ({QualitySettings.names[i]}) has no render pipeline asset");
            Debug.Log($"ProjectSetup: URP ok ({QualitySettings.count} quality levels)");

            var versionFile = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "ProjectSettings", "ProjectVersion.txt");
            Check(File.Exists(versionFile) && File.ReadAllText(versionFile).Contains("m_EditorVersion: " + ExpectedEditorVersion),
                "ProjectVersion.txt does not contain m_EditorVersion: " + ExpectedEditorVersion);
            Debug.Log($"ProjectSetup: ProjectVersion ok ({ExpectedEditorVersion})");

            // PlayerSettings.* setters alone did not flush ProjectSettings.asset in batchmode (2026-10-28): the file was only
            // written while activeInputHandler changed above. Mark the singleton dirty so SaveAssets always writes it.
            EditorUtility.SetDirty(so.targetObject);
            AssetDatabase.SaveAssets();
            Debug.Log("PROJECT_SETUP_OK");
        }

        static void SetString(string name, string current, string wanted, Action<string> write) => Set(name, current, wanted, write);

        static void Set<T>(string name, T current, T wanted, Action<T> write)
        {
            if (Equals(current, wanted))
            {
                Debug.Log($"ProjectSetup: {name} ok ({wanted})");
                return;
            }
            Debug.Log($"ProjectSetup: {name} {current} -> {wanted}");
            write(wanted);
        }

        static void Check(bool ok, string what)
        {
            if (ok) return;
            Debug.LogError("ProjectSetup: FAIL " + what);
            throw new InvalidOperationException("ProjectSetup: FAIL " + what);
        }
    }
}
