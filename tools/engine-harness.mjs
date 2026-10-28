// SPEC-01 step 7b: runs the REAL race engine of the 2D game (DesktopDrift/js/game-engine.js ->
// startGame) headlessly in Node with a scripted autopilot and writes a per-frame trace of the whole
// race to tools/engine-trace-<track>.json. Nothing of the engine is re-implemented: the harness only
// provides browser shims, a fixed 60 Hz clock, a seeded Math.random and the steering keys.
//
//   node tools/engine-harness.mjs oval          # sandbox oval, laps 2, frame cap 3600
//   node tools/engine-harness.mjs green-study   # registered track, its own laps, frame cap 14400
//
// One track per process: store.js caches the save at module level, so each race starts from the
// same seeded localStorage.
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { DOMParser, parseHTML } from 'linkedom';

const DD = process.env.DESKTOPDRIFT_DIR ?? '/Users/letulip/Projects/Claude/DesktopDrift';
const TRACK = process.argv[2];
const RUNS = {
  oval: { module: 'js/track-oval.js', opts: { laps: 2, stock: true, initItems: false }, cap: 60 * 60 },
  'green-study': { module: 'js/track-green-study.js', opts: { initItems: true }, cap: 240 * 60 },
};
if (!RUNS[TRACK]) throw new Error(`usage: engine-harness.mjs ${Object.keys(RUNS).join('|')}`);
const RUN = RUNS[TRACK];
const SEED = 1;
const LOOKAHEAD = 12;
const DEAD_BAND = 0.05;               // rad
const FRAME_MS = 1000 / 60;           // (1000 / 60) / 1000 === 1 / 60 exactly in IEEE doubles
const DATE_BASE = Date.UTC(2026, 0, 1, 12, 0, 0);   // Date.now() = DATE_BASE + simulated ms (TZ=UTC)

// ─── Deterministic environment ────────────────────────────────────────────────────────────────
process.env.TZ = 'UTC';               // awardAchievements reads new Date().getHours()
let simNow = 0;                       // simulated ms since startGame
{
  // Numerical Recipes LCG, 32-bit, seed 1.
  let s = SEED >>> 0;
  Math.random = () => { s = (Math.imul(1664525, s) + 1013904223) >>> 0; return s / 4294967296; };
  const RealDate = Date;
  class SimDate extends RealDate {
    constructor(...a) { if (a.length) super(...a); else super(DATE_BASE + simNow); }
    static now() { return DATE_BASE + simNow; }
  }
  globalThis.Date = SimDate;
  Object.defineProperty(performance, 'now', { value: () => simNow, configurable: true });
}

// ─── DOM: index.html parsed by linkedom, <template id="tpl-game"> mounted into <body> ────────
const html = readFileSync(join(DD, 'index.html'), 'utf8');
const { window, document } = parseHTML(html);
const tpl = document.getElementById('tpl-game');
document.body.appendChild((tpl.content ?? tpl).cloneNode(true));
for (const k of ['HTMLElement', 'HTMLCanvasElement', 'HTMLImageElement', 'Element', 'Node', 'Event', 'CustomEvent', 'KeyboardEvent']) {
  if (window[k] && !globalThis[k]) globalThis[k] = window[k];
}
globalThis.window = window;
globalThis.document = document;
globalThis.DOMParser = DOMParser;
globalThis.Path2D = class Path2D { constructor() {} addPath() {} moveTo() {} lineTo() {} closePath() {} arc() {} rect() {} bezierCurveTo() {} quadraticCurveTo() {} };
window.innerWidth = 1280; window.innerHeight = 720; window.devicePixelRatio = 1;
if (typeof window.addEventListener !== 'function') { window.addEventListener = () => {}; window.removeEventListener = () => {}; }
// Bare-global window members (confirm-exit.js calls addEventListener('keydown', ...) unqualified).
for (const k of ['addEventListener', 'removeEventListener', 'dispatchEvent']) globalThis[k] = window[k].bind(window);

// Canvas 2D context: a callable Proxy that swallows every call and property set.
const sink = new Proxy(function () {}, {
  get: (t, p) => (p === Symbol.toPrimitive ? () => 0 : p === 'then' ? undefined : sink),
  set: () => true,
  apply: () => sink,
  construct: () => sink,
});
const canvasProto = (window.HTMLCanvasElement ?? globalThis.HTMLCanvasElement)?.prototype;
if (canvasProto) Object.defineProperty(canvasProto, 'getContext', { value: () => sink, configurable: true, writable: true });

