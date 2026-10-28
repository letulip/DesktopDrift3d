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

        // SPEC-02 §8 (C3 polish).
        public double FovMin = 55;               // deg at speed 0
        public double FovMax = 68;               // deg at MaxSpeed
        public double DriftOffset = 60;          // units: sideways shift toward the inside of the arc at full slip
        public double DriftSlipRef = 80;         // units/s: |side velocity| that gives the full DriftOffset
        public double MobileDistanceScale = 1.0 / 0.65; // Distance and Height on phones (2D ZOOM 0.65); not LookAhead
        public double IntroSeconds = 3.0;        // s: countdown fly-in from IntroScale to 1 (smoothstep)
        public double IntroScale = 2.2;          // Distance/Height multiplier at the start of the countdown
        public double OrbitRateDeg = 18;         // deg/s: orbit around the car after the finish
        public double ShakeAmp = 4;              // units: cone-hit shake amplitude; 0 = off
        public double ShakeSeconds = 0.25;       // s: cone-hit shake duration
    }
}
