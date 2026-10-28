namespace DesktopDrift.Core
{
    /// <summary>The car's drive profile (JS: CARS[i]._drive).</summary>
    public sealed class DriveProfile
    {
        public double MaxSpeed;
        public double Thrust;
        public double Steer;
        public double LowSpeedTurn;
        public double SteerSmooth;
        public double SelfAlign;
        public double Grip;
        public double DriftSteerBoost;
        public double RollFriction;
        public double DriftDrag;
    }
}
