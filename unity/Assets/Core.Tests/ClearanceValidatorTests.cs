#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    // Gate: ClearanceValidator.Check reproduces, line by line and exit code, the stdout of the 2D game's own
    // tools/check-item-clearance.js (tools/clearance-ref/<id>.json, produced by tools/export-clearance.mjs).
    // The 2D repository (DESKTOPDRIFT_DIR, default /Users/letulip/Projects/Claude/DesktopDrift) is REQUIRED: without it
    // every parity test here FAILS; only DD3D_SKIP_2D_PARITY=1 turns that into an explicit Ignore.
    [TestFixture]
    public class ClearanceValidatorTests
    {
        private static string DesktopDriftDir()
        {
            string dd = Environment.GetEnvironmentVariable("DESKTOPDRIFT_DIR") ?? "/Users/letulip/Projects/Claude/DesktopDrift";
            if (!Directory.Exists(dd))
            {
                string msg = "DesktopDrift 2D source directory not found: '" + dd + "' (set DESKTOPDRIFT_DIR) – CLEARANCE PARITY NOT CHECKED";
                if (Environment.GetEnvironmentVariable("DD3D_SKIP_2D_PARITY") == "1") Assert.Ignore(msg + " (DD3D_SKIP_2D_PARITY=1)");
                Assert.Fail(msg + ". Set DD3D_SKIP_2D_PARITY=1 to skip explicitly.");
            }
            return dd;
        }

        private static Dictionary<string, double> Radii()
        {
            var radii = new Dictionary<string, double>();
            var items = (Dictionary<string, object?>)Json.Parse(File.ReadAllText(TestPaths.Locate("unity/Assets/Content/_json/items.json")))!;
            foreach (KeyValuePair<string, object?> kv in items)
            {
                if (!kv.Key.StartsWith("ITEM_", StringComparison.Ordinal)) continue;
                radii[kv.Key] = (double)((Dictionary<string, object?>)kv.Value!)["r"]!;
            }
            return radii;
        }

        private static ClearanceValidator.Report Run(string dd, string id)
        {
            string svgPath = Path.Combine(dd, "tracks", id + ".svg");
            return ClearanceValidator.Check(File.ReadAllText(svgPath), svgPath, Radii());
        }

        [TestCase("green-study")]
        [TestCase("cafe-marble")]
        [TestCase("dev-desk")]
        [TestCase("dining-oak")]
        [TestCase("steel-kitchen")]
        [TestCase("workbench")]
        public void OutputMatchesThe2DScript(string id)
        {
            string dd = DesktopDriftDir();
            var r = (Dictionary<string, object?>)Json.Parse(File.ReadAllText(TestPaths.Locate("tools/clearance-ref/" + id + ".json")))!;
            var expected = (List<object?>)r["lines"]!;
            ClearanceValidator.Report rep = Run(dd, id);
            Assert.That(rep.ExitCode, Is.EqualTo((int)(double)r["exitCode"]!), id + ": exit code");
            Assert.That(rep.Lines.Count, Is.EqualTo(expected.Count), id + ": line count");
            for (int i = 0; i < expected.Count; i++)
                Assert.That(rep.Lines[i], Is.EqualTo((string)expected[i]!), id + ": line " + i);
        }

        [Test]
        public void KnownOverlapsMatchThe2DGame()
        {
            string dd = DesktopDriftDir();
            var actual = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string id in TrackSources.TrackIds(File.ReadAllText(TestPaths.Locate("unity/Assets/Content/_json/tracks.json"))))
                foreach (ClearanceValidator.Result res in Run(dd, id).Results)
                    if (res.Margin < 0) actual.Add(id + "/" + res.RawId);
            var expected = new SortedSet<string>(StringComparer.Ordinal) { "workbench/ITEM_COMB" };
            Assert.That(actual, Is.EquivalentTo(expected),
                "2D data: move the item in the SVG of the 2D game, then rerun scripts/export-clearance.sh and update this set");
        }
    }
}
