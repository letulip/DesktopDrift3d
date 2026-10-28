using System;
using DesktopDrift.Core;
using UnityEngine;

namespace DesktopDrift.Game
{
    /// <summary>On-device camera tuning snapshot: written by the dev overlay's Save, read at startup.</summary>
    [Serializable]
    public sealed class TuningFile
    {
        public string mode = "C2";
        public CameraParams c2 = new CameraParams();

        public static string ToJson(TuningFile f) => JsonUtility.ToJson(f, true);
        public static TuningFile FromJson(string json) => JsonUtility.FromJson<TuningFile>(json);
        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, "camera-tuning.json");
    }
}
