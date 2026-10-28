namespace DesktopDrift.Core
{
    /// <summary>
    /// SPEC-02 §6 (D-08): C2 chase-camera tuning, game units (mm) and seconds.
    /// Public double fields so Unity serializes it inside a ScriptableObject and JsonUtility prints it.
    /// Pitch is not a parameter: it follows from Height/Distance and the look point.
    /// </summary>
    [System.Serializable]
    public sealed class CameraParams
    {
        public double Distance = 574;            // 7 car lengths (82)
        public double Height = 344;              // 0.6 * Distance
        public double LookAhead = 123;           // 1.5 car lengths at max speed
        public double YawLag = 0.35;             // s, exponential time constant
        public double MaxYawRateDeg = 180;       // deg/s
        public double MinSpeedForVelocityYaw = 80; // units/s: below it the heading leans to the car axis
    }
}
