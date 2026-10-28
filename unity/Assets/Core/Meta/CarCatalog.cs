// SPEC-02 step 3: the car list from Content/_json/cars.json (+ ratings from car-registry.json), so the host
// hands RaceRules the car the garage selected (garage.carIndex) instead of wiring one by hand.
#nullable enable
using System;
using System.Collections.Generic;

namespace DesktopDrift.Core
{
    public sealed class CarRatings
    {
        public double Accel, Handling, Speed;
    }

    public sealed class CarDefinition
    {
        public string Id = "", Name = "";
        public DriveProfile Drive = new DriveProfile();
        public double Len, Wid, Vw, Vh;
        public bool Flip;
        public double? Price;
        public CarRatings? Ratings;
    }

    public sealed class CarCatalog
    {
        public IReadOnlyList<CarDefinition> Cars = new List<CarDefinition>();

        private static Dictionary<string, object?> O(object? v) => (Dictionary<string, object?>)v!;
        private static List<object?> A(object? v) => (List<object?>)v!;
        private static double N(object? v) => (double)v!;

        public static CarCatalog FromJson(string carsJson, string? registryJson = null)
        {
            var ratings = new Dictionary<string, CarRatings>();
            if (registryJson != null)
            {
                foreach (object? e in A(O(Json.Parse(registryJson))["CAR_REGISTRY"]))
                {
                    var r = O(e);
                    if (r.TryGetValue("ratings", out object? rv) && rv is Dictionary<string, object?> rd)
                        ratings[(string)r["id"]!] = new CarRatings { Accel = N(rd["accel"]), Handling = N(rd["handling"]), Speed = N(rd["speed"]) };
                }
            }
            var cars = new List<CarDefinition>();
            foreach (object? e in A(O(Json.Parse(carsJson))["CARS"]))
            {
                var c = O(e);
                var d = O(c["drive"]);
                string id = (string)c["id"]!;
                cars.Add(new CarDefinition
                {
                    Id = id,
                    Name = (string)c["name"]!,
                    Drive = new DriveProfile
                    {
                        Thrust = N(d["thrust"]), MaxSpeed = N(d["maxSpeed"]), Steer = N(d["steer"]), SteerSmooth = N(d["steerSmooth"]),
                        LowSpeedTurn = N(d["lowSpeedTurn"]), SelfAlign = N(d["selfAlign"]), Grip = N(d["grip"]),
                        RollFriction = N(d["rollFriction"]), DriftDrag = N(d["driftDrag"]), DriftSteerBoost = N(d["driftSteerBoost"]),
                    },
                    Len = N(c["len"]), Wid = N(c["wid"]), Vw = N(c["vw"]), Vh = N(c["vh"]),
                    Flip = c.TryGetValue("flip", out object? f) && f is bool fb && fb,
                    Price = c.TryGetValue("price", out object? p) && p is double pd ? (double?)pd : null,
                    Ratings = ratings.TryGetValue(id, out CarRatings? cr) ? cr : null,
                });
            }
            return new CarCatalog { Cars = cars };
        }

        // game-engine.js:344: Math.max(0, Math.min(g.carIndex ?? 0, CARS.length - 1))
        public CarDefinition ByIndex(double carIndex)
        {
            double i = Math.Max(0, Math.Min(carIndex, Cars.Count - 1));
            return Cars[(int)i];
        }

        public static PhysicsConstants PhysicsFromConfig(string configJson)
        {
            var cfg = O(Json.Parse(configJson));
            return new PhysicsConstants { PhysHz = N(cfg["PHYS_HZ"]), GripWobble = N(cfg["GRIP_WOBBLE"]), SteerWobble = N(cfg["STEER_WOBBLE"]) };
        }

        public List<Achievements.ContentCar> ToContentCars()
        {
            var list = new List<Achievements.ContentCar>();
            foreach (CarDefinition c in Cars) list.Add(new Achievements.ContentCar { Id = c.Id, Price = c.Price });
            return list;
        }
    }
}
