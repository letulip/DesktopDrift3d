namespace DesktopDrift.Core
{
    /// <summary>Car pose and velocity fed to the chase camera (game units, radians).</summary>
    public struct CarKinematics
    {
        public double X, Y, Angle, Vx, Vy, MaxSpeed;
    }
}
