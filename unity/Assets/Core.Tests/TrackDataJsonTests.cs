#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    // Gate for TrackSources.BuildFromContent + TrackDataJson: a TrackData built from the 2D game's SVG survives
    // Serialize -> Deserialize with EXACT doubles (==, no tolerance), re-serializes byte for byte, and reversing the
    // round-tripped track equals reversing the original. The 2D repository rule is TrackBuilderParityTests' (Build):
    // missing dir => Fail, unless DD3D_SKIP_2D_PARITY=1 => Ignore.
    [TestFixture]
    public class TrackDataJsonTests
    {
        private static string R(double d) { return d.ToString("R", CultureInfo.InvariantCulture); }

        private static void Eq(string id, string field, int i, double a, double b)
        {
            Assert.That(a == b, id + " " + field + "[" + i + "]: original=" + R(a) + " roundtrip=" + R(b));
        }

        private static void EqS(string id, string field, int i, string a, string b)
        {
            Assert.That(a == b, id + " " + field + "[" + i + "]: original='" + a + "' roundtrip='" + b + "'");
        }

        private static void EqB(string id, string field, int i, bool a, bool b)
        {
            Assert.That(a == b, id + " " + field + "[" + i + "]: original=" + a + " roundtrip=" + b);
        }

        private static void Count(string id, string field, int a, int b)
        {
            Assert.That(a == b, id + " " + field + ": count original=" + a + " roundtrip=" + b);
        }

        private static void Pts(string id, string field, List<Vec2> a, List<Vec2> b)
        {
            Count(id, field, a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Eq(id, field + ".x", i, a[i].X, b[i].X);
                Eq(id, field + ".y", i, a[i].Y, b[i].Y);
            }
        }

        private static void AssertSame(string id, TrackData a, TrackData b)
        {
            EqS(id, "id", 0, a.Id, b.Id);
            Eq(id, "laps", 0, a.Laps, b.Laps);
            EqB(id, "reversed", 0, a.Reversed, b.Reversed);
            Eq(id, "trackHalf", 0, a.TrackHalf, b.TrackHalf);
            Eq(id, "coneR", 0, a.ConeR, b.ConeR);
            Eq(id, "k", 0, a.K, b.K);
            Eq(id, "cpR", 0, a.CpR, b.CpR);
            Eq(id, "table.w", 0, a.Table.W, b.Table.W);
            Eq(id, "table.h", 0, a.Table.H, b.Table.H);
            EqB(id, "table.round", 0, a.Table.Round, b.Table.Round);
            EqS(id, "theme.background", 0, a.Theme.Background, b.Theme.Background);
            EqS(id, "theme.table", 0, a.Theme.Table, b.Theme.Table);
            EqS(id, "theme.tableEdge", 0, a.Theme.TableEdge, b.Theme.TableEdge);
            EqS(id, "theme.track", 0, a.Theme.Track, b.Theme.Track);
            EqS(id, "theme.skid", 0, a.Theme.Skid, b.Theme.Skid);
            EqS(id, "theme.checkpoint", 0, a.Theme.Checkpoint, b.Theme.Checkpoint);
            EqS(id, "theme.cone", 0, a.Theme.Cone, b.Theme.Cone);
            Eq(id, "startPos.x", 0, a.StartPos.X, b.StartPos.X);
            Eq(id, "startPos.y", 0, a.StartPos.Y, b.StartPos.Y);
            Eq(id, "startAngle", 0, a.StartAngle, b.StartAngle);
            Pts(id, "center", a.Center, b.Center);
            Pts(id, "outer", a.Outer, b.Outer);
            Pts(id, "inner", a.Inner, b.Inner);
            Pts(id, "checkpoints", a.Checkpoints, b.Checkpoints);

            Count(id, "cones", a.Cones.Count, b.Cones.Count);
            for (int i = 0; i < a.Cones.Count; i++)
            {
                Cone p = a.Cones[i], q = b.Cones[i];
                Eq(id, "cones.x", i, p.X, q.X); Eq(id, "cones.y", i, p.Y, q.Y);
                Eq(id, "cones.x0", i, p.X0, q.X0); Eq(id, "cones.y0", i, p.Y0, q.Y0);
                Eq(id, "cones.vx", i, p.Vx, q.Vx); Eq(id, "cones.vy", i, p.Vy, q.Vy);
                Eq(id, "cones.ang", i, p.Ang, q.Ang); Eq(id, "cones.spin", i, p.Spin, q.Spin);
                EqB(id, "cones.knocked", i, p.Knocked, q.Knocked); Eq(id, "cones.ci", i, p.Ci, q.Ci);
            }

            Count(id, "props", a.Props.Count, b.Props.Count);
            for (int i = 0; i < a.Props.Count; i++)
            {
                Prop p = a.Props[i], q = b.Props[i];
                Eq(id, "props.x", i, p.X, q.X); Eq(id, "props.y", i, p.Y, q.Y);
                Eq(id, "props.ang", i, p.Ang, q.Ang); Eq(id, "props.hl", i, p.Hl, q.Hl); Eq(id, "props.r", i, p.R, q.R);
                EqS(id, "props.kind", i, p.Kind, q.Kind);
                Eq(id, "props.cos", i, p.Cos, q.Cos); Eq(id, "props.sin", i, p.Sin, q.Sin);
            }

            Count(id, "collectibles", a.Collectibles.Count, b.Collectibles.Count);
            for (int i = 0; i < a.Collectibles.Count; i++)
            {
                Collectible p = a.Collectibles[i], q = b.Collectibles[i];
                EqS(id, "collectibles.kind", i, p.Kind, q.Kind);
                Eq(id, "collectibles.x", i, p.X, q.X); Eq(id, "collectibles.y", i, p.Y, q.Y);
                Eq(id, "collectibles.r", i, p.R, q.R); Eq(id, "collectibles.value", i, p.Value, q.Value);
                EqS(id, "collectibles.capId", i, p.CapId, q.CapId); Eq(id, "collectibles.hl", i, p.Hl, q.Hl);
            }
        }

        [TestCase("green-study")]
        [TestCase("steel-kitchen")]
        [TestCase("workbench")]
        [TestCase("cafe-marble")]
        [TestCase("dev-desk")]
        [TestCase("dining-oak")]
        public void RoundTripIsExact(string id)
        {
            TrackData t = TrackBuilderParityTests.Build(id);
            string s = TrackDataJson.Serialize(t);
            TrackData t2 = TrackDataJson.Deserialize(s);
            AssertSame(id, t, t2);
            string s2 = TrackDataJson.Serialize(t2);
            Assert.That(s2 == s, id + ": Serialize(Deserialize(s)) differs from s (lengths " + s.Length + " vs " + s2.Length + ")");
            TestContext.Out.WriteLine(id + " json bytes=" + s.Length);

            string r1 = TrackDataJson.Serialize(TrackUtil.ReverseTrack(t));
            string r2 = TrackDataJson.Serialize(TrackUtil.ReverseTrack(t2));
            Assert.That(r2 == r1, id + ": ReverseTrack(roundtrip) serializes differently from ReverseTrack(original)");
        }

        [Test]
        public void EveryTrackHasAllSevenThemeKeys()
        {
            var doc = (Dictionary<string, object?>)Json.Parse(File.ReadAllText(TestPaths.Locate("unity/Assets/Content/_json/tracks.json")))!;
            var rows = (List<object?>)doc["TRACKS"]!;
            Assert.That(rows.Count, Is.GreaterThan(0));
            string[] keys = { "background", "table", "tableEdge", "track", "skid", "checkpoint", "cone" };
            foreach (object? r in rows)
            {
                var row = (Dictionary<string, object?>)r!;
                var th = (Dictionary<string, object?>)row["theme"]!;
                foreach (string k in keys)
                {
                    Assert.That(th.TryGetValue(k, out object? v) && v is string str && str.Length > 0,
                        row["id"] + ": theme." + k + " missing or empty");
                }
            }
        }
    }
}
