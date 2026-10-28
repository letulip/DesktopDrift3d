#nullable enable
using System.Collections.Generic;
using System.IO;
using DesktopDrift.Core;
using UnityEngine;

namespace DesktopDrift.Game.Tests
{
    /// <summary>Repo files for PlayMode tests: walks up from Application.dataPath to the folder holding the oval trace.</summary>
    internal static class TraceFiles
    {
        public const string OvalTrace = "tools/engine-trace-oval.json";

        public static string Root()
        {
            string? dir = Application.dataPath;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, OvalTrace))) return dir;
                dir = Path.GetDirectoryName(dir);
            }
            throw new FileNotFoundException(OvalTrace + " not found above " + Application.dataPath);
        }

        public static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative));

        public static string Content(string file) => Read("unity/Assets/Content/_json/" + file);

        public static Dictionary<string, object?> Trace() => (Dictionary<string, object?>)Json.Parse(Read(OvalTrace))!;
    }
}
