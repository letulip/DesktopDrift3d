#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    // 2D render.js: TH = T.theme ? { ...THEME_DEFAULT, ...T.theme } : THEME_DEFAULT.
    [TestFixture]
    public class TrackThemeTests
    {
        private static TrackTheme Default() =>
            TrackTheme.ParseDefault(File.ReadAllText(TestPaths.Locate("unity/Assets/Content/_json/render.json")));

        private static void AssertSame(TrackTheme e, TrackTheme a, string what)
        {
            Assert.AreEqual(e.Background, a.Background, what + " background");
            Assert.AreEqual(e.Table, a.Table, what + " table");
            Assert.AreEqual(e.TableEdge, a.TableEdge, what + " tableEdge");
            Assert.AreEqual(e.Track, a.Track, what + " track");
            Assert.AreEqual(e.Skid, a.Skid, what + " skid");
            Assert.AreEqual(e.Checkpoint, a.Checkpoint, what + " checkpoint");
            Assert.AreEqual(e.Cone, a.Cone, what + " cone");
        }

        [Test]
        public void DefaultIsThe2DThemeDefault()
        {
            var d = Default();
            Assert.AreEqual("#0f0b08", d.Background);
            Assert.AreEqual("#2e241a", d.Table);
            Assert.AreEqual("#5a4a36", d.TableEdge);
            Assert.AreEqual("#43372a", d.Track);
            Assert.AreEqual("rgba(15,9,6,1)", d.Skid);
            Assert.AreEqual("rgba(125,212,255,0.5)", d.Checkpoint);
            Assert.AreEqual("#ff7a1a", d.Cone);
        }

        [Test]
        public void OvalMergedWithDefaultEqualsDefault()
        {
            var d = Default();
            AssertSame(d, TrackTheme.Merge(OvalTrack.Build().Theme, d), "oval");
            AssertSame(d, TrackTheme.Merge(null, d), "null");
        }

        [Test]
        public void TrackThemeFromTracksJsonKeepsItsOwnValues()
        {
            var d = Default();
            var doc = (Dictionary<string, object?>)Json.Parse(File.ReadAllText(TestPaths.Locate("unity/Assets/Content/_json/tracks.json")))!;
            var tracks = ((List<object?>)doc["TRACKS"]!).Select(o => (Dictionary<string, object?>)o!).ToList();
            Assert.IsNotEmpty(tracks);
            foreach (var t in tracks)
            {
                var th = (Dictionary<string, object?>)t["theme"]!;
                var own = new TrackTheme
                {
                    Background = (string)th["background"]!, Table = (string)th["table"]!, TableEdge = (string)th["tableEdge"]!,
                    Track = (string)th["track"]!, Skid = (string)th["skid"]!, Checkpoint = (string)th["checkpoint"]!, Cone = (string)th["cone"]!,
                };
                AssertSame(own, TrackTheme.Merge(own, d), (string)t["id"]!);
            }
        }

        [Test]
        public void PartialThemeFillsOnlyItsEmptyKeys()
        {
            var d = Default();
            var partial = new TrackTheme { Table = "#112233", Cone = "#445566" };
            var m = TrackTheme.Merge(partial, d);
            Assert.AreEqual("#112233", m.Table);
            Assert.AreEqual("#445566", m.Cone);
            Assert.AreEqual(d.Background, m.Background);
            Assert.AreEqual(d.TableEdge, m.TableEdge);
            Assert.AreEqual(d.Track, m.Track);
            Assert.AreEqual(d.Skid, m.Skid);
            Assert.AreEqual(d.Checkpoint, m.Checkpoint);
            Assert.AreEqual("", partial.Background, "the input is not mutated");
        }
    }
}
