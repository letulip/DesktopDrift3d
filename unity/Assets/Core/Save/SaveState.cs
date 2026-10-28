// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/store.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DesktopDrift.Core
{
    // The save tree (Root, as produced by SaveSchema.Load / Json.Parse) plus the store.js API over it.
    // Getters return LIVE objects of the tree. JS `save()` after a mutation maps to: every mutating
    // method here increments Revision and invokes OnChanged (the Unity layer persists
    // Json.Serialize(Root) there). Code that mutates a returned live object directly (Settings(),
    // Garage(), CarLook(), ...) calls Save() afterwards, exactly like the JS. Persistence itself,
    // replaceAll and the 2D migrations are not part of Core (D-17).
    public sealed class SaveState
    {
        public const int LedgerMax = 50; // store.js LEDGER_MAX

        public Dictionary<string, object?> Root { get; }
        public int Revision { get; private set; }
        public Action? OnChanged;

        // store.js: _pushLedger stamps `t: Date.now()`. Core has no clock (determinism invariant,
        // 03-architecture §4): the Unity layer sets NowMs = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // tests leave the default, so ledger timestamps are 0 there.
        public Func<double> NowMs = () => 0;

        public SaveState(Dictionary<string, object?> root)
        {
            Root = root;
        }

        // store.js save(): signal that the tree changed.
        public void Save()
        {
            Revision++;
            OnChanged?.Invoke();
        }

        // ── helpers over the tree ───────────────────────────────────────────────
        private static Dictionary<string, object?> DictIn(Dictionary<string, object?> parent, string key)
        {
            if (parent.TryGetValue(key, out object? v) && v is Dictionary<string, object?> d) return d;
            var n = new Dictionary<string, object?>();
            parent[key] = n;
            return n;
        }

        private static List<object?> ListIn(Dictionary<string, object?> parent, string key)
        {
            if (parent.TryGetValue(key, out object? v) && v is List<object?> l) return l;
            var n = new List<object?>();
            parent[key] = n;
            return n;
        }

        private static double Num(object? v)
        {
            return v is double d ? d : 0;
        }

        private static bool Truthy(object? v)
        {
            switch (v)
            {
                case null: return false;
                case bool b: return b;
                case double d: return d != 0 && !double.IsNaN(d);
                case string s: return s.Length > 0;
                default: return true;
            }
        }

        private static List<string> Strings(List<object?> l)
        {
            var r = new List<string>();
            foreach (object? o in l) if (o is string s) r.Add(s);
            return r;
        }

        // store.js:146 snapshot
        public Dictionary<string, object?> Snapshot() { return Root; }

        // ── live slices ─────────────────────────────────────────────────────────
        public Dictionary<string, object?> Settings() { return DictIn(Root, "settings"); }
        public Dictionary<string, object?> Garage() { return DictIn(Root, "garage"); }
        public Dictionary<string, object?> Records() { return DictIn(Root, "records"); }
        public Dictionary<string, object?> Achievements() { return DictIn(Root, "achievements"); }
        public Dictionary<string, object?> Stats() { return DictIn(Root, "stats"); }

        // ── Achievements slice: { [id]: { unlocked: bool, progress: number } } ──
        public Dictionary<string, object?> AchAll() { return Achievements(); }

        public HashSet<string> AchUnlocked()
        {
            Dictionary<string, object?> a = Achievements();
            var set = new HashSet<string>();
            foreach (KeyValuePair<string, object?> kv in a)
                if (kv.Value is Dictionary<string, object?> e && e.TryGetValue("unlocked", out object? u) && Truthy(u))
                    set.Add(kv.Key);
            return set;
        }

        private Dictionary<string, object?> AchEntry(string id)
        {
            Dictionary<string, object?> a = Achievements();
            if (a.TryGetValue(id, out object? v) && v is Dictionary<string, object?> e) return e;
            var n = new Dictionary<string, object?> { ["unlocked"] = false, ["progress"] = 0.0 };
            a[id] = n;
            return n;
        }

        // Idempotent. Returns true the first time.
        public bool AchUnlock(string id)
        {
            Dictionary<string, object?> e = AchEntry(id);
            if (e.TryGetValue("unlocked", out object? u) && Truthy(u)) return false;
            e["unlocked"] = true;
            Save();
            return true;
        }

        // Latches to the max seen (progress never regresses).
        public void AchSetProgress(string id, double n)
        {
            Dictionary<string, object?> e = AchEntry(id);
            e.TryGetValue("progress", out object? cur);
            double next = Math.Max(cur is double cd ? cd : 0, n);
            if (!(cur is double c0) || c0 != next)
            {
                e["progress"] = next;
                Save();
            }
        }

        // ── caps ────────────────────────────────────────────────────────────────
        // store.js collectedCaps: the raw collected entries for a track (empty if none yet) – a copy.
        // Entries are whatever capCollect stored: capId strings ("x,y" for caps, "t<k>" for tires), or
        // numeric indices (double) from the oldest saves.
        public List<object?> CollectedCaps(string trackId)
        {
            Dictionary<string, object?> caps = DictIn(Stats(), "caps");
            return caps.TryGetValue(trackId, out object? v) && v is List<object?> arr ? new List<object?>(arr) : new List<object?>();
        }

        // store.js capCollect: appends the key (`!arr.includes(idx)` – SameValueZero, here object.Equals on a
        // boxed double or a string). Integers are stored as double, like every JSON number. No-op if present.
        public void CapCollect(string trackId, object key)
        {
            object boxed = key is int k ? (double)k : key;
            Dictionary<string, object?> caps = DictIn(Stats(), "caps");
            List<object?> arr = ListIn(caps, trackId);
            if (!arr.Contains(boxed))
            {
                arr.Add(boxed);
                Save();
            }
        }

        // ctx.caps for the achievement checks: stats.caps as it is (track id -> raw collected entries);
        // achievements.js only reads `.length` of each list.
        public Dictionary<string, List<object?>> CapsMap()
        {
            var r = new Dictionary<string, List<object?>>();
            if (Stats().TryGetValue("caps", out object? c) && c is Dictionary<string, object?> caps)
                foreach (KeyValuePair<string, object?> kv in caps)
                    if (kv.Value is List<object?> arr) r[kv.Key] = new List<object?>(arr);
            return r;
        }

        // ── Tire-coin economy ───────────────────────────────────────────────────
        public int Wallet()
        {
            Root.TryGetValue("wallet", out object? w);
            return (int)Num(w);
        }

        private void PushLedger(double amount, string reason)
        {
            List<object?> ledger = ListIn(Root, "ledger");
            ledger.Add(new Dictionary<string, object?>
            {
                ["t"] = NowMs(),
                ["amount"] = amount,
                ["reason"] = reason,
                ["balance"] = (double)Wallet(),
            });
            if (ledger.Count > LedgerMax) ledger.RemoveRange(0, ledger.Count - LedgerMax);
        }

        // Add (or remove, if n<0) tire coins; clamped at 0. Returns the new balance. A non-empty reason
        // logs a history entry; pass none for silent changes (per-tire pickups).
        public int AddTires(int n, string reason = "")
        {
            double before = Wallet();
            double after = Math.Max(0, before + n);
            Root["wallet"] = after;
            double delta = after - before;
            if (delta != 0 && !string.IsNullOrEmpty(reason)) PushLedger(delta, reason);
            Save();
            return Wallet();
        }

        // Log a history entry for coins already added to the wallet WITHOUT changing the balance.
        public void RecordTxn(int amount, string reason)
        {
            if (amount == 0) return;
            PushLedger(amount, reason);
            Save();
        }

        // The tire-coin transaction history (oldest first); entries are { t, amount, reason, balance }.
        public List<object?> Ledger() { return ListIn(Root, "ledger"); }

        // ── per-instance markers ────────────────────────────────────────────────
        private bool Has(string statsKey, string instanceId)
        {
            return ListIn(Stats(), statsKey).Contains(instanceId);
        }

        private bool Mark(string statsKey, string instanceId)
        {
            List<object?> l = ListIn(Stats(), statsKey);
            if (l.Contains(instanceId)) return false;
            l.Add(instanceId);
            Save();
            return true;
        }

        public bool TireSwept(string instanceId) { return Has("tiresSwept", instanceId); }
        // Returns true the FIRST time (-> award the bonus).
        public bool MarkTireSwept(string instanceId) { return Mark("tiresSwept", instanceId); }

        public bool HasTrophy(string instanceId) { return Has("trophies", instanceId); }
        public bool MarkTrophy(string instanceId) { return Mark("trophies", instanceId); }

        public bool HasPerpetual(string instanceId) { return Has("perpetuals", instanceId); }
        public bool MarkPerpetual(string instanceId) { return Mark("perpetuals", instanceId); }

        public bool MarkCleared(string instanceId) { return Mark("cleared", instanceId); }

        // ── Shop: owned cosmetics ───────────────────────────────────────────────
        public List<object?> Owned() { return ListIn(Root, "owned"); }
        public bool IsOwned(string id) { return Owned().Contains(id); }

        public void Grant(string id)
        {
            List<object?> o = Owned();
            if (!o.Contains(id))
            {
                o.Add(id);
                Save();
            }
        }

        // ── Car ownership ───────────────────────────────────────────────────────
        public List<object?> OwnedCars() { return ListIn(Root, "ownedCars"); }

        public bool CarOwned(string id) { return Economy.CarOwned(id, Strings(OwnedCars())); }

        public void GrantCar(string id)
        {
            List<object?> o = OwnedCars();
            if (!o.Contains(id))
            {
                o.Add(id);
                Save();
            }
        }

        // Deduct the price + grant the car, only if affordable and not already owned.
        public BuyResult BuyCar(string id, int price)
        {
            if (CarOwned(id)) return new BuyResult { Ok = false, Reason = "owned", Wallet = Wallet() };
            if (Wallet() < price) return new BuyResult { Ok = false, Reason = "broke", Wallet = Wallet() };
            AddTires(-price, "Bought car: " + id);
            GrantCar(id);
            return new BuyResult { Ok = true, Wallet = Wallet() };
        }

        // The per-car equipped look for a car index (live object, lazily created with null defaults).
        public Dictionary<string, object?> CarLook(int carIndex)
        {
            Dictionary<string, object?> cars = DictIn(Garage(), "cars");
            string key = carIndex.ToString(CultureInfo.InvariantCulture);
            if (cars.TryGetValue(key, out object? v) && v is Dictionary<string, object?> look) return look;
            var n = new Dictionary<string, object?>
            {
                ["bodyColor"] = null, ["neonColor"] = null, ["finish"] = null, ["trailColor"] = null,
                ["glassColor"] = null, ["outlineColor"] = null, ["neon"] = null, ["expression"] = null,
            };
            cars[key] = n;
            return n;
        }

        // Pure Economy.Buy against the current wallet+owned snapshot, then commit (deduct + grant) on success.
        public BuyResult Purchase(ShopItem item)
        {
            BuyResult result = Economy.Buy(Wallet(), Strings(Owned()), item.Id, item.Price);
            if (result.Ok)
            {
                AddTires(-item.Price, "Bought " + (string.IsNullOrEmpty(item.Name) ? "item" : item.Name));
                Grant(item.Id);
            }
            return result;
        }
    }
}
