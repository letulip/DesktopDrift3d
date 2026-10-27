namespace DesktopDrift.Core
{
    /// <summary>Per-game motion state touched by the step (JS: `sm`, the game state S).</summary>
    public sealed class StepMotion
    {
        public double SteerSmooth;
        public double PhysT;
    }
}
