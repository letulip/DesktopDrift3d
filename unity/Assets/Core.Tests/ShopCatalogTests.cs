#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    // Source: DesktopDrift/tests/shop-catalog.test.js @ 5556140; CATALOG loaded from shop.json
    [TestFixture]
    public class ShopCatalogTests
    {
        private static List<ShopItem> LoadCatalog()
        {
            var root = (Dictionary<string, object?>)Json.Parse(File.ReadAllText(
                TestPaths.Locate("unity/Assets/Content/_json/shop.json")))!;
            var list = new List<ShopItem>();
            foreach (object? o in (List<object?>)root["CATALOG"]!)
            {
                var d = (Dictionary<string, object?>)o!;
                list.Add(new ShopItem
                {
                    Id = (string)d["id"]!, Name = (string)d["name"]!, Kind = (string)d["kind"]!,
                    Value = (string)d["value"]!, Price = (int)(double)d["price"]!,
                });
            }
            return list;
        }

        private static readonly List<ShopItem> Catalog = LoadCatalog();

        // CATALOG invariants: all ids are unique
        [Test]
        public void AllIdsAreUnique()
        {
            var ids = Catalog.Select(i => i.Id).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
        }

        // CATALOG invariants: all prices are positive integers
        [Test]
        public void AllPricesArePositiveIntegers()
        {
            foreach (var item in Catalog) Assert.That(item.Price > 0, Is.True, item.Id + ": price " + item.Price);
        }

        // CATALOG invariants: all kinds are known
        [Test]
        public void AllKindsAreKnown()
        {
            var valid = new HashSet<string> { "finish", "trail", "glass", "outline", "expression", "neon-layout", "neon-anim" };
            foreach (var item in Catalog) Assert.That(valid.Contains(item.Kind), Is.True, item.Id + ": unknown kind '" + item.Kind + "'");
        }

        // CATALOG invariants: all items have non-empty id, name, value
        [Test]
        public void AllItemsHaveIdNameValue()
        {
            foreach (var item in Catalog)
            {
                Assert.That(item.Id, Is.Not.Empty);
                Assert.That(item.Name, Is.Not.Empty, item.Id + ": missing name");
                Assert.That(item.Value, Is.Not.Empty, item.Id + ": missing value");
            }
        }

        // byKind: returns finish items
        [Test]
        public void ByKind_ReturnsFinishItems()
        {
            var finishes = ShopCatalog.ByKind(Catalog, "finish");
            Assert.That(finishes.Count, Is.EqualTo(4));
            Assert.That(finishes.All(i => i.Kind == "finish"), Is.True);
        }

        // byKind: finish pricing matches plan (matte 40, metallic 80, pearl 150, chrome 250)
        [Test]
        public void ByKind_FinishPricing()
        {
            var f = ShopCatalog.ByKind(Catalog, "finish").ToDictionary(i => i.Value, i => i.Price);
            Assert.That(f["matte"], Is.EqualTo(40));
            Assert.That(f["metallic"], Is.EqualTo(80));
            Assert.That(f["pearl"], Is.EqualTo(150));
            Assert.That(f["chrome"], Is.EqualTo(250));
        }

        // byKind: returns trail items
        [Test]
        public void ByKind_ReturnsTrailItems()
        {
            var trails = ShopCatalog.ByKind(Catalog, "trail");
            Assert.That(trails.Count, Is.EqualTo(8));
            Assert.That(trails.All(i => i.Kind == "trail"), Is.True);
        }

        // byKind: trail values are hex colors
        [Test]
        public void ByKind_TrailValuesAreHexColors()
        {
            foreach (var item in ShopCatalog.ByKind(Catalog, "trail"))
                Assert.That(Regex.IsMatch(item.Value, "^#[0-9A-Fa-f]{6}$"), Is.True, item.Id + ": value '" + item.Value + "' is not a hex color");
        }

        // byKind: unknown kind returns empty array
        [Test]
        public void ByKind_UnknownKindReturnsEmpty()
        {
            Assert.That(ShopCatalog.ByKind(Catalog, "livery"), Is.Empty);
            Assert.That(ShopCatalog.ByKind(Catalog, ""), Is.Empty);
        }

        // Neon FX catalog: sells 3 layouts (solid stays free/unlisted) with valid neon.js layout ids
        [Test]
        public void NeonCatalog_Layouts()
        {
            var layouts = ShopCatalog.ByKind(Catalog, "neon-layout");
            Assert.That(layouts.Count, Is.EqualTo(3));
            var ids = new HashSet<string>(Neon.Layouts.Select(l => l.Id));
            foreach (var it in layouts) Assert.That(ids.Contains(it.Value), Is.True, it.Id + ": value '" + it.Value + "' not a layout");
            Assert.That(layouts.Any(it => it.Value == "solid"), Is.False, "solid must stay free (unlisted)");
        }

        // Neon FX catalog: sells 3 animations (static stays free/unlisted) with valid neon.js anim ids
        [Test]
        public void NeonCatalog_Anims()
        {
            var anims = ShopCatalog.ByKind(Catalog, "neon-anim");
            Assert.That(anims.Count, Is.EqualTo(3));
            var ids = new HashSet<string>(Neon.Anims.Select(a => a.Id));
            foreach (var it in anims) Assert.That(ids.Contains(it.Value), Is.True, it.Id + ": value '" + it.Value + "' not an anim");
            Assert.That(anims.Any(it => it.Value == "none"), Is.False, "static must stay free (unlisted)");
        }
    }
}
