namespace DesktopDrift.Core
{
    /// <summary>SPEC-02 §8 (C3): race phase as seen by the chase camera; supplied by the Game layer.</summary>
    public struct CamPhase
    {
        public double Intro01;       // 0 = countdown start, 1 = fly-in done
        public bool Orbit;           // after the finish: circle the car
        public double DistanceScale; // 1 on desktop, CameraParams.MobileDistanceScale on phones

        public static CamPhase Normal => new CamPhase { Intro01 = 1, Orbit = false, DistanceScale = 1 };
    }
}
