using System.Reflection;
using DesktopDrift.Core;
using DesktopDrift.Game;
using NUnit.Framework;

namespace DesktopDrift.Game.Tests
{
    public sealed class TuningFileTests
    {
        [Test]
        public void RoundTripKeepsModeAndEveryDoubleField()
        {
            var f = new TuningFile { mode = "C1" };
            var fields = typeof(CameraParams).GetFields(BindingFlags.Public | BindingFlags.Instance);
            int n = 0;
            foreach (var fi in fields)
            {
                if (fi.FieldType != typeof(double)) continue;
                fi.SetValue(f.c2, (double)fi.GetValue(f.c2) + 1.0);
                n++;
            }
            Assert.Greater(n, 0);
            var back = TuningFile.FromJson(TuningFile.ToJson(f));
            Assert.AreEqual("C1", back.mode);
            foreach (var fi in fields)
                if (fi.FieldType == typeof(double))
                    Assert.AreEqual((double)fi.GetValue(f.c2), (double)fi.GetValue(back.c2), 1e-9, fi.Name);
        }

        [Test]
        public void PathEndsWithFileName() => StringAssert.EndsWith("camera-tuning.json", TuningFile.Path);
    }
}
