// SPEC-03 step 2: the committed track assets (Assets/Content/Tracks) are current w.r.t. the 2D SVGs + content JSON and
// round-trip exactly. The 2D repository (DESKTOPDRIFT_DIR, default /Users/letulip/Projects/Claude/DesktopDrift) is
// REQUIRED for the staleness guard: without it that test FAILS; only DD3D_SKIP_2D_PARITY=1 turns it into an Ignore.
// On failure: run scripts/import-tracks.sh and commit the result.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using DesktopDrift.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DesktopDrift.Game.Tests
{
    public sealed class TrackAssetTests
    {
        const string Hint = " - run scripts/import-tracks.sh";
        const string Dir = "Assets/Content/Tracks";

        static string Content(string file) { return File.ReadAllText(Path.Combine(Application.dataPath, "Content/_json", file)); }

        static Dictionary<string, object?> Row(string id)
        {
            foreach (object? r in (List<object?>)((Dictionary<string, object?>)Json.Parse(Content("tracks.json"))!)["TRACKS"]!)
            {
                var row = (Dictionary<string, object?>)r!;
                if ((string)row["id"]! == id) return row;
            }
            throw new AssertionException("track '" + id + "' missing in tracks.json");
        }

        static TrackAsset Load(string id)
        {
            var a = AssetDatabase.LoadAssetAtPath<TrackAsset>(Dir + "/" + id + ".asset");
            Assert.IsNotNull(a, "missing " + Dir + "/" + id + ".asset" + Hint);
            Assert.IsNotNull(a!.Data, id + ": Data not set" + Hint);
            return a;
        }

        static string DesktopDriftDir()
        {
            string dd = Environment.GetEnvironmentVariable("DESKTOPDRIFT_DIR") ?? "/Users/letulip/Projects/Claude/DesktopDrift";
            if (!Directory.Exists(dd))
            {
                string msg = "DesktopDrift 2D source directory not found: '" + dd + "' (set DESKTOPDRIFT_DIR) - TRACK STALENESS NOT CHECKED";
                if (Environment.GetEnvironmentVariable("DD3D_SKIP_2D_PARITY") == "1") Assert.Ignore(msg + " (DD3D_SKIP_2D_PARITY=1)");
                Assert.Fail(msg + ". Set DD3D_SKIP_2D_PARITY=1 to skip explicitly.");
            }
            return dd;
        }

        [TestCase("green-study")]
        [TestCase("steel-kitchen")]
        [TestCase("workbench")]
        [TestCase("cafe-marble")]
        [TestCase("dev-desk")]
        [TestCase("dining-oak")]
        public void CommittedAssetIsCurrentAndExact(string id)
        {
            TrackAsset a = Load(id);
            var row = Row(id);
            Assert.AreEqual(id, a.Id, "Id" + Hint);
            Assert.AreEqual((string)row["name"]!, a.DisplayName, "DisplayName" + Hint);
            Assert.AreEqual((string)row["desc"]!, a.Description, "Description" + Hint);

            string svg = File.ReadAllText(Path.Combine(DesktopDriftDir(), "tracks", id + ".svg"));
            string expected = TrackDataJson.Serialize(TrackSources.BuildFromContent(svg, id,
                Content("tracks.json"), Content("items.json"), Content("collectibles.json"))) + "\n";
            Assert.AreEqual(expected, a.Data!.text, id + ": committed track data is stale vs the 2D SVG / content JSON" + Hint);

            Assert.AreEqual(a.Data.text, TrackDataJson.Serialize(a.ToTrackData()) + "\n", id + ": ToTrackData does not re-serialize byte-identical" + Hint);
        }

        [Test]
        public void AssetSetEqualsTracksJsonIds()
        {
            var want = new List<string>(TrackSources.TrackIds(Content("tracks.json")));
            var have = new List<string>();
            foreach (string path in Directory.GetFiles(Path.Combine(Application.dataPath, "Content/Tracks"), "*.asset"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (name != "TrackCatalog") have.Add(name);
            }
            want.Sort(StringComparer.Ordinal);
            have.Sort(StringComparer.Ordinal);
            CollectionAssert.AreEqual(want, have, "Content/Tracks assets differ from tracks.json ids" + Hint);
        }

        [Test]
        public void CatalogListsTracksJsonIdsInOrder()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<TrackCatalog>(Dir + "/TrackCatalog.asset");
            Assert.IsNotNull(catalog, "missing " + Dir + "/TrackCatalog.asset" + Hint);
            var have = new List<string>();
            foreach (TrackAsset t in catalog!.Tracks) have.Add(t.Id);
            CollectionAssert.AreEqual(new List<string>(TrackSources.TrackIds(Content("tracks.json"))), have, "catalog differs from tracks.json" + Hint);
        }
    }
}
