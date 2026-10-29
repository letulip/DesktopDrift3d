#nullable enable
using DesktopDrift.Core;

namespace DesktopDrift.Game
{
    /// <summary>
    /// Dev/test steer source (DD_STEER=auto, PlayMode race tests): the harness autopilot over the live car and the
    /// loaded track's centre line. The nearest-centre index restarts with every race (RaceLoop.OnReset).
    /// </summary>
    public sealed class AutopilotSteerSource : ISteerSource
    {
        readonly RaceLoop _loop;
        readonly Autopilot _pilot = new Autopilot();

        public AutopilotSteerSource(RaceLoop loop)
        {
            _loop = loop;
            loop.OnReset += _pilot.Reset;
        }

        public int Resolve()
        {
            CarState c = _loop.State.Car;
            return _pilot.Steer(c.X, c.Y, c.Angle, _loop.Track.Center);
        }
    }
}
