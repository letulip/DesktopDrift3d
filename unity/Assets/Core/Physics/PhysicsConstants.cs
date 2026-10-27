namespace DesktopDrift.Core
{
    /// <summary>Physics constants from config.js (JS: K).</summary>
    public sealed class PhysicsConstants
    {
        public double PhysHz;      // PHYS_HZ: reference normalisation frequency (120 = crisp/ideal)
        public double GripWobble;  // GRIP_WOBBLE: grip breathe amplitude (0 = perfect circle)
        public double SteerWobble; // STEER_WOBBLE: heading drift amplitude, rad/s
    }
}
