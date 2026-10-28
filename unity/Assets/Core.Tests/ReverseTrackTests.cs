using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    // Port of DesktopDrift/tests/reverse-track.test.js @ 5556140 – same numbers, same names (in comments).
    [TestFixture]
    public class ReverseTrackTests
    {
        private static void AssertSame(IReadOnlyList<Vec2> expected, IReadOnlyList<Vec2> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].X, actual[i].X);
                Assert.AreEqual(expected[i].Y, actual[i].Y);
            }
        }

        // Synthetic circular track: N points around a circle, with inner/outer offset radially.
        private static TrackData MakeCircleTrack(int N = 24, double r = 100)
        {
            var t = new TrackData { K = 8, Laps = 3, Id = "circle" };
            for (int i = 0; i < N; i++)
            {
                double a = ((double)i / N) * Math.PI * 2;
                double c = Math.Cos(a), s = Math.Sin(a);
                t.Center.Add(new Vec2(r * c, r * s));
                t.Inner.Add(new Vec2((r - 20) * c, (r - 20) * s));
                t.Outer.Add(new Vec2((r + 20) * c, (r + 20) * s));
            }
            t.Cones = new List<Cone> { new Cone { X = 1, Y = 2 } };
            t.Props = new List<Prop> { new Prop { X = 3 } };
            t.Collectibles = new List<Collectible> { new Collectible { X = 5, Y = 6, Kind = "tire" } };
            t.Table = new TableRect { W = 1 };
            t.Theme = new TrackTheme { Track = "#fff" };
            return t;
        }

        // test('instanceId: bare id forward, `${id}:rev` reversed')
        [Test]
        public void InstanceId_BareForwardRevReversed()
        {
            Assert.AreEqual("green-study", TrackUtil.InstanceId("green-study", false));
            Assert.AreEqual("green-study:rev", TrackUtil.InstanceId("green-study", true));
        }

        // test('reverseTrack: centreline order is reversed; input not mutated')
        [Test]
        public void ReverseTrack_CentrelineReversedInputNotMutated()
        {
            var T = MakeCircleTrack();
            var origFirst = T.Center[0]; var origLast = T.Center[T.Center.Count - 1];
            var R = TrackUtil.ReverseTrack(T);
            Assert.AreEqual(origLast, R.Center[0]);
            Assert.AreEqual(origFirst, R.Center[R.Center.Count - 1]);
            Assert.AreEqual(T.Center.Count, R.Center.Count);
            // input untouched (pure)
            Assert.AreEqual(origFirst, T.Center[0]);
            Assert.AreNotSame(T.Center, R.Center);
        }

        // test('reverseTrack: inner/outer reversed in lockstep with center')
        [Test]
        public void ReverseTrack_InnerOuterReversedInLockstep()
        {
            var T = MakeCircleTrack();
            int N = T.Center.Count;
            var R = TrackUtil.ReverseTrack(T);
            Assert.AreEqual(T.Inner[N - 1], R.Inner[0]);
            Assert.AreEqual(T.Outer[N - 1], R.Outer[0]);
            Assert.AreEqual(N, R.Inner.Count);
            Assert.AreEqual(N, R.Outer.Count);
        }

        // test('reverseTrack: double reverse restores the original geometry')
        [Test]
        public void ReverseTrack_DoubleReverseRestoresGeometry()
        {
            var T = MakeCircleTrack();
            var RR = TrackUtil.ReverseTrack(TrackUtil.ReverseTrack(T));
            AssertSame(T.Center, RR.Center);
            AssertSame(T.Inner, RR.Inner);
            AssertSame(T.Outer, RR.Outer);
        }

        // test('reverseTrack: start moves to the reversed first point, facing the opposite way')
        [Test]
        public void ReverseTrack_StartMovesAndFlips180()
        {
            var T = MakeCircleTrack();
            int N = T.Center.Count;
            var R = TrackUtil.ReverseTrack(T);
            Assert.AreEqual(T.Center[N - 1].X, R.StartPos.X);
            Assert.AreEqual(T.Center[N - 1].Y, R.StartPos.Y);
            double fwdX = T.Center[N - 1].X - T.Center[N - 2].X, fwdY = T.Center[N - 1].Y - T.Center[N - 2].Y;
            double revX = R.Center[1].X - R.Center[0].X, revY = R.Center[1].Y - R.Center[0].Y;
            Assert.IsTrue(Math.Abs(revX + fwdX) < 1e-9 && Math.Abs(revY + fwdY) < 1e-9, "start direction should flip 180°");
            Assert.IsTrue(Math.Abs(R.StartAngle - Math.Atan2(revY, revX)) < 1e-9);
        }

        // test('reverseTrack: checkpoints recomputed; other content carried over')
        [Test]
        public void ReverseTrack_CheckpointsRecomputedContentCarriedOver()
        {
            var T = MakeCircleTrack();
            var R = TrackUtil.ReverseTrack(T);
            Assert.IsTrue(R.Checkpoints != null && R.Checkpoints.Count > 0);
            Assert.AreEqual(true, R.Reversed);
            Assert.AreEqual("circle", R.Id);
            Assert.AreEqual(3, R.Laps);
            Assert.AreSame(T.Collectibles, R.Collectibles);   // carried by reference (positions unchanged)
            Assert.AreSame(T.Cones, R.Cones);
            Assert.AreSame(T.Theme, R.Theme);
        }
    }
}
