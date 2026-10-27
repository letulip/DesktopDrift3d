# Unity 3D port – analysis and staged estimate

Status: **analysis; platform decision taken 27.10.2026.** If the port goes ahead,
1.0 is native-first (AAB for Google Play, iOS second) and the Unity Web build is
optional after 1.0 (§13.6). The go/no-go on the port itself waits for the spike
(§12). Written to answer one question: how hard and how long would it be to rebuild Desktop Drift in Unity with
low-poly 3D models, and what the stages are. Numbers are estimates in focused
person-weeks (owner + Claude), not promises; the first stage is a measured spike
that replaces the guesses with data.

## 1. Verdict

- **Difficulty: medium-high.** Nothing is research-grade. The gameplay maths
  (physics, collision, scoring, economy, achievements, save migrations) is pure data
  and formulas and ports 1:1 with the existing tests as the spec. The cost sits in
  three places: the volume of net-new 3D art, rebuilding eight screens of UI in a new
  UI system, and, only if a Web build is wanted, fitting it into the portals'
  mobile-web budget (§13: 1.0 is native-first, the Web build is optional).
- **Duration: 21–30 focused person-weeks** after the owner's rulings in §13 (the
  first cut said 22–33; table in §6). At the intensity of the summer build (May 31 –
  Aug 6, 610 commits, the 2D game from zero to Early Access) that is roughly **4–6
  months of calendar time to a 1.0 at feature parity**, about 7 months on the
  pessimistic path. At 10–15 h/week it stretches past a year and only
  works with the scope cut in §11.
- **The hidden cost is velocity.** Today's loop (Claude edits a module, `npm test`,
  browser-verify) has no equivalent in the Editor: compile + domain reload, play
  mode, Web builds of minutes, and Claude cannot see a scene without screenshots.
  Expect each feature to cost 2–3× today's wall-clock until the C# tooling
  (edit-mode tests, CLI builds, an MCP bridge) is in place.
- **Recommendation:** do not commit to the full port from this document. Run the
  3-week spike in §12 (the C# contour first, then physics parity + one grey-box track + an APK
  measured on a mid-range phone). If the goal is only "a low-poly 3D look on the existing
  portals", §10 option B (Three.js inside the current stack) delivers it in a third
  of the time and keeps everything that already works.

## 2. What exists today (inventory, main @ 2026-08-06)

| Area | Size |
|---|---|
| JavaScript | 8,046 lines in 58 modules (`js/`, `js/screens/`), zero dependencies |
| CSS / HTML | 1,937 lines across 13 stylesheets; 13 HTML files (8 screens) |
| Unit tests | 42 files, 3,660 lines (`node --test`) |
| Cars | 8 top-down SVGs → generated `cars-data.js` (silhouette + details + drive profile) |
| Moods | 88 SVGs (8 cars × 11 emotions), recoloured at load |
| Props | 75 descriptors in `items.js`, 107 SVGs; 69 distinct items placed across the tracks (134 placements) |
| Tracks | 6 Figma SVG tracks × forward/reversed + the parametric sandbox oval; per-track colour themes |
| Collectibles | tires (respawning proximity pickup), cola caps (drift-donut sweep) |
| Shop | 4 finishes, 8 trails, 14 glass tints, 14 outlines, 11 moods, 3 neon layouts + 3 animations over 6 zones; 20 body + 10 neon colours free |
| Achievements | 40 definitions (ladders scale with track count), toasts, dedicated page |
| Save | `store.js` v4, deep-merge load, 3 migrations, export/import as a code or a file |
| Sound | procedural Web Audio chimes (a data table) + a recorded drift sample; music parked (licence) |
| Platform | 5-call adapter contract, CrazyGames SDK v3 adapter with midgame ads and happytime; SW/PWA offline; portal build preset |
| Distribution | itch.io, Newgrounds, Game Jolt live (0.9.0); CrazyGames and Yandex next |
| Build | 1.4 MB initial download, 185 files |

## 3. Carry-over map: what ports, what is rebuilt, what is net-new

