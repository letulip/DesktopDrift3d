// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/physics.js @ 5556140 (2026-08-06), ported 2026-10-27.
//
// Pure car kinematics for one frame — no DOM, no singletons.
//
// This is a VERBATIM extraction of the integration that used to live inline in
// game-engine.js `frame()`. It is feel-critical: do NOT reorder the operations.
// Frame-rate independence comes from `Math.Pow(k, dt * PhysHz)` decay terms and a
// dt clamped by the caller — the math is identical at any refresh rate.
//
// Mutates:  car.{Vx,Vy,X,Y,Angle}  and  sm.{SteerSmooth,PhysT}
// Returns:  the pre-/in-step kinematics snapshot the caller's scoring + skid code needs
//           (StepResult { Drifting, Speed, VS, Fwd, Side }), matching the values those lines read
//           before this was extracted (VS is post-grip-decay; Speed/Fwd/Side are
//           pre-integration).
//
// car         : { X, Y, Vx, Vy, Angle }     (mutated)
// sm          : the game state S            (sm.SteerSmooth, sm.PhysT mutated)
// steerTarget : -1 | 0 | 1  (already resolved from keys/pointers by the caller)
// p           : the car's drive profile (CARS[i]._drive)
// k           : { PhysHz, GripWobble, SteerWobble } physics constants from config.js
using System;

namespace DesktopDrift.Core
{
    public static class CarPhysics
    {
        public static StepResult StepCar(CarState car, StepMotion sm, double steerTarget, DriveProfile p, PhysicsConstants k, double dt)
        {
            sm.SteerSmooth += (steerTarget - sm.SteerSmooth) * Math.Min(1.0, dt * p.SteerSmooth);

            var fwd = new Vec2(Math.Cos(car.Angle), Math.Sin(car.Angle));
            var side = new Vec2(-Math.Sin(car.Angle), Math.Cos(car.Angle));
            double vF = car.Vx * fwd.X + car.Vy * fwd.Y;
            double vS = car.Vx * side.X + car.Vy * side.Y;
            double speed = CoreMath.Hypot(car.Vx, car.Vy);
            bool drifting = Scoring.IsDrifting(vS, speed);

            sm.PhysT += dt;
            double wobSlow = Math.Sin(sm.PhysT * 0.8 + 1.7) + 0.5 * Math.Sin(sm.PhysT * 1.9 + 4.2);
            double wobFast = 0.6 * Math.Sin(sm.PhysT * 5.3 + 0.5) + 0.4 * Math.Sin(sm.PhysT * 12.1 + 2.1);
            double wob = 0.7 * wobSlow + 0.3 * wobFast;
            double live = Math.Min(1.0, speed / p.MaxSpeed) * (0.4 + 0.6 * Math.Min(1.0, Math.Abs(vS) / 80));
            double liveSteer = Math.Min(1.0, speed / p.MaxSpeed) * Math.Min(1.0, Math.Abs(vS) / 60);
            double fAdj = dt * k.PhysHz;
            double gripAdj = fAdj * (1 + k.GripWobble * wob * live);

            if (vF < p.MaxSpeed) vF += p.Thrust * dt;
            vF *= Math.Pow(p.RollFriction, fAdj);
            vS *= Math.Pow(p.Grip, gripAdj);
            vF *= Math.Max(0, 1 - p.DriftDrag * Math.Abs(vS) * dt);

            double turnFactor = Math.Max(p.LowSpeedTurn, Math.Min(speed / 160, 1.0));
            double authority = drifting ? p.DriftSteerBoost : 1;
            car.Angle += sm.SteerSmooth * p.Steer * turnFactor * authority * dt;
            car.Angle += k.SteerWobble * wobSlow * liveSteer * dt;

            car.Vx = fwd.X * vF + side.X * vS;
            car.Vy = fwd.Y * vF + side.Y * vS;

            if (speed > 40)
            {
                double moveAng = Math.Atan2(car.Vy, car.Vx);
                double diff = moveAng - car.Angle;
                while (diff > Math.PI) diff -= Math.PI * 2;
                while (diff < -Math.PI) diff += Math.PI * 2;
                car.Angle += diff * p.SelfAlign * Math.Min(1.0, speed / p.MaxSpeed) * dt;
            }

            car.X += car.Vx * dt;
            car.Y += car.Vy * dt;

            return new StepResult(drifting, speed, vS, fwd, side);
        }
    }
}
