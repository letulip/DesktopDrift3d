// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/car-order.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System;
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    public static class CarOrder
    {
        // car-order.js: ownedFirstOrder. The JS takes (cars, isOwned(car)); the car is modelled as its index.
        public static List<int> OwnedFirstOrder(int carCount, Func<int, bool> isOwnedIndex)
        {
            var result = new List<int>();
            for (int i = 0; i < carCount; i++) if (isOwnedIndex(i)) result.Add(i);
            for (int i = 0; i < carCount; i++) if (!isOwnedIndex(i)) result.Add(i);
            return result;
        }
    }
}
