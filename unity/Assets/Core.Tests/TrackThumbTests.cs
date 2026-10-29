// Gate: TrackThumb.Commands reproduces, command for command, what the 2D game's own drawThumb issues on a recording canvas
// (tools/thumb-ref/<id>.json from tools/export-thumbs.mjs). Needs the 2D checkout like TrackBuilderParityTests (missing => Fail,
// DD3D_SKIP_2D_PARITY=1 => Ignore). Also pins the UI rule: the thumbnail of a reversed track is NOT mirrored.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    [TestFixture]
    public class TrackThumbTests
    {
        private static Dictionary<string, object?> O(object? o) { return (Dictionary<string, object?>)o!; }
        private static List<object?> A(object? o) { return (List<object?>)o!; }

        private static TrackTheme ThemeOf(string id)
        {
            var doc = O(Json.Parse(File.ReadAllText(TestPaths.Locate("unity/Assets/Content/_json/tracks.json"))));
            foreach (object? t in A(doc["TRACKS"]))
            {
                var d = O(t);
                if ((string)d["id"]! != id) continue;
                var th = O(d["theme"]);
                string G(string k) { return th.ContainsKey(k) ? (string)th[k]! : ""; }
                return new TrackTheme { Background = G("background"), Table = G("table"), TableEdge = G("tableEdge"), Track = G("track") };
            }
            throw new InvalidOperationException("unknown track " + id);
        }

        private static void Near(string where, double expected, double actual)
        {
            double tol = 1e-9 * Math.Max(1.0, Math.Abs(expected));
            Assert.That(Math.Abs(expected - actual) <= tol,
                where + ": JS=" + expected.ToString("R", CultureInfo.InvariantCulture) + " C#=" + actual.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AssertSame(string id, List<ThumbCommand> cs, string refRelative)
        {
            var doc = O(Json.Parse(File.ReadAllText(TestPaths.Locate(refRelative))));
            Assert.AreEqual(260.0, (double)doc["width"]!);
            Assert.AreEqual(140.0, (double)doc["height"]!);
            var js = A(doc["commands"]);
            Assert.That(cs.Count, Is.EqualTo(js.Count), id + ": command count JS=" + js.Count + " C#=" + cs.Count);
            for (int i = 0; i < js.Count; i++)
            {
                var c = A(js[i]);
                string name = (string)c[0]!;
                string where = id + " cmd[" + i + "]";
                if (name == "set")
                {
                    string prop = (string)c[1]!;
                    Assert.AreEqual("set:" + prop, cs[i].Name, where);
                    if (c[2] is string str)
                    {
                        Assert.AreEqual(str, cs[i].Value, where + " " + prop);
                        Assert.AreEqual(0, cs[i].Args.Length, where);
                    }
                    else
                    {
                        Assert.IsNull(cs[i].Value, where);
                        Assert.AreEqual(1, cs[i].Args.Length, where);
                        Near(where + " " + prop, (double)c[2]!, cs[i].Args[0]);
                    }
                }
                else
                {
                    Assert.AreEqual(name, cs[i].Name, where);
                    Assert.IsNull(cs[i].Value, where);
                    Assert.AreEqual(c.Count - 1, cs[i].Args.Length, where + " arg count");
                    for (int a = 1; a < c.Count; a++) Near(where + " " + name + " arg" + (a - 1), (double)c[a]!, cs[i].Args[a - 1]);
                }
            }
        }

        [TestCase("green-study")]
        [TestCase("steel-kitchen")]
        [TestCase("workbench")]
        [TestCase("cafe-marble")]
        [TestCase("dev-desk")]
        [TestCase("dining-oak")]
        public void CommandsEqualTheRealDrawThumb(string id)
        {
            TrackData t = TrackBuilderParityTests.Build(id);
            var cmds = TrackThumb.Commands(t.Center, ThemeOf(id), 260, 140);
            AssertSame(id, cmds, "tools/thumb-ref/" + id + ".json");
        }

        [Test]
        public void ReversedTrackGetsTheSameForwardThumbnail()
        {
            TrackData fwd = TrackBuilderParityTests.Build("green-study");
            var a = TrackThumb.ForInstance(fwd, false);
            var b = TrackThumb.ForInstance(fwd, true);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Name, b[i].Name);
                Assert.AreEqual(a[i].Value, b[i].Value);
                CollectionAssert.AreEqual(a[i].Args, b[i].Args);
            }
            // and it is the 2D picture (JS ref), not the reversed track's: first lineTo must match the forward reference
            AssertSame("green-study", b, "tools/thumb-ref/green-study.json");
        }

        [Test]
        public void MissingThemeKeysUseTheThumbFallbacksNotThemeDefault()
        {
            var cmds = TrackThumb.Commands(new[] { new Vec2(0, 0), new Vec2(100, 0), new Vec2(100, 50) }, null, 260, 140);
            Assert.AreEqual("#0b0907", cmds[0].Value);
            Assert.AreEqual("#2e241a", cmds[4].Value);
            Assert.AreEqual("#5a4a36", cmds[6].Value);
            Assert.AreEqual("#ffb14d", cmds[10].Value);
        }
    }
}
