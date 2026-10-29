// SPEC-03 step 1b: generates Assets/Content/Tracks/<id>.track.json + <id>.asset (TrackAsset) from the 2D game's SVGs.
// Reuses Core (TrackSources.BuildFromContent + TrackDataJson), no second parser. Idempotent: files and assets are
// only touched when their content differs. Run headless through scripts/import-tracks.sh.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DesktopDrift.Core;
using DesktopDrift.Game;
using UnityEditor;
using UnityEngine;

namespace DesktopDrift.Editor
{
    public static class TrackImporter
    {
        const string DefaultDir = "/Users/letulip/Projects/Claude/DesktopDrift";
        const string OutDir = "Assets/Content/Tracks";
        const string JsonDir = "Assets/Content/_json";

        [MenuItem("DesktopDrift/Import Tracks")]
        public static void ImportAll()
        {
            string dd = Environment.GetEnvironmentVariable("DESKTOPDRIFT_DIR") ?? DefaultDir;
            if (!Directory.Exists(dd))
            {
                Fail("DesktopDrift 2D source directory not found: '" + dd + "' (set DESKTOPDRIFT_DIR)");
                return;
            }

            string root = Path.GetDirectoryName(Application.dataPath)!;
            string tracks = File.ReadAllText(Path.Combine(root, JsonDir, "tracks.json"));
            string items = File.ReadAllText(Path.Combine(root, JsonDir, "items.json"));
            string collectibles = File.ReadAllText(Path.Combine(root, JsonDir, "collectibles.json"));

            var rows = new Dictionary<string, Dictionary<string, object?>>();
            foreach (object? r in (List<object?>)((Dictionary<string, object?>)Json.Parse(tracks)!)["TRACKS"]!)
            {
                var row = (Dictionary<string, object?>)r!;
                rows[(string)row["id"]!] = row;
            }

            Directory.CreateDirectory(Path.Combine(root, OutDir));
            foreach (string id in TrackSources.TrackIds(tracks))
            {
                string svgPath = Path.Combine(dd, "tracks", id + ".svg");
                if (!File.Exists(svgPath)) { Fail("SVG missing for track '" + id + "': " + svgPath); return; }
                TrackData t = TrackSources.BuildFromContent(File.ReadAllText(svgPath), id, tracks, items, collectibles);
                string text = TrackDataJson.Serialize(t) + "\n";
                byte[] bytes = new UTF8Encoding(false).GetBytes(text);

                string jsonRel = OutDir + "/" + id + ".track.json";
                string jsonAbs = Path.Combine(root, jsonRel);
                bool changed = !File.Exists(jsonAbs) || !BytesEqual(File.ReadAllBytes(jsonAbs), bytes);
                if (changed) File.WriteAllBytes(jsonAbs, bytes);
                AssetDatabase.ImportAsset(jsonRel, ImportAssetOptions.ForceSynchronousImport);
                var data = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonRel);

                string assetRel = OutDir + "/" + id + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<TrackAsset>(assetRel);
                bool created = asset == null;
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<TrackAsset>();
                    AssetDatabase.CreateAsset(asset, assetRel);
                }
                var meta = rows[id];
                bool assetChanged = created;
                assetChanged |= Set(ref asset.Id, id);
                assetChanged |= Set(ref asset.DisplayName, (string)meta["name"]!);
                assetChanged |= Set(ref asset.Description, (string)meta["desc"]!);
                if (asset.Data != data) { asset.Data = data; assetChanged = true; }
                if (assetChanged) EditorUtility.SetDirty(asset);

                Debug.Log("TRACK " + id + " center=" + t.Center.Count + " cones=" + t.Cones.Count + " props=" + t.Props.Count
                    + " collectibles=" + t.Collectibles.Count + " checkpoints=" + t.Checkpoints.Count + " bytes=" + bytes.Length
                    + " changed=" + ((changed || assetChanged) ? "true" : "false"));
            }
            UpdateCatalog(new List<string>(TrackSources.TrackIds(tracks)));
            AssetDatabase.SaveAssets();
        }

        // Assets/Content/Tracks/TrackCatalog.asset: the TrackAssets in tracks.json order; only dirtied when the list changed.
        static void UpdateCatalog(List<string> ids)
        {
            const string rel = OutDir + "/TrackCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<TrackCatalog>(rel);
            bool changed = catalog == null;
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<TrackCatalog>();
                AssetDatabase.CreateAsset(catalog, rel);
            }
            var want = new List<TrackAsset>();
            foreach (string id in ids) want.Add(AssetDatabase.LoadAssetAtPath<TrackAsset>(OutDir + "/" + id + ".asset"));
            if (!catalog.Tracks.SequenceEqual(want)) { catalog.Tracks = want; changed = true; }
            if (changed) EditorUtility.SetDirty(catalog);
            Debug.Log("TRACK catalog entries=" + want.Count + " changed=" + (changed ? "true" : "false"));
        }

        static bool Set(ref string field, string value)
        {
            if (field == value) return false;
            field = value;
            return true;
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static void Fail(string msg)
        {
            Debug.LogError(msg);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
