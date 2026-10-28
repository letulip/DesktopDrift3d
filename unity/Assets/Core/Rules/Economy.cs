// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/economy.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System;
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    public sealed class BuyResult
    {
        public bool Ok;
        public string? Reason; // "owned" | "broke" | null
        public int Wallet;
        public IReadOnlyList<string> Owned = Array.Empty<string>();
    }

    public static class Economy
    {
        public const double StarPps = 100;  // economy.js: STAR_PPS
        public const int MaxStars = 5;      // economy.js: MAX_STARS
        public const int FinishFlat = 2;    // economy.js: FINISH_FLAT
        public const int FinishPerStar = 2; // economy.js: FINISH_PER_STAR
        public const int FirstClearBonus = 20; // economy.js: FIRST_CLEAR_BONUS
        public const double DdkPps = 600;   // economy.js: DDK_PPS
        public const int OnePpsBonus = 5;   // economy.js: ONE_PPS_BONUS
        public const int UnbrokenBonus = 30; // economy.js: UNBROKEN_BONUS
        public const bool CarGatingEnabled = true; // economy.js: CAR_GATING_ENABLED
        public static readonly IReadOnlyList<string> FreeCars = new[] { "bismark", "panda" }; // economy.js: FREE_CARS

        // JS `x || 0`: NaN stands in for undefined/null (and NaN itself is falsy in JS too).
        private static double Num(double v) => double.IsNaN(v) ? 0 : v;

        public static int StarsForPps(double pps)
        {
            return (int)Math.Max(0, Math.Min(MaxStars, Math.Floor(Num(pps) / StarPps)));
        }

        public static int FinishPayout(double pps)
        {
            return FinishFlat + FinishPerStar * StarsForPps(pps);
        }

        public static bool IsDdk(double pps)
        {
            return Num(pps) >= DdkPps;
        }

        public static bool IsOnePps(double pps)
        {
            return CoreMath.JsRound(Num(pps)) == 1;
        }

        public static bool CanAfford(double balance, double price)
        {
            return Num(balance) >= price;
        }

        public static BuyResult Buy(int wallet, IReadOnlyList<string>? owned, string itemId, int price)
        {
            var have = owned ?? Array.Empty<string>();
            foreach (string o in have)
                if (o == itemId) return new BuyResult { Ok = false, Reason = "owned", Wallet = wallet, Owned = have };
            if (!CanAfford(wallet, price))
                return new BuyResult { Ok = false, Reason = "broke", Wallet = wallet, Owned = have };
            var next = new List<string>(have) { itemId };
            return new BuyResult { Ok = true, Reason = null, Wallet = wallet - price, Owned = next };
        }
    }
}
