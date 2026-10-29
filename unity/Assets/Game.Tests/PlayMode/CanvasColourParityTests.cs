#nullable enable
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using DesktopDrift.Core;

namespace DesktopDrift.Game.Tests
{
    /// <summary>
    /// D-26: the project works in Gamma so flat colours, vertex colours and alpha blending equal the 2D canvas (sRGB).
    /// Renders the ribbon material with a skid-mark quad over half of it and compares bytes with the canvas formula.
    /// </summary>
    public class CanvasColourParityTests
    {
        const string RibbonHex = "#d3b483";   // cafe-marble track colour
        const string SkidCss = "rgba(15,9,6,0.3)"; // THEME_DEFAULT skid RGB at alpha 0.3
        const int Size = 64;
        const int IsolatedLayer = 31; // other PlayMode tests may leave the Game scene loaded: render only our own quads

        [UnityTest]
        public IEnumerator RibbonAndSkidOverRibbonEqualTheCanvasColours()
        {
#if UNITY_EDITOR
            var ribbonMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Ribbon.mat");
            var skidMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/SkidMark.mat");
            Assert.IsNotNull(ribbonMat, "Ribbon.mat");
            Assert.IsNotNull(skidMat, "SkidMark.mat");
            Assert.AreEqual(ColorSpace.Gamma, QualitySettings.activeColorSpace, "project colour space (D-26)");

            ThemeColor ribbon = ThemeColor.Parse(RibbonHex);
            ThemeColor skid = ThemeColor.Parse(SkidCss);
            var root = new GameObject("ColourParity");
            var rt = new RenderTexture(Size, Size, 24);
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var prev = RenderTexture.active;
            try
            {
                var camGo = new GameObject("Cam");
                camGo.transform.SetParent(root.transform);
                camGo.transform.SetPositionAndRotation(new Vector3(0, 5, 0), Quaternion.Euler(90, 0, 0));
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 1f;
                cam.aspect = 1f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 20f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.cullingMask = 1 << IsolatedLayer;
                cam.targetTexture = rt;

                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", new Color((float)ribbon.R, (float)ribbon.G, (float)ribbon.B, 1f));
                Quad(root.transform, "Ribbon", ribbonMat!, -1f, 1f, 0f, Color.white, block);
                Quad(root.transform, "Skid", skidMat!, 0f, 1f, 0.01f, TrackView.OverlayColor(skid), null);

                yield return null;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();

                Color32 plain = tex.GetPixel(Size / 4, Size / 2);
                Color32 covered = tex.GetPixel(3 * Size / 4, Size / 2);
                Color32 expPlain = Bytes(ribbon.R, ribbon.G, ribbon.B);
                Color32 expCovered = Bytes(
                    skid.A * skid.R + (1 - skid.A) * ribbon.R,
                    skid.A * skid.G + (1 - skid.A) * ribbon.G,
                    skid.A * skid.B + (1 - skid.A) * ribbon.B);
                Debug.Log($"CanvasColourParity ribbon={Fmt(plain)} expected={Fmt(expPlain)} covered={Fmt(covered)} expected={Fmt(expCovered)}");
                AssertNear(expPlain, plain, 2, "uncovered ribbon pixel");
                AssertNear(expCovered, covered, 3, "skid over ribbon pixel");
            }
            finally
            {
                RenderTexture.active = prev;
                Object.Destroy(root);
                rt.Release();
                Object.Destroy(rt);
                Object.Destroy(tex);
            }
#else
            yield return null;
            Assert.Ignore("Editor-only: loads materials through AssetDatabase.");
#endif
        }

        static Color32 Bytes(double r, double g, double b) =>
            new Color32((byte)System.Math.Round(r * 255), (byte)System.Math.Round(g * 255), (byte)System.Math.Round(b * 255), 255);

        static string Fmt(Color32 c) => $"({c.r},{c.g},{c.b})";

        static void AssertNear(Color32 expected, Color32 actual, int tol, string what)
        {
            string msg = $"{what}: expected {Fmt(expected)} actual {Fmt(actual)} (tolerance {tol}/255)";
            Assert.LessOrEqual(Mathf.Abs(expected.r - actual.r), tol, msg + " r");
            Assert.LessOrEqual(Mathf.Abs(expected.g - actual.g), tol, msg + " g");
            Assert.LessOrEqual(Mathf.Abs(expected.b - actual.b), tol, msg + " b");
        }

        // Horizontal quad facing up (clockwise seen from above = front face), x in [x0, x1], z in [-1, 1].
        static void Quad(Transform parent, string name, Material mat, float x0, float x1, float y, Color vertexColour, MaterialPropertyBlock? block)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(x0, y, -1), new Vector3(x0, y, 1), new Vector3(x1, y, 1), new Vector3(x1, y, -1) };
            mesh.colors = new[] { vertexColour, vertexColour, vertexColour, vertexColour };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.RecalculateBounds();
            var go = new GameObject(name) { layer = IsolatedLayer };
            go.transform.SetParent(parent);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (block != null) r.SetPropertyBlock(block);
        }
    }
}
