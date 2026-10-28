#nullable enable
using System;
using System.Globalization;
using System.Numerics;

namespace DesktopDrift.Core
{
    /// <summary>Math helpers shared by the dotnet and Unity worlds.</summary>
    public static class CoreMath
    {
        // The single hypot used by both the dotnet and Unity worlds (do not use double.Hypot or
        // Math.Sqrt(x*x + y*y)). Mirrors V8's Math.hypot (src/builtins/math.tq, MathHypot) operation
        // for operation so the parity traces and the checkpoint argmax see the same ulps as the 2D game:
        // NaN/Infinity rules, scale by the largest magnitude, Kahan-compensated sum of squares, sqrt * max.
        // Order of the two arguments matters (same as the JS call sites). Not bit-exact only where a
        // compiler contracts `n * n - compensation` into an FMA (IL2CPP/clang may); the 1e-6 parity
        // tolerance is the gate, ulps are not.
        public static double Hypot(double x, double y)
        {
            bool oneIsNaN = false;
            double ax = 0, ay = 0, max = 0;
            if (double.IsNaN(x)) oneIsNaN = true; else { ax = Math.Abs(x); if (ax > max) max = ax; }
            if (double.IsNaN(y)) oneIsNaN = true; else { ay = Math.Abs(y); if (ay > max) max = ay; }
            if (double.IsPositiveInfinity(max)) return double.PositiveInfinity;
            if (oneIsNaN) return double.NaN;
            if (max == 0) return 0;
            double sum = 0, compensation = 0;
            double n = ax / max;
            double summand = n * n - compensation;
            double preliminary = sum + summand;
            compensation = (preliminary - sum) - summand;
            sum = preliminary;
            n = ay / max;
            summand = n * n - compensation;
            preliminary = sum + summand;
            compensation = (preliminary - sum) - summand;
            sum = preliminary;
            return Math.Sqrt(sum) * max;
        }

        // JS Math.round: half rounds toward +∞ (C# Math.Round is banker's rounding – never use it for parity).
        // Not floor(x + 0.5): that addition rounds 0.49999999999999994 up to 1 and odd integers >= 2^52 up by one.
        public static double JsRound(double x)
        {
            double f = Math.Floor(x);
            return (x - f) >= 0.5 ? f + 1 : f;
        }

        // ECMAScript Number.prototype.toFixed(f) (ECMA-262 §21.1.3.3), exact: n is the integer for which
        // n / 10^f − x is closest to zero on the EXACT binary value of x (mantissa · 2^e as BigInteger),
        // ties → the larger n. Negative x ⇒ "-" + toFixed(−x) (so (−0.0001).toFixed(0) = "-0"); −0 ⇒ "0".
        // |x| >= 1e21 (JS falls back to ToString) never occurs in the game and throws.
        public static string JsToFixed(double x, int f)
        {
            if (f < 0 || f > 100) throw new ArgumentOutOfRangeException(nameof(f), "toFixed digits must be 0..100");
            if (double.IsNaN(x)) return "NaN";
            if (Math.Abs(x) >= 1e21) throw new NotSupportedException("JsToFixed: |x| >= 1e21 uses the JS ToString form");
            string sign = "";
            if (x < 0) { sign = "-"; x = -x; }
            long bits = BitConverter.DoubleToInt64Bits(x);
            int exp = (int)((bits >> 52) & 0x7FF);
            long man = bits & 0xFFFFFFFFFFFFFL;
            if (exp == 0) exp = 1; else man |= 1L << 52;
            exp -= 1075;                                   // x = man · 2^exp exactly
            BigInteger num = new BigInteger(man) * BigInteger.Pow(10, f);
            BigInteger den = BigInteger.One;
            if (exp >= 0) num <<= exp; else den <<= -exp;
            BigInteger n = BigInteger.DivRem(num, den, out BigInteger rem);
            if (rem * 2 >= den) n += 1;                    // closest; a tie takes the larger n
            string m = n.ToString(CultureInfo.InvariantCulture);
            if (f != 0)
            {
                if (m.Length <= f) m = new string('0', f + 1 - m.Length) + m;
                int k = m.Length;
                m = m.Substring(0, k - f) + "." + m.Substring(k - f);
            }
            return sign + m;
        }

        // JS `+x.toFixed(f)` / `parseFloat(x.toFixed(f))`. "-0.000" parses to −0 as in JS on every runtime.
        public static double JsToFixedNumber(double x, int f)
        {
            string s = JsToFixed(x, f);
            double v = double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
            return v == 0 && s[0] == '-' ? -0.0 : v;
        }

        // ECMAScript Number::toString(x) (ECMA-262 §6.1.6.1.20) – what `${x}`, String(x) and JSON print – without
        // the runtime's "R" (on Mono / .NET Framework "R" is G15 then G17, not the shortest round-trip):
        // the shortest digit string 1..17 that parses back to x, laid out by the spec's exponent rules.
        // Round-trip probe for JsNumberToString. Mono's double.Parse throws OverflowException on strings at the
        // edge of the double range (e.g. "1.7976931348623157E+308", seen on Unity 6000.3 EditMode) where .NET Core
        // parses them; an overflow counts as "does not round-trip" so the search continues to 17 digits.
        private static bool RoundTrips(string es, double a)
        {
            try { return double.Parse(es, NumberStyles.Float, CultureInfo.InvariantCulture) == a; }
            catch (OverflowException) { return false; }
        }

        public static string JsNumberToString(double x)
        {
            if (double.IsNaN(x)) return "NaN";
            if (x == 0) return "0";
            if (double.IsInfinity(x)) return x > 0 ? "Infinity" : "-Infinity";
            string sign = x < 0 ? "-" : "";
            double a = Math.Abs(x);
            string digits = "";
            int e10 = 0;
            for (int p = 1; p <= 17; p++)
            {
                string es = a.ToString("E" + (p - 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                if (p < 17 && !RoundTrips(es, a)) continue;
                int ePos = es.IndexOf('E');
                digits = es.Substring(0, ePos).Replace(".", "").TrimEnd('0');
                if (digits.Length == 0) digits = "0";
                e10 = int.Parse(es.Substring(ePos + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                break;
            }
            int k = digits.Length, n = e10 + 1;           // x = 0.d1..dk · 10^n
            if (k <= n && n <= 21) return sign + digits + new string('0', n - k);
            if (0 < n && n <= 21) return sign + digits.Substring(0, n) + "." + digits.Substring(n);
            if (-6 < n && n <= 0) return sign + "0." + new string('0', -n) + digits;
            string ex = (n - 1 >= 0 ? "+" : "-") + Math.Abs(n - 1).ToString(CultureInfo.InvariantCulture);
            return sign + digits.Substring(0, 1) + (k > 1 ? "." + digits.Substring(1) : "") + "e" + ex;
        }
    }
}
