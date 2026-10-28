namespace DesktopDrift.Core
{
    /// <summary>Chase-camera state: heading, eye position and look point (game units, H = height).</summary>
    public struct CamState
    {
        public double Yaw, YawTarget;
        public double PosX, PosY, PosH;
        public double LookX, LookY, LookH;
    }
}
