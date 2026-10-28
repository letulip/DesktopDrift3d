using NUnit.Framework;
using UnityEngine;

namespace DesktopDrift.Game.Tests
{
    public class CameraRigTests
    {
        static readonly Vector3 Car = new Vector3(0.3f, 0f, -0.2f);

        [Test]
        public void LooksDownAndTowardPlusZ()
        {
            CameraRig.C1Pose(Car, 60f, 0.9f, 0f, 60f, out _, out var rot);
            var f = rot * Vector3.forward;
            Assert.Less(f.y, 0f);
            Assert.Greater(f.z, 0f);
        }

        [Test]
        public void ZeroOffsetIsCollinearAtDistance()
        {
            CameraRig.C1Pose(Car, 60f, 0.9f, 0f, 60f, out var pos, out var rot);
            var d = Car - pos;
            Assert.AreEqual(0.9f, d.magnitude, 1e-5f);
            Assert.AreEqual(0f, Vector3.Cross(d.normalized, rot * Vector3.forward).magnitude, 1e-5f);
        }

        [Test]
        public void TenPercentOffsetPutsCarMinusPointTwoNdc()
        {
            CameraRig.C1Pose(Car, 60f, 0.9f, 0.1f, 60f, out var pos, out var rot);
            var m = Matrix4x4.Perspective(60f, 1f, 0.01f, 50f) * Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(pos, rot, Vector3.one).inverse;
            var c = m * new Vector4(Car.x, Car.y, Car.z, 1f);
            Assert.AreEqual(-0.2f, c.y / c.w, 0.01f);
        }

        [Test]
        public void CameraIsAboveCar()
        {
            CameraRig.C1Pose(Car, 60f, 0.9f, 0.1f, 60f, out var pos, out _);
            Assert.Greater(pos.y, Car.y);
        }

        [Test]
        public void PresetHasC3Defaults()
        {
            var p = ScriptableObject.CreateInstance<CameraPreset>();
            try
            {
                Assert.AreEqual(55.0, p.c2.FovMin);
                Assert.AreEqual(1.0 / 0.65, p.c2.MobileDistanceScale, 1e-12);
            }
            finally
            {
                Object.DestroyImmediate(p);
            }
        }

        [Test]
        public void PresetKeepsSerializableC2Defaults()
        {
            var p = ScriptableObject.CreateInstance<CameraPreset>();
            var q = ScriptableObject.CreateInstance<CameraPreset>();
            try
            {
                Assert.IsNotNull(p.c2);
                Assert.AreEqual(574.0, p.c2.Distance);
                p.c2.Distance = 600;
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(p), q);
                Assert.AreEqual(600.0, q.c2.Distance, "c2 serializes inside the preset");
            }
            finally
            {
                Object.DestroyImmediate(p);
                Object.DestroyImmediate(q);
            }
        }
    }
}
