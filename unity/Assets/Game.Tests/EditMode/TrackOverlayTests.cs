// SPEC-03 step 3d: the chequered start flag and the next-checkpoint ring, as js/render.js draws them.
#nullable enable
using System.Collections.Generic;
using System.Reflection;
using DesktopDrift.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DesktopDrift.Game.Tests
{
    public sealed class TrackOverlayTests
    {
        static readonly string[] Ids = { "cafe-marble", "dev-desk", "dining-oak", "green-study", "steel-kitchen", "workbench", "oval" };
        const float Tol = 1f / 255f + 1e-4f;
        const float U = WorldMapping.UnitsToMeters;

        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        static Material Overlay()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/TrackOverlay.mat");
            Assert.IsNotNull(m, "missing TrackOverlay.mat");
            return m;
        }

        static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

        TrackView BuildView(TrackData track)
        {
            var go = new GameObject("TrackOverlayTest");
            created.Add(go);
            var view = go.AddComponent<TrackView>();
            foreach (string f in new[] { "Table", "Ribbon", "Cone", "Prop" })
                Set(view, char.ToLowerInvariant(f[0]) + f.Substring(1) + "Material", AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/" + f + ".mat"));
            Set(view, "overlayMaterial", Overlay());
            Set(view, "renderJson", AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/_json/render.json"));
            view.Build(track);
            return view;
        }

        CheckpointRing BuildRing(TrackData track)
        {
            var go = new GameObject("RingTest");
            created.Add(go);
            var ring = go.AddComponent<CheckpointRing>();
            Set(ring, "material", Overlay());
            ring.Rebuild(track);
            return ring;
        }

        static Color Gamma(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.gamma : c;

        static void AssertColor(ThemeColor expected, Color actual, string what)
        {
            Color g = Gamma(actual);
            Assert.AreEqual(expected.R, g.r, Tol, what + " r");
            Assert.AreEqual(expected.G, g.g, Tol, what + " g");
            Assert.AreEqual(expected.B, g.b, Tol, what + " b");
            Assert.AreEqual(expected.A, g.a, Tol, what + " a");
        }

        [Test]
        public void OverlayMaterial_QueueIsAboveSkidMarks()
        {
            Assert.AreEqual(3001, Overlay().renderQueue);
            Assert.AreEqual(3000, AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/SkidMark.mat").renderQueue);
        }

        [TestCaseSource(nameof(Ids))]
        public void Flag_QuadsCentreAndColours(string id)
        {
            var track = TrackViewTests.LoadTrack(id);
            var view = BuildView(track);
            var flag = view.transform.Find("Flag");
            Assert.IsNotNull(flag, "missing Flag");
            var mesh = flag!.GetComponent<MeshFilter>().sharedMesh;
            int cols = Mathf.CeilToInt((float)(track.TrackHalf * 2) / 10f);
            Assert.AreEqual(2 * cols, mesh.vertexCount / 4, "quad count");
            Assert.AreEqual(2 * cols * 6, mesh.triangles.Length);
            Assert.AreEqual(3001, flag.GetComponent<MeshRenderer>().sharedMaterial.renderQueue);

            // Back to the 2D start frame: X along travel, Y across, origin at Center[0].
            var c0 = track.Center[0];
            double ca = System.Math.Cos(track.StartAngle), sa = System.Math.Sin(track.StartAngle);
            double minX = 1e9, maxX = -1e9, minY = 1e9, maxY = -1e9;
            foreach (var v in mesh.vertices)
            {
                double wx = v.x / U - c0.X, wy = -v.z / U - c0.Y;
                double lx = wx * ca + wy * sa, ly = -wx * sa + wy * ca;
                minX = System.Math.Min(minX, lx); maxX = System.Math.Max(maxX, lx);
                minY = System.Math.Min(minY, ly); maxY = System.Math.Max(maxY, ly);
                Assert.AreEqual(TrackView.FlagY, v.y, 1e-6f);
            }
            Assert.AreEqual(-10, minX, 1e-3); Assert.AreEqual(10, maxX, 1e-3);
            Assert.AreEqual(-track.TrackHalf, minY, 1e-3); Assert.AreEqual(-track.TrackHalf + cols * 10, maxY, 1e-3);
            // The flag's centre in the start frame is (0, -TrackHalf + cols*5) = (0, ~0): it maps to Center[0] (exactly for TrackHalf*2 % 10 == 0).
            Assert.AreEqual(0, (minX + maxX) / 2, 1e-3);
            Assert.AreEqual(0, (minY + maxY) / 2, cols * 5.0 - track.TrackHalf + 1e-3);

            ThemeColor ribbon = ThemeColor.Parse(view.EffectiveTheme!.Track);
            var expectedWhite = ThemeColor.Over(new ThemeColor(1, 1, 1, 0.92), ribbon);
            var expectedBlack = ThemeColor.Over(new ThemeColor(0, 0, 0, 0.82), ribbon);
            var distinct = new List<Color>();
            foreach (var c in mesh.colors) if (!distinct.Contains(c)) distinct.Add(c);
            Assert.AreEqual(2, distinct.Count, "two flag colours");
            // Cell (0,0) is white; its first quad is the first four vertices.
            AssertColor(expectedWhite, mesh.colors[0], "white");
            AssertColor(expectedBlack, mesh.colors[4], "black");
        }

        [TestCaseSource(nameof(Ids))]
        public void Ring_HiddenForZenAndFirstCheckpoint_ElseOnNextCheckpoint(string id)
        {
            var track = TrackViewTests.LoadTrack(id);
            var ring = BuildRing(track);

            ring.Place(track, null);
            Assert.IsFalse(ring.Visible, "no race state");
            ring.Place(track, new RaceState { NextCp = 0 });
            Assert.IsFalse(ring.Visible, "NextCp == 0");
            ring.Place(track, new RaceState { NextCp = 1, Zen = true });
            Assert.IsFalse(ring.Visible, "Zen");

            Assert.Greater(track.Checkpoints.Count, 1);
            for (int n = 1; n < track.Checkpoints.Count; n++)
            {
                ring.Place(track, new RaceState { NextCp = n });
                Assert.IsTrue(ring.Visible, "NextCp " + n);
                var cp = track.Checkpoints[n];
                Vector3 centre = WorldMapping.ToWorld(cp.X, cp.Y);
                Assert.AreEqual(centre.x, ring.transform.position.x, 1e-5f);
                Assert.AreEqual(centre.z, ring.transform.position.z, 1e-5f);
                float lo = (float)(track.CpR - 4.5) * U - 1e-5f, hi = (float)(track.CpR + 4.5) * U + 1e-5f;
                foreach (var v in ring.GetComponent<MeshFilter>().sharedMesh.vertices)
                {
                    Vector3 w = ring.transform.TransformPoint(v);
                    float d = new Vector2(w.x - centre.x, w.z - centre.z).magnitude;
                    Assert.That(d, Is.InRange(lo, hi), "vertex distance");
                    Assert.AreEqual(CheckpointRing.RingY, w.y, 1e-6f);
                }
            }
        }

        [Test]
        public void Ring_DarkBandThenCyanBand()
        {
            var track = TrackViewTests.LoadTrack("green-study");
            var ring = BuildRing(track);
            var mesh = ring.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(2 * 2 * CheckpointRing.Segments, mesh.vertexCount);
            var tris = mesh.triangles;
            var cols = mesh.colors;
            int half = tris.Length / 2;
            AssertColor(new ThemeColor(0, 0, 0, 0.55), cols[tris[0]], "dark first");
            AssertColor(ThemeColor.Parse("#7dd4ff"), cols[tris[half]], "cyan second");
            Assert.AreEqual(3001, ring.GetComponent<MeshRenderer>().sharedMaterial.renderQueue);
            float r = (float)track.CpR * U;
            foreach (var t in tris[..half]) Assert.AreEqual(4.5f * U, Mathf.Abs(new Vector2(mesh.vertices[t].x, mesh.vertices[t].z).magnitude - r), 1e-5f + 0f, "dark band edge");
        }
    }
}
