namespace DesktopDrift.Core
{
    /// <summary>Kinematics snapshot of one step (JS: { drifting, speed, vS, fwd, side }).</summary>
    public readonly struct StepResult
    {
        public readonly bool Drifting;
        public readonly double Speed;
        public readonly double VS;
        public readonly Vec2 Fwd;
        public readonly Vec2 Side;

        public StepResult(bool drifting, double speed, double vS, Vec2 fwd, Vec2 side)
        {
            Drifting = drifting;
            Speed = speed;
            VS = vS;
            Fwd = fwd;
            Side = side;
        }
    }
}
