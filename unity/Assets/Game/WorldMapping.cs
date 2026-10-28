using DesktopDrift.Core;
using UnityEngine;

namespace DesktopDrift.Game
{
    /// <summary>The only place where 2D game coordinates/angles become Unity world coordinates/rotations.</summary>
    public static class WorldMapping
    {
        public const float UnitsToMeters = 0.001f;

        public static Vector3 ToWorld(double x, double y) => new Vector3((float)x, 0f, (float)-y) * UnitsToMeters;

        public static Vector3 ToWorld(Vec2 p) => ToWorld(p.X, p.Y);

        public static float YawDeg(double a) => (float)(a * Mathf.Rad2Deg) + 90f;

        public static Quaternion Yaw(double a) => Quaternion.Euler(0f, YawDeg(a), 0f);
    }
}
