namespace DesktopDrift.Core
{
    /// <summary>Mutable car kinematics (JS: the `car` object { x, y, vx, vy, angle }).</summary>
    public sealed class CarState
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public double Angle;
    }
}
