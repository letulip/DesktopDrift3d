using System.Linq;
using DesktopDrift.Core;
using NUnit.Framework;
using UnityEngine;

namespace DesktopDrift.Game.Tests
{
    public class CollectibleViewsTests
    {
        static TrackData Synthetic()
        {
            var t = new TrackData();
            t.Collectibles.Add(new Collectible { Kind = "tire", X = 10, Y = 20, R = 20, Value = 1 });
            t.Collectibles.Add(new Collectible { Kind = "cola", X = 100, Y = 50, R = 30, CapId = "c1" });
            t.Collectibles.Add(new Collectible { Kind = "tire", X = -40, Y = 5, R = 20, Value = 1 });
            return t;
        }

        readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        T Make<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            _made.Add(go);
            return go.AddComponent<T>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        [Test]
        public void Tires_OneViewPerTire_NullForOthers_AtCoreRadius()
        {
            var tires = Make<Tires>();
            tires.Build(Synthetic());
            Assert.AreEqual(3, tires.Views.Count);
            Assert.IsNotNull(tires.Views[0]);
            Assert.IsNull(tires.Views[1]);
            Assert.IsNotNull(tires.Views[2]);
            Assert.AreEqual(2, tires.Views.Count(v => v != null && v.activeSelf));
            Assert.AreEqual(2f * 20f * WorldMapping.UnitsToMeters, tires.Views[0].transform.localScale.x, 1e-6f);
        }

        [Test]
        public void Caps_OneViewPerCola()
        {
            var caps = Make<ColaCaps>();
            caps.Build(Synthetic());
            Assert.AreEqual(3, caps.Views.Count);
            Assert.AreEqual(1, caps.Views.Count(v => v != null));
            Assert.IsNotNull(caps.Views[1]);
        }

        [Test]
        public void TireCollected_HidesView_RebuildRespawns()
        {
            var tires = Make<Tires>();
            var track = Synthetic();
            tires.Build(track);
            tires.OnRaceEvent(new RaceEvent(RaceEvent.EventKind.TireCollected) { Index = 0 });
            Assert.IsFalse(tires.Views[0].activeSelf);
            Assert.IsTrue(tires.Views[2].activeSelf);
            tires.Build(track);
            Assert.IsTrue(tires.Views[0].activeSelf);
            Assert.AreEqual(2, tires.transform.childCount);
        }
    }
}
