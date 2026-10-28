// SPEC-02 step 3: ContentDescriptor from the exported content JSON (tracks.json, shop.json, cars.json).
#nullable enable
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    public static class ContentJson
    {
        private static Dictionary<string, object?> O(object? v) => (Dictionary<string, object?>)v!;
        private static List<object?> A(object? v) => (List<object?>)v!;

        public static ContentDescriptor Build(string tracksJson, string shopJson, string carsJson)
        {
            var tracks = new List<Achievements.ContentTrack>();
            foreach (object? e in A(O(Json.Parse(tracksJson))["TRACKS"]))
            {
                var t = O(e);
                tracks.Add(new Achievements.ContentTrack { Id = (string)t["id"]!, Name = (string)t["name"]! });
            }
            var shop = new List<Achievements.ContentShopItem>();
            foreach (object? e in A(O(Json.Parse(shopJson))["CATALOG"]))
            {
                var i = O(e);
                shop.Add(new Achievements.ContentShopItem { Id = (string)i["id"]!, Kind = (string)i["kind"]! });
            }
            return Achievements.BuildContent(tracks, shop, CarCatalog.FromJson(carsJson).ToContentCars());
        }
    }
}
