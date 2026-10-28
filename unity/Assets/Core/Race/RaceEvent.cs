// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/game-engine.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable

namespace DesktopDrift.Core
{
    /// <summary>
    /// One side effect of startGame / frame() / stop() (DOM, audio, haptics, platform SDK), emitted in
    /// the order the JS performs it. Payload fields are used per kind (see RaceRules call sites):
    /// Flash: Text, Color · Haptic: Id ("cone" | "crash" | "checkpoint") · Sfx: Id ("crash" with Value =
    /// magnitude, "record", "finish", "achieve" with Value = delay ms) · Countdown: Number (3, 2, 1) ·
    /// Checkpoint: Index (passed checkpoint), Number (next one) · LapDone: Number (lapNum), Value (lap
    /// time) · ConeHit: Index (cone) · NearMiss: Number (S.nearMisses) · TireCollected / CapCollected:
    /// Index (collectible), Number (tires) · ComboBurn: Text (reason), Value (combo points lost) ·
    /// DriftStart / DriftEnd: Value (slip level) · Finished: Result.
    /// </summary>
    public sealed class RaceEvent
    {
        public enum EventKind
        {
            Flash,
            Haptic,
            Sfx,
            Countdown,
            Go,
            GameplayStart,
            GameplayStop,
            HappyMoment,
            Checkpoint,
            LapDone,
            ConeHit,
            NearMiss,
            TireCollected,
            CapCollected,
            DriftStart,
            DriftEnd,
            ComboBurn,
            Finished,
        }

        public EventKind Kind;
        public string Text = "";
        public string Color = "";
        public string Id = "";
        public int Index;
        public int Number;
        public double Value;
        public RaceResult? Result;

        public RaceEvent(EventKind kind)
        {
            Kind = kind;
        }

        public override string ToString()
        {
            return Kind + (Text.Length > 0 ? " '" + Text + "'" : "") + (Id.Length > 0 ? " #" + Id : "");
        }
    }
}
