// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/shop-catalog.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    public static class ShopCatalog
    {
        // shop-catalog.js: byKind – the catalog is passed in (data lives in shop.json). Unknown kind => empty.
        public static List<ShopItem> ByKind(IReadOnlyList<ShopItem> catalog, string kind)
        {
            var result = new List<ShopItem>();
            foreach (ShopItem item in catalog) if (item.Kind == kind) result.Add(item);
            return result;
        }
    }
}