// localStorage: the Map stub of DesktopDrift/tests/helpers.js, seeded with sound + haptics off.
const SAVE_SEED = { version: 4, settings: { units: 'kmh', haptics: false, soundEnabled: false, volume: 1.0 } };
const { installLocalStorage } = await import(pathToFileURL(join(DD, 'tests/helpers.js')).href);
installLocalStorage({ 'desktop-drift': JSON.stringify(SAVE_SEED) });
window.localStorage = globalThis.localStorage;

Object.defineProperty(globalThis, 'navigator', { value: { vibrate() { return true; }, userAgent: 'node' }, configurable: true, writable: true });
window.navigator = globalThis.navigator;
globalThis.Image = class Image { constructor() { this.complete = false; this.naturalWidth = 0; this.naturalHeight = 0; this._src = ''; } set src(v) { this._src = v; } get src() { return this._src; } addEventListener() {} removeEventListener() {} decode() { return new Promise(() => {}); } };
globalThis.fetch = async (u) => {
  const s = String(u);
  const p = s.startsWith('file:') ? fileURLToPath(s) : join(DD, s.replace(/^\.\//, ''));
  if (!existsSync(p)) return { ok: false, status: 404, text: async () => '', json: async () => null, arrayBuffer: async () => new ArrayBuffer(0) };
  const buf = readFileSync(p);
  return { ok: true, status: 200, text: async () => buf.toString('utf8'), json: async () => JSON.parse(buf.toString('utf8')),
    arrayBuffer: async () => buf.buffer.slice(buf.byteOffset, buf.byteOffset + buf.byteLength) };
};

// rAF queue: every pending callback runs once per tick, in id order (ach-toast also uses rAF).
const rafQ = new Map();
let rafId = 0;
globalThis.requestAnimationFrame = window.requestAnimationFrame = (cb) => { rafQ.set(++rafId, cb); return rafId; };
globalThis.cancelAnimationFrame = window.cancelAnimationFrame = (id) => { rafQ.delete(id); };

// The engine computes dt = (now - last) / 1000 with last = the previous `now`. Accumulated
// simNow (k * 1000/60) makes that difference wobble in the last bits, so the engine frame gets a
// Tick whose valueOf is FRAME_MS when read as `now` and 0 when read again as `last` – dt is then
// (1000/60 - 0) / 1000 === 1/60 exactly, every frame, like the C# FixedUpdate.
class Tick { constructor() { this.read = false; } valueOf() { if (this.read) return 0; this.read = true; return FRAME_MS; } }

// ─── Engine ───────────────────────────────────────────────────────────────────────────────────
const imp = (rel) => import(pathToFileURL(join(DD, rel)).href);
const { startGame } = await imp('js/game-engine.js');
const { car, S, keys, pointers } = await imp('js/state.js');
const { resolveSteer } = await imp('js/input.js');
const { nearestCenter } = await imp('js/track-util.js');
const store = await imp('js/store.js');
const T = await imp(RUN.module);

const commit = execFileSync('git', ['-C', DD, 'rev-parse', 'HEAD']).toString().trim();
const walletStart = store.wallet();
const opts = { ...RUN.opts, zen: false, reversed: false, onExit: () => {}, onRestart: () => {} };
const rafBefore = rafId;
startGame(T, opts);
let engineFrame = null;                       // the engine's frame(): the last rAF startGame queued
for (const [id, cb] of rafQ) if (id > rafBefore) engineFrame = cb;
if (!engineFrame) throw new Error('startGame queued no animation frame');

const overlay = () => document.getElementById('raceResultsOverlay');
const finished = () => { const o = overlay(); return !!o && o.classList.contains('show'); };

const S_FIELDS = ['score', 'comboPoints', 'mult', 'multBuild', 'transitions', 'nearMisses', 'nearMissCd', 'crashCd',
  'driftTime', 'driftGrace', 'lastSlipSign', 'lapNum', 'lapTime', 'nextCp', 'startCd', 'goT', 'bestLap', 'lastLap',
  'physT', 'steerSmooth'];
const capIdx = Object.keys(S.caps).map(Number).sort((a, b) => a - b);
const frames = [];
const events = [];
let prevFlash = S.flashMsg, prevFlashT = S.flashT;
let nearIdx = 0;
let frame = 0;
const W = window.innerWidth;

while (frame < RUN.cap && !finished()) {
  // Autopilot: steer toward the centerline point LOOKAHEAD indices past the nearest one.
  const nc = nearestCenter(car.x, car.y, T.center, nearIdx);
  nearIdx = nc.idx;
  const tgt = T.center[(nearIdx + LOOKAHEAD) % T.center.length];
  const hx = Math.cos(car.angle), hy = Math.sin(car.angle);
  const tx = tgt.x - car.x, ty = tgt.y - car.y;
  const err = Math.atan2(hx * ty - hy * tx, hx * tx + hy * ty);
  keys['ArrowLeft'] = err < -DEAD_BAND;
  keys['ArrowRight'] = err > DEAD_BAND;
  const steer = resolveSteer(pointers, keys, W);

  simNow += FRAME_MS;
  const pending = [...rafQ.entries()];
  rafQ.clear();
  for (const [, cb] of pending) cb(cb === engineFrame ? new Tick() : simNow);
  frame++;

  const caps = capIdx.map((i) => [S.caps[i].sweep, S.caps[i].collected]);
  frames.push([steer, car.x, car.y, car.angle, car.vx, car.vy, ...S_FIELDS.map((k) => S[k]),
    T.cones.filter((c) => c.knocked).length, store.wallet(), caps]);
  if (S.flashMsg !== prevFlash || S.flashT > prevFlashT) events.push([frame, S.flashMsg]);
  prevFlash = S.flashMsg; prevFlashT = S.flashT;
}

const isFinished = finished();
const final = {
  frames: frame,
  finished: isFinished,
  lapNum: S.lapNum,
  score: Math.round(S.score),
  lapScores: S.lapScores.map((l) => ({ ...l })),
  bestLap: S.bestLap,
  record: T.id ? (store.records()[T.id] ?? null) : null,
  walletStart,
  walletEnd: store.wallet(),
  walletDelta: store.wallet() - walletStart,
  ledger: store.ledger().map((e) => ({ ...e })),
  achievements: [...store.achUnlocked()].sort(),
  conesKnocked: T.cones.filter((c) => c.knocked).length,
  capsCollected: capIdx.filter((i) => S.caps[i].collected).length,
};

const J = JSON.stringify;
const out =
  '{\n' +
  `"source": ${J({ commit, track: TRACK, module: RUN.module, laps: T.laps ?? RUN.opts.laps, seed: SEED, rng: 'LCG 1664525/1013904223 mod 2^32' })},\n` +
  `"opts": ${J({ laps: RUN.opts.laps ?? null, stock: !!RUN.opts.stock, initItems: !!RUN.opts.initItems, zen: false, reversed: false })},\n` +
  `"saveSeed": ${J(SAVE_SEED)},\n` +
  `"clock": ${J({ frameMs: FRAME_MS, dateBase: DATE_BASE, tz: 'UTC' })},\n` +
  `"dt": ${J(1 / 60)},\n` +
  `"lookahead": ${J(LOOKAHEAD)},\n` +
  `"deadBand": ${J(DEAD_BAND)},\n` +
  `"frameCap": ${J(RUN.cap)},\n` +
  `"frameFields": ${J(['steer', 'x', 'y', 'angle', 'vx', 'vy', ...S_FIELDS, 'conesKnocked', 'wallet', 'caps[sweep,collected]'])},\n` +
  '"frames": [\n' + frames.map((f) => J(f)).join(',\n') + '\n],\n' +
  '"events": [\n' + events.map((e) => J(e)).join(',\n') + '\n],\n' +
  `"final": ${J(final)}\n}\n`;
const outPath = join(dirname(fileURLToPath(import.meta.url)), `engine-trace-${TRACK}.json`);
writeFileSync(outPath, out);
console.log(`engine-harness: ${TRACK} frames ${frame} finished ${isFinished} lapNum ${S.lapNum} score ${final.score} ` +
  `walletDelta ${final.walletDelta} events ${events.length} -> tools/engine-trace-${TRACK}.json`);
process.exit(0);   // ach-toast / results timers would otherwise keep Node alive