**Ports 1:1** (pure logic, ~2,100 lines → C#, existing tests as the spec):
physics.js (66), scoring.js (55), collision.js (152), cola.js, economy.js,
achievements.js (297), neon.js (layout/animation maths), finish.js (the presets),
car-stats.js, store.js (372, incl. migrations v2–v4), shop-catalog.js,
tire-seed.js, track-util.js (348: SVG path parsing, Chaikin, centerline queries),
reverse-track, route/opts resolution, wallet-history, share-util, sound-params (as
data).

**Rebuilt in a new form** (same behaviour, different mechanism, ~1,500 lines today):
- game-engine.js (660): the orchestrator becomes a component-driven loop with the
  same step order; HUD writes become UI events.
- track-factory.js + 6 track modules + track-registry: an Editor importer
  (SVG → TrackAsset) and a prefab per track.
- platform.js / platform-crazygames.js: a C# interface + the official CrazyGames
  Unity SDK; Yandex through their Unity guidance.
- sw.js / router / redirect-shim / sw-update: Unity's Web template (optionally the
  PWA template); the SPA shell disappears.
- sound.js: AudioSources + baked clips.
- profile-io / profile-sync: the same format; file and clipboard access through a
  jslib bridge on Web.

**Net-new** (no equivalent today):
- All 3D art: 8 cars, ~70 props, cones/tires/caps, one table mesh with 6 material
  presets, moods as windshield decals (deferred to 1.1, §13). The SVG silhouettes and
  props are references, not assets; props come from open low-poly libraries (§13).
- Camera, lighting, materials and shaders (finishes, glass, outline, neon emissive +
  ground glow, cap radial fill), the skid-mark mesh system.
- UI Toolkit implementation of 8 screens + HUD + overlays, replacing 1,476 lines of
  screen JS, 1,937 lines of CSS and 13 HTML files.
- Build, size and performance engineering for mobile Web.
- Editor tooling replacing `gen-cars`, `check-item-clearance`, `collider-tuner`,
  `sound-lab`, `car-eyes-lab` and the capture pipeline.

Rough split of the effort: logic port 10 %, rebuilds 20 %, net-new 70 %.

## 4. Target design in Unity (the decisions the estimate assumes)

- **Engine and licence.** Unity 6 (current LTS line), URP, IL2CPP; targets Web
  first, Android/iOS/desktop later. Unity Personal is free under $200k of annual
  revenue and funding, the runtime fee is gone, and the splash screen is optional on
  Unity 6 Personal. Pro is $2,310 per seat per year (2026 price) only above that
  threshold.
- **Units.** 1 game unit = 1 mm. The toy car (`len` 78) becomes ~8 cm, the sandbox
  oval ~2.5 m across: a real 1:64 toy on a real table. Every existing number
  (TRACK_HALF 100, CONE_R 9, prop capsules, CP_R) ports unchanged.
- **Physics.** Keep the custom kinematics. `stepCar` ports verbatim onto a kinematic
  Rigidbody moving in the XZ plane in FixedUpdate at 60 Hz (the maths is already
  frame-rate independent through `pow(k, dt·PHYS_HZ)`), with interpolation for
  rendering. Walls, props, cones, near-miss and checkpoints stay the pure 2D
  functions; PhysX is not used for the car. This is what makes feel parity a port
  rather than a re-tune.
- **Parity harness.** Record a scripted input trace on the JS engine, replay it in
  C#, assert positions within 1e-6 per frame. One day of work that de-risks the
  whole project.
