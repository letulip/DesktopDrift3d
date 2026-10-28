#if UNITY_EDITOR
#nullable enable
using System.Collections.Generic;
using System.IO;
using DesktopDrift.Core;
using UnityEngine;

namespace DesktopDrift.Game
{
    /// <summary>
    /// Editor-only dev hook (DD_STEER=trace): replays the steer column of tools/engine-trace-oval.json by
    /// RaceLoop.StepCount, so a capture shows the car driving the lap. 0 after the trace ends.
    /// </summary>
    public sealed class TraceSteerSource : ISteerSource
    {
        const string OvalTrace = "tools/engine-trace-oval.json";

        readonly RaceLoop _loop;
        readonly List<object?> _frames;

        public TraceSteerSource(RaceLoop loop)
        {
            _loop = loop;
            var root = (Dictionary<string, object?>)Json.Parse(File.ReadAllText(Path.Combine(Root(), OvalTrace)))!;
            _frames = (List<object?>)root["frames"]!;
        }

        public int Resolve() => _loop.StepCount < _frames.Count ? (int)(double)((List<object?>)_frames[_loop.StepCount]!)[0]! : 0;

        // Same walk-up as Game.Tests TraceFiles (not referenced: the game must not depend on the test assembly).
        static string Root()
        {
            string? dir = Application.dataPath;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, OvalTrace))) return dir;
                dir = Path.GetDirectoryName(dir);
            }
            throw new FileNotFoundException(OvalTrace + " not found above " + Application.dataPath);
        }
    }
}
#endif
