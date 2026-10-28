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

        // tools/jsfixed-ref.json: node's own toFixed on 164 doubles (the review's cases, ±0, 120 random of
        // mixed magnitude, 30 decimal near-ties) × digits 0 / 2 / 3.
        [Test]
        public void JsToFixed_MatchesNodeReference()
        {
            var root = (Dictionary<string, object?>)Json.Parse(File.ReadAllText(TestPaths.Locate("tools/jsfixed-ref.json")))!;
            var cases = (List<object?>)root["cases"]!;
            Assert.That(cases.Count, Is.GreaterThanOrEqualTo(450));
            var bad = new List<string>();
            foreach (object? o in cases)
            {
                var c = (List<object?>)o!;
                string xs = (string)c[0]!;
                double x = xs == "-0" ? -0.0 : double.Parse(xs, NumberStyles.Float, CultureInfo.InvariantCulture);
                int d = (int)(double)c[1]!;
                string want = (string)c[2]!, got = CoreMath.JsToFixed(x, d);
                if (got != want) bad.Add(xs + ".toFixed(" + d + "): JS " + want + " C# " + got);
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
            Assert.AreEqual("2.67", CoreMath.JsToFixed(2.675, 2));
            Assert.AreEqual("1.234", CoreMath.JsToFixed(1.2345, 3));
            Assert.AreEqual("-0.001", CoreMath.JsToFixed(-0.0005, 3));
            Assert.AreEqual("0", CoreMath.JsToFixed(-0.0, 0));
            Assert.IsTrue(double.IsNegative(CoreMath.JsToFixedNumber(-0.0001, 3)), "+(-0.0001).toFixed(3) is -0");
        }

        // Number::toString layout rules (node: String(x)).
        [TestCase(7.000000000000001, "7.000000000000001")]
        [TestCase(0.1, "0.1")]
        [TestCase(123456789012345680000.0, "123456789012345680000")]
        [TestCase(1e21, "1e+21")]
        [TestCase(1e-7, "1e-7")]
        [TestCase(0.000001, "0.000001")]
        [TestCase(-1.5e-10, "-1.5e-10")]
        [TestCase(5e-324, "5e-324")]
        [TestCase(1.7976931348623157e308, "1.7976931348623157e+308")]
        [TestCase(-852.0, "-852")]
        [TestCase(-0.0, "0")]
        public void JsNumberToString_MatchesJs(double x, string js)
        {
            Assert.AreEqual(js, CoreMath.JsNumberToString(x));
        }
    }
}
