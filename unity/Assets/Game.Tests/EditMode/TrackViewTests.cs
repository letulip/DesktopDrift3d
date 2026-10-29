// SPEC-03 step 3b: TrackView builds the 2D look - round-stroke ribbon, rect/ellipse table with edge, floor, theme colours
// through MaterialPropertyBlocks (no leaked material instances).
#nullable enable
using System.Collections.Generic;
using System.Reflection;
using DesktopDrift.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DesktopDrift.Game.Tests
{
    public sealed class TrackViewTests
    {
        static readonly string[] Ids = { "cafe-marble", "dev-desk", "dining-oak", "green-study", "steel-kitchen", "workbench", "oval" };
        const float Tol = 1f / 255f + 1e-4f;

        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        static TrackData Load(string id)
        {
            if (id == "oval") return OvalTrack.Build();
            var a = AssetDatabase.LoadAssetAtPath<TrackAsset>("Assets/Content/Tracks/" + id + ".asset");
            Assert.IsNotNull(a, "missing track asset " + id);
            return a!.ToTrackData();
        }

        TrackView BuildView(TrackData track)
        {
            var go = new GameObject("TrackViewTest");
            created.Add(go);
            var view = go.AddComponent<TrackView>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (string f in new[] { "tableMaterial", "ribbonMaterial", "coneMaterial", "propMaterial" })
            {
                var m = new Material(shader);
                created.Add(m);
                typeof(TrackView).GetField(f, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(view, m);
            }
            view.Build(track);
            return view;
        }

        static Renderer Child(TrackView v, string name)
        {
            var t = v.transform.Find(name);
            Assert.IsNotNull(t, "missing child " + name);
            return t!.GetComponent<Renderer>();
        }

        static Color BlockColor(Renderer r)
        {
            var b = new MaterialPropertyBlock();
            r.GetPropertyBlock(b);
            return b.GetColor("_BaseColor");
        }

        static Color Expected(string s, Color32 fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            var c = ThemeColor.Parse(s);
            return new Color((float)c.R, (float)c.G, (float)c.B, 1f);
        }

        static void AssertColor(Color expected, Color actual, string what)
        {
            Assert.AreEqual(expected.r, actual.r, Tol, what + " r");
            Assert.AreEqual(expected.g, actual.g, Tol, what + " g");
            Assert.AreEqual(expected.b, actual.b, Tol, what + " b");
        }

        static Mesh MeshOf(TrackView v, string name) => v.transform.Find(name)!.GetComponent<MeshFilter>().sharedMesh;

        [Test]
        public void Ribbon_IsTheRoundStrokeAndFacesUp([ValueSource(nameof(Ids))] string id)
        {
            var track = Load(id);
            var view = BuildView(track);
            var rib = RibbonGeometry.Build(track.Center, track.TrackHalf);
            var mesh = MeshOf(view, "Ribbon");
            Assert.AreEqual(rib.Triangles.Count / 3, mesh.triangles.Length / 3, id + " triangles");
            Assert.AreEqual(rib.Vertices.Count, mesh.vertexCount, id + " vertices");
            TestContext.Out.WriteLine(id + ": ribbon " + mesh.vertexCount + " vertices, " + mesh.triangles.Length / 3 + " triangles");
            mesh.RecalculateNormals();
            foreach (var n in mesh.normals) Assert.Greater(n.y, 0.99f, id + " ribbon normal");
        }

        [Test]
        public void Table_IsEllipseExactlyWhenRound([ValueSource(nameof(Ids))] string id)
        {
            var track = Load(id);
            AssertTable(id, track);
        }

        [Test]
        public void Table_RoundFlagOnTheOvalGivesAnEllipse()
        {
            var track = Load("oval");
            track.Table.Round = true;
            AssertTable("oval-round", track);
            track.Table.Round = false;
            AssertTable("oval-rect", track);
        }

        void AssertTable(string id, TrackData track)
        {
            var view = BuildView(track);
            var mesh = MeshOf(view, "Table");
            float a = (float)track.Table.W * WorldMapping.UnitsToMeters * 0.5f, b = (float)track.Table.H * WorldMapping.UnitsToMeters * 0.5f;
            bool onEllipse = mesh.vertexCount > 4;
            foreach (var v in mesh.vertices)
            {
                Assert.AreEqual(0f, v.y, 1e-7f, id + " table top at y = 0");
                if (v.sqrMagnitude < 1e-12f) continue; // the fan centre
                float e = (v.x / a) * (v.x / a) + (v.z / b) * (v.z / b);
                if (Mathf.Abs(e - 1f) > 1e-4f) onEllipse = false;
            }
            Assert.AreEqual(track.Table.Round, onEllipse, id + " ellipse iff Round");
            if (!track.Table.Round)
            {
                Assert.AreEqual(4, mesh.vertexCount, id);
                foreach (var v in mesh.vertices) { Assert.AreEqual(a, Mathf.Abs(v.x), 1e-6f); Assert.AreEqual(b, Mathf.Abs(v.z), 1e-6f); }
            }
            else Assert.AreEqual(129, mesh.vertexCount, id + " fan of 128 segments");
            mesh.RecalculateNormals();
            foreach (var n in mesh.normals) Assert.Greater(n.y, 0.99f, id + " table normal");
            // Edge: a flat band 0.2 mm above the top plus a 30-unit skirt.
            var edge = MeshOf(view, "TableEdge");
            Assert.AreEqual(-0.03f, edge.bounds.min.y, 1e-6f, id + " skirt depth");
            Assert.AreEqual(0.0002f, edge.bounds.max.y, 1e-6f, id + " band height");
        }

        [Test]
        public void Floor_IsBelowTheTableAndTenTimesWider([ValueSource(nameof(Ids))] string id)
        {
            var track = Load(id);
            var view = BuildView(track);
            var floor = MeshOf(view, "Floor");
            Assert.AreEqual(-0.03f, floor.bounds.min.y, 1e-6f);
            Assert.AreEqual(-0.03f, floor.bounds.max.y, 1e-6f);
            Assert.Less(floor.bounds.max.y, MeshOf(view, "Table").bounds.min.y + 1e-7f);
            Assert.AreEqual(10f * (float)track.Table.W * WorldMapping.UnitsToMeters, floor.bounds.size.x, 1e-4f);
            Assert.AreEqual(10f * (float)track.Table.H * WorldMapping.UnitsToMeters, floor.bounds.size.z, 1e-4f);
        }

        [Test]
        public void ThemeColours_ReachTableRibbonFloorConesAndCamera([ValueSource(nameof(Ids))] string id)
        {
            var track = Load(id);
            var camGo = new GameObject("TestCam") { tag = "MainCamera" };
            created.Add(camGo);
            camGo.AddComponent<Camera>();
            var view = BuildView(track);
            var th = track.Theme;
            var bg = Expected(th.Background, TrackView.Fallback.Background);
            AssertColor(Expected(th.Table, TrackView.Fallback.Table), BlockColor(Child(view, "Table")), id + " table");
            AssertColor(Expected(th.TableEdge, TrackView.Fallback.TableEdge), BlockColor(Child(view, "TableEdge")), id + " edge");
            AssertColor(Expected(th.Track, TrackView.Fallback.Track), BlockColor(Child(view, "Ribbon")), id + " ribbon");
            AssertColor(bg, BlockColor(Child(view, "Floor")), id + " floor");
            Assert.IsNotNull(Camera.main);
            AssertColor(bg, Camera.main!.backgroundColor, id + " camera");
            Assert.AreEqual(track.Cones.Count, view.ConeViews.Count);
            for (int i = 0; i < track.Cones.Count; i++)
                AssertColor(Expected(th.Cone, TrackView.Fallback.Cone), BlockColor(Child(view, "Cone" + i)), id + " cone " + i);
        }

        [Test]
        public void Materials_AreNotInstanced()
        {
            var view = BuildView(Load("cafe-marble"));
            foreach (var r in view.GetComponentsInChildren<MeshRenderer>())
                Assert.IsTrue(r.sharedMaterial == null || !r.sharedMaterial.name.EndsWith("(Instance)"), r.name);
        }
    }
}