- **Camera.** World-fixed yaw, as today (the camera translates, never rotates),
  pitched ~55–65°, following the car with the same ~10 % screen offset; mobile zooms
  out (today's 0.65 factor becomes camera distance). This keeps split-screen touch
  steering, the minimap and the readability of near-misses. A chase camera is a
  different game and a re-tune; not assumed.
- **Rendering.** Flat-shaded low-poly, one directional light, soft shadows on
  desktop and baked or none on mobile Web, bloom only where neon demands it,
  per-track colour presets derived from `track_themes.json`.
- **Cars and cosmetics as materials.** Body = URP Lit with finish presets (matte,
  metallic, chrome; pearl through a small Shader Graph iridescence). Glass tint =
  transparent material colour. Outline = inverted-hull pass. Neon = 6 emissive zone
  meshes + an additive ground-glow quad driven by the ported `zoneColors(neon, t)`.
  Trails = TrailRenderer colour. Moods = a windshield decal quad with 11 textures
  recoloured through mask channels (body / eye / outline) instead of string replace.
- **Tracks.** An Editor importer reads the same Figma SVGs (`track_path`, `ITEM_*`
  proxy lines, cap and tire lines) into a TrackAsset (ScriptableObject) and builds a
  prefab: a ribbon mesh extruded from the Chaikin-smoothed centerline with UVs along
  its length, table top and edge meshes, props instantiated from a prefab catalog
  keyed by the `items.js` ids, cones along the edges, checkpoints. Reversed = the
  same mirror transform. The collider-clearance validator becomes an edit-mode test.
- **Skid marks.** Pooled mesh-strip skids per rear wheel, alpha by slip, a hard cap
  on segments (today's batching trick has a direct equivalent).
- **Collectibles.** Tires = trigger pickup with respawn; caps = a disc with a radial
  fill shader driven by the ported sweep maths.
- **UI.** UI Toolkit. USS mirrors the `:root` tokens of `base.css`, Unbounded 800
  imported as a font asset, screens as UXML documents: menu, tracks, zen, garage (a
  3D turntable instead of canvas previews), modify, settings, achievements, donate,
  plus HUD, pause, confirm-exit, results, achievement toasts and wallet history.
- **Persistence.** JSON in `Application.persistentDataPath` (IndexedDB-backed on
  Web with an explicit sync), the same schema and migrations, and the current
  profile export code stays importable so web players carry progress into the 3D
  edition.
- **Platform layer.** The 5-call contract as a C# interface; the official CrazyGames
  Unity SDK; Yandex through their Unity guidance; jslib bridges for
  `navigator.share`, `navigator.vibrate` and the clipboard; native equivalents on
  Android/iOS.
- **Testing and agents.** All game logic in a plain C# assembly with no UnityEngine
  dependency; NUnit edit-mode tests ported from the 42 Node test files; runs from the
  CLI (`-batchmode -runTests`). Scene and prefab work through an MCP bridge for Unity
  (community packages exist) plus Unity Recorder screenshots for visual checks; the
  owner remains the eyes for feel and look.

## 5. What the port buys (why anyone would pay §6)

- Lighting, shadows and camera depth: the table-top world reads as a place, not a
  diagram; finishes finally look like finishes; neon becomes light.
- Store paths the web build cannot take: Steam (premium $3–5, or free + DLC),
  Google Play and App Store natives, consoles later; the portal web build remains.
- Native performance headroom for ghost cars, replays, photo mode, particles.
- The 2D game keeps running during the port; the 3D edition inherits its listings,
  reviews and promo playbooks.

## 6. Stages, difficulty and estimate

FTE weeks = focused person-weeks for owner + Claude together. Optimistic assumes
kit or AI-generated props and no Web surprises; realistic is the planning number.

| # | Stage | Scope | Gate (done when) | Difficulty | Opt | Real |
|---|---|---|---|---|---|---|
| 0 | Spike | Week 1 = the C# contour (§13.4): assembly split, NUnit edit-mode tests from the CLI, a CLI build script with a size/time report, the MCP bridge, screenshot capture, the JS→C# parity harness. Weeks 2–3: `stepCar` + collision port, grey-box oval, placeholder car, camera, HUD stub, an APK measured on a mid-range Android | Trace parity ≤ 1e-6; 60 fps on the phone; the contour runs end to end without the Editor GUI; go/no-go | medium | 2 | 3 |
| 1 | Core gameplay | Scoring/combo/PPS/stars, laps/checkpoints/finish, cones/near-miss/drift zones, tires/caps, modes TA/Sandbox/Zen, countdown/pause/confirm-exit/results, skids, NUnit tests for every ported module | All 42 test files' expectations green in C#; a full Time Attack race on the grey-box track | medium | 3 | 4 |
| 2 | Track pipeline | SVG importer → TrackAsset → prefab; ribbon and table meshes; prop catalog; reversed; clearance validator; 6 tracks imported grey | 12 instances playable with records, caps and tires | medium | 2 | 3 |
| 3 | Cars | Material and cosmetic system (finishes, glass, outline, neon zones + glow, trails); 8 low-poly cars modelled or adapted; garage turntable; moods deferred to 1.1 | All 8 cars with every 1.0 cosmetic applied in-game and in the garage | high (art) | 3 | 3 |
| 4 | Props and environments | ~70 placed props from open low-poly libraries, fitted automatically to the existing capsule footprints and re-palettised to one style; cones/tires/caps; one table mesh (rect or round, sized per track as today) with 6 material presets; lighting presets per theme; credits file | 6 tracks dressed; 60 fps on the stage-0 phone | medium (curation) | 3 | 4 |
| 5 | Meta UI | UI Toolkit design system + 8 screens, HUD, overlays, toasts, wallet history, achievements page, share card (RenderTexture → PNG → share) | Every screen of the 2D game has its 3D counterpart with the same copy | medium | 3 | 4 |
| 6 | Systems | Save + migrations + profile import; economy/shop/achievements wiring; platform interface + CrazyGames Unity SDK + Yandex; haptics/share/clipboard bridges; PWA template decision | A web save code imports; ads and happytime fire on the CrazyGames QA domain | medium | 2 | 3 |
| 7 | Audio | The drift sample and the music files exist as files; the chimes are a parameter table and get baked to clips once (a 50-line script); slip-driven drift pitch, settings gating; the music slot stays behind the licence decision | Sound parity with the settings toggle and volume | low | 0.5 | 0.5 |
| 8 | Store hardening (native-first, §13) | Android: ARM64 IL2CPP, 16 KB pages, target API level, a device matrix (Adreno and Mali), thermals, AAB size, Play policies and the closed-test gate; iOS later (+1–2 weeks: Xcode, Apple Developer, review). A Unity Web build is optional and comes after 1.0 (+2 weeks: size diet, Safari memory, portal frames) | 60 fps on a 2020-class mid-range Android; an AAB that passes Play's pre-launch checks | medium | 2 | 3 |
| 9 | Release | New screenshots, GIF and trailer; listings refresh (itch, Newgrounds, Game Jolt); CrazyGames and Yandex submissions; 1.0.0 | Live on the same portals as 0.9.0 | low | 1 | 2 |
| | **Total** | | | | **21.5** | **29.5** |

Dependencies: 0 → 1 → 2. Stages 3 and 4 run in parallel with 1–2 once the spike
sets the look. 5 and 6 need 1–2. 8 needs everything. 9 last.

## 7. Calendar scenarios

| Regime | Weekly effort | 1.0 at parity |
|---|---|---|
| Summer-2026 intensity (owner near full-time + agent waves) | ~40 h + agents | 4–6 months |
| Half-time | ~20 h | 9–13 months |
| Evenings and weekends | 10–15 h | 18–28 months (needs the §11 cut) |

For scale: the 2D game went from zero to Early Access in about ten weeks. The port
is 2.5–3× that: the design is done, but the art is net-new and the loop is slower.

## 8. Risks, ranked

1. **Velocity drop** (certain). Mitigation: stage 0 builds the C# test/CLI/MCP loop
   first; logic stays in plain classes; the owner takes the visual checks.
2. **Mobile-web budget** (off the critical path: 1.0 is native-first, §13; the risk
   returns only with the optional Web build). Today 1.4 MB loads in a second; a lean
   Unity Web build is 12–25 MB (an estimate, to be measured when that build is
   scheduled). CrazyGames: ≤ 50 MB initial
   download, ≤ 20 MB for mobile homepage eligibility, ≤ 250 MB total with the SDK,
   1,500 files. Yandex has its own archive limit (verify at submission; their docs
   carry a Unity optimisation guide). Each extra second of load costs portal players.
   Mitigation: Addressables, high code stripping, Brotli, no unneeded packages, one
   scene.
3. **Low-end Android and the store gates** (likely). Natively the engine has
   headroom, but thermals and Mali-class GPUs still want a poly budget. Google Play
   makes a personal developer account created after 13.11.2023 run a closed test with
   12 testers opted in for 14 consecutive days before production access (verify on
   Google's own page at submission; organisation accounts are exempt). iOS adds
   Xcode, the Apple Developer fee and review. Unity 6 covers the 16 KB page-size and
   target-API requirements. The Web-specific part (Safari 15+ with WebGL 2, memory)
   applies only to the optional Web build.
   Mitigation: a strict poly and texture budget, no real-time shadows on mobile,
   real devices from stage 0, the 2D build kept live for the long tail.
4. **Art volume** (owner's ruling: open libraries, §13.1). ~70 props + 8 cars + one
   table with 6 materials; moods deferred. The work becomes curation: finding,
   licensing (CC0 preferred, CC-BY with credits), fitting each model to its capsule
   footprint automatically, and re-palettising everything to one style. Without that
   discipline the tracks look like a flea market; that is the residual risk.
5. **Feel drift from perspective** (medium). Same maths, different perception of
   speed and slip under a pitched camera. Mitigation: the parity harness proves the
   maths; the camera gets its own tuning week; the owner signs off per car.
6. **Two products to promote** (medium). Decide early whether the 3D edition
   replaces the 2D build on the portals or ships beside it; the promo playbooks
   assume one. Poki's exclusivity question is unchanged by the engine.
7. **Token cost** (medium). Editor logs, YAML scenes and C# compile loops are
   heavier per feature than JS modules; the agent waves need the same discipline as
   today (thin agents, gates in the parent).
8. **Music** (unchanged). The licence blocker survives the port.

## 9. Money

Unity Personal $0; CC0 kits $0; Synty-class prop packs $20–60 each if used; an AI
3D generation plan ~$20–30 per month during stages 3–4; fonts OFL; Google Play $25
once; Apple Developer $99 per year if iOS ships. The cost is time.

## 10. Alternatives measured against the same inventory

| Option | What changes | Keeps | Effort | Right when |
|---|---|---|---|---|
| **A. Unity port** (this document) | everything but the formulas and the data | the design, the data tables, the tests as spec, the listings | 22–33 FTE weeks | the goal includes Steam or mobile stores, or a Unity future |
| **B. Three.js render layer inside the current stack** | `render.js` and the car/track/prop visuals become a Three.js scene in the same world coordinates; 3D art as glTF; skids as mesh strips; the garage preview becomes a small 3D canvas | physics, collision, scoring, economy, store, achievements, all 8 HTML screens, SW/PWA, the portal adapter, the SEO pages, the Node tests and the Claude browser loop; the build grows by ~0.2 MB of library plus models | 8–12 FTE weeks (4–6 tech + the same art levers) | the goal is a low-poly 3D look on the existing portals |
| **C. 2.5D pre-rendered sprites** | cars and props modelled low-poly in Blender, rendered top-down with baked lighting, the SVGs swapped for sprites (body tint through masks) | the whole engine; no perspective change | 3–5 FTE weeks | to test whether the low-poly look sells before buying B or A |
| Godot 4 | the same scope as A | – | about A | not recommended here: the web export is single-threaded on the Compatibility renderer and the portal SDKs are community-maintained |

B and C share the art with A: props and cars modelled once move to Unity later if A
is chosen after all.

## 11. If the full port is chosen: the 1.0 scope cut

Ship the 3D edition with Time Attack + Zen + Sandbox, all 6 tracks, all 8 cars,
body colours, finishes, trails, neon solid + one layout, tires and caps,
achievements, records and profile import. Defer moods, glass tints, outlines, neon
animations and the share card to 1.1. That removes about two weeks from the table and
most of the material and shader risk from the critical path.

## 12. Recommendation and first step

1. Decide the goal first: a portal look-upgrade (→ B, or C as a 3-week probe) versus
   store platforms and a Unity future (→ A).
2. In either case the first step has the same shape: a **3-week spike** with a
   measured gate. Week 1 builds the C# contour (§13.4); then a physics parity trace,
   one grey-box track, the camera scheme, an APK timed on a mid-range Android, and a
   page of numbers. Nothing in the live 2D game changes during the spike.
3. Keep the 2D promo wave (CrazyGames, Yandex) moving regardless: the 3D edition
   inherits whatever those listings earn.

Open decisions for the owner: the camera scheme (a fixed-yaw high camera is
assumed); successor versus side-by-side product; store targets for 1.0; the art
route (Blender by hand, kits, AI generation + cleanup); the parity scope for 1.0
(§11).

## 13. Owner's rulings (27.10.2026) and what they change

1. **Props come from open low-poly libraries** (Kenney and Quaternius are CC0;
   Poly Pizza aggregates CC0/CC-BY low-poly; Sketchfab with the CC filter;
   OpenGameArt). CC0 needs nothing, CC-BY goes into CREDITS.md. Two tools make this
   cheap: the track importer scales every model to the capsule footprint it already
   has in `items.js` (`hl`, `r`), so placement and collision stay identical; and a
   single palette texture re-colours models from different authors into one style.
   Stage 4 drops from 3/6 to 3/4 weeks and its difficulty from art to curation.
2. **Moods are not a show-stopper and are deferred to 1.1.** They are cheaper than
   the first cut assumed: each emotion SVG is not a full-car drawing but three paths
   in the car's own frame (window outline, window fill, eyes), i.e. a decal. With the
   3D car modelled from the same silhouette, a top-down planar projection of the
   rasterised SVG lands the eyes on the windshield with no per-file work; the
   placeholder colours become mask channels (body / eye tint / outline). About a week
   of tooling when it is scheduled, no art.
3. **One table, six looks.** The 2D engine already draws one rectangle (or an
   ellipse) sized from each track's SVG extents, in four theme colours. In 3D that is
   one table prefab, scaled per track, with six material presets (baize, brushed
   steel, worn wood, marble, laminate, oak) plus the floor colour. Days, not weeks.
4. **The C# contour is built first**, as week 1 of the spike, before any gameplay
   code: (a) two assemblies, `DesktopDrift.Core` with no UnityEngine reference and
   the Unity-side assembly; (b) NUnit edit-mode tests run from the CLI
   (`-batchmode -runTests -testPlatform EditMode`), the 42 Node test files ported as
   they are needed; (c) a CLI build script (`-executeMethod`) for Android and Web
   with a size and time report; (d) a compile-only batch run whose log is the error
   channel; (e) an MCP bridge for Unity for scene, prefab and material operations;
   (f) screenshot capture in play mode for visual checks; (g) the JS→C# parity
   harness. Done when a change goes from edit to green tests to an APK with no
   Editor GUI in the loop.
5. **Audio is mostly there.** The drift sample and the music files exist; the
   chimes are a parameter table (`sound-params.js`) and get baked to clips once.
   Stage 7 drops to half a week. The music licence blocker is unchanged.
6. **1.0 is native-first (AAB for Google Play, iOS second), the Unity Web build
   is optional and later.** Confirmed by the owner 27.10.2026 as the fixed
   decision.
   This removes the web size and Safari risks from the critical path, which is where
   Unity's advantage lies anyway. The portals keep the live 2D build; the 3D edition
   monetises through the stores (premium, or free with rewarded ads, decided later).
   New constraints: Google Play's closed-test gate for a new personal account (12
   testers for 14 consecutive days), the $25 registration, IL2CPP ARM64 and 16 KB
   page alignment (Unity 6 handles both); iOS adds the Apple Developer fee, Xcode
   and review. Android first, iOS second.

Revised totals: 21.5 optimistic / 29.5 realistic focused weeks (from 22 / 33). The
rulings mostly remove variance and risk rather than weeks: UI, the track pipeline and
the tooling still dominate.

Sources checked 27.10.2026: Google Play closed-testing requirement for personal
accounts (third-party summaries, e.g. https://extendsclass.com/blog/google-plays-closed-testing-requirement-what-developers-need-to-know-in-2026,
verify on Google's page), Unity pricing and plan thresholds
(https://unity.com/products/pricing-updates), Unity 6 Web mobile browser support
(https://unity.com/blog/engine-platform/web-runtime-updates-enhance-browser-experience,
https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-browsercompatibility.html),
CrazyGames technical requirements (https://docs.crazygames.com/requirements/technical,
https://docs.crazygames.com/sdk/unity/intro/), Yandex Games Unity guidance
(https://yandex.com/dev/games/doc/en/sdk/unity/setting-up), Godot 4 web export docs
(https://docs.godotengine.org/en/4.5/tutorials/export/exporting_for_web.html).
