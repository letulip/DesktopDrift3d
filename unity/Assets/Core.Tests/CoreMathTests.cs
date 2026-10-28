// CoreMath vectors: JsRound against Math.round semantics (review З1) and JsToFixed against
// Number.prototype.toFixed values computed by node (tools/jsfixed-ref.json, tools/js-refs.mjs).
#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    [TestFixture]
    public class CoreMathTests
    {
        [Test]
        public void JsRound_MatchesMathRound()
        {
            // node: Math.round(0.49999999999999994) === 0, Math.round(1.4999999999999998) === 1, Math.round(-0.5) === -0
            Assert.AreEqual(0, CoreMath.JsRound(0.49999999999999994));
            Assert.AreEqual(1, CoreMath.JsRound(1.4999999999999998));
            Assert.AreEqual(3, CoreMath.JsRound(2.5));
            Assert.AreEqual(0, CoreMath.JsRound(-0.5));
            Assert.AreEqual(-1, CoreMath.JsRound(-1.5));
            Assert.AreEqual(-2, CoreMath.JsRound(-2.5));
            Assert.AreEqual(4503599627370497, CoreMath.JsRound(4503599627370497));   // odd 2^52 + 1 stays
        }
    }
}
