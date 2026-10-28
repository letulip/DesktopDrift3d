// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/achievements.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    // The `buildContent` result (achievements.js lines 245-260). Default-constructed = `_content({})`.
    public sealed class ContentDescriptor
    {
        public List<string> ForwardIds = new List<string>();
        public List<string> ReversedIds = new List<string>();
        public List<string> AllInstanceIds = new List<string>();
        public List<string> ForwardTrackIds = new List<string>();
        public List<string> ShopCategories = new List<string>();
        public Dictionary<string, string> CatalogById = new Dictionary<string, string>(); // item id -> kind
        public List<string> Cars = new List<string>();                                    // ids of purchasable (paid) cars
        public Dictionary<string, string> Names = new Dictionary<string, string>();
    }
}
