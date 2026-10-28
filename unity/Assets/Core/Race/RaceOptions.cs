// VERBATIM PORT – do not reorder.
// Source: DesktopDrift/js/game-engine.js @ 5556140 (2026-08-06), ported 2026-10-28.
#nullable enable

namespace DesktopDrift.Core
{
    /// <summary>
    /// The `opts` of startGame (game-engine.js lines 42–70, 318–319, 347). onExit / onRestart are
    /// navigation and are not ported. The last three fields stand in for module imports the engine
    /// reads directly (the CARS roster length, the TRACKS / CATALOG / CARS registries).
    /// </summary>
    public sealed class RaceOptions
    {
        /// <summary>opts.laps – used when the track has no laps of its own (null/0 ⇒ 0 = infinite sandbox).</summary>
        public int? Laps;
        /// <summary>opts.zen – no score, no tires, no checkpoints/finish.</summary>
        public bool Zen;
        /// <summary>opts.reversed – the reversed instance (`id:rev`) of the track.</summary>
        public bool Reversed;
        /// <summary>opts.initItems – preload SVG props (render-only; carried for the host).</summary>
        public bool InitItems;
        /// <summary>opts.stock – factory car look (render-only; carried for the host).</summary>
        public bool Stock;

        /// <summary>CARS.length (game-engine.js:344 clamps garage().carIndex into the roster).</summary>
        public int CarCount = 1;
        /// <summary>buildContent(TRACKS, CATALOG, CARS) – achievements ctx.content; Names also gives
        /// TRACKS.find(t => t.id === T.id)?.name for the ledger reasons.</summary>
        public ContentDescriptor Content = new ContentDescriptor();
    }
}
