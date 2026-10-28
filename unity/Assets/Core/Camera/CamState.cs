namespace DesktopDrift.Core
{
    /// <summary>Chase-camera state: heading, eye position and look point (game units, H = height), FOV.</summary>
    public struct CamState
    {
        public double Yaw, YawTarget;
        public double PosX, PosY, PosH;
        public double LookX, LookY, LookH;
        public double Fov;     // degrees (C3)
        public double Lateral; // units: current sideways drift offset, lags toward its target (C3)
    }
}
