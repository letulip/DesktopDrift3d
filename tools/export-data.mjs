// SPEC-01 step 1: exports every data table of the 2D game (DesktopDrift/js/*.js) to
// unity/Assets/Content/_json/<name>.json. Nothing is retyped: values come from the LIVE modules
// (or, for non-exported constants, from the module text). Deterministic: keys sorted recursively.
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const DD = process.env.DESKTOPDRIFT_DIR ?? '/Users/letulip/Projects/Claude/DesktopDrift';
const OUT = join(dirname(fileURLToPath(import.meta.url)), '..', 'unity/Assets/Content/_json');
mkdirSync(OUT, { recursive: true });

// config.js builds Path2D objects at module load; Node has no Path2D. Stub BEFORE the dynamic import.
globalThis.Path2D = class Path2D { constructor() {} };

const commit = execFileSync('git', ['-C', DD, 'rev-parse', 'HEAD']).toString().trim();
const SKIP_KEYS = new Set(['_p2d', '_lines', 'path', 'details', 'lines']);   // SVG geometry, not needed
const skipped = [];   // reported on stderr: class instances / non-finite numbers

const load = (rel) => import(pathToFileURL(join(DD, rel)).href);

// Plain-data projection: drops functions and SVG-geometry keys, sorts keys recursively.
const clean = (v, where) => {
  if (typeof v === 'function' || typeof v === 'symbol' || v === undefined) return undefined;
  if (v === null || typeof v === 'string' || typeof v === 'boolean') return v;
  if (typeof v === 'number') {
    if (!Number.isFinite(v)) { skipped.push(`${where}: non-finite ${v}`); return null; }
    return v;
  }
  if (Array.isArray(v)) return v.map((x, i) => { const c = clean(x, `${where}[${i}]`); return c === undefined ? null : c; });
  if (v instanceof Set) return [...v].map((x, i) => clean(x, `${where}{${i}}`));
  if (typeof v === 'object') {
    const proto = Object.getPrototypeOf(v);
    if (proto !== Object.prototype && proto !== null) { skipped.push(`${where}: instance of ${v.constructor?.name}`); return undefined; }
    const o = {};
    for (const k of Object.keys(v).sort()) {
      if (SKIP_KEYS.has(k)) continue;
      const c = clean(v[k], `${where}.${k}`);
      if (c !== undefined) o[k] = c;
    }
    return o;
  }
  return undefined;
};

const sortTop = (o) => Object.fromEntries(Object.keys(o).sort().map(k => [k, o[k]]));
const written = [];
const emit = (name, file, body) => {
  const obj = sortTop({ ...body, _source: { commit, file } });
  writeFileSync(join(OUT, `${name}.json`), JSON.stringify(obj, null, 2) + '\n');
  written.push(name);
};
// Every exported binding that is not a function/class, as plain data.
const exportsOf = (mod, where, exclude = []) => {
  const o = {};
  for (const k of Object.keys(mod).sort()) {
    if (exclude.includes(k)) continue;
    const c = clean(mod[k], `${where}:${k}`);
    if (c !== undefined) o[k] = c;
  }
  return o;
};
const moduleExport = (name, rel, exclude = []) => load(rel).then(m => emit(name, rel, exportsOf(m, rel, exclude)));

// ── config.js ────────────────────────────────────────────────────────────────
const cfg = await load('js/config.js');
emit('config', 'js/config.js', exportsOf(cfg, 'config.js', ['CARS']));

// ── cars: the engine reads car._drive; exported as `drive`. A raw `drive` that differs is kept as `driveRaw`.
const deepEq = (a, b) => JSON.stringify(clean(a, 'x')) === JSON.stringify(clean(b, 'x'));
const cars = cfg.CARS.map((c, i) => {
  const o = clean(c, `CARS[${i}]`);
  if (!c._drive) throw new Error(`CARS[${i}] (${c.id}) has no _drive`);
  const drive = clean(c._drive, `CARS[${i}]._drive`);
  delete o._drive;
  if (c.drive !== undefined && !deepEq(c.drive, c._drive)) o.driveRaw = clean(c.drive, `CARS[${i}].drive`);
  o.drive = drive;
  return sortTop(o);
});
emit('cars', 'js/config.js', { CARS: cars });

await moduleExport('car-registry', 'js/car-registry.js');
await moduleExport('car-stats', 'js/car-stats.js');
await moduleExport('items', 'js/items.js');
await moduleExport('collectibles', 'js/collectibles.js');
await moduleExport('palette', 'js/palette.js');
await moduleExport('sfx', 'js/sound-params.js');
await moduleExport('economy', 'js/economy.js');
await moduleExport('scoring', 'js/scoring.js');
await moduleExport('neon', 'js/neon.js');

const shop = await load('js/shop-catalog.js');
emit('shop', 'js/shop-catalog.js', exportsOf(shop, 'shop-catalog.js'));

// ── tracks: registry (live) + laps/tires from the track module text ──────────
const reg = await load('js/track-registry.js');
const tracks = reg.TRACKS.map((t, i) => {
  const o = clean(t, `TRACKS[${i}]`);
  const mod = `js/track-${t.id}.js`;
  const p = join(DD, mod);
  if (!existsSync(p)) throw new Error(`track module missing: ${mod}`);
  const text = readFileSync(p, 'utf8');
  const laps = text.match(/laps:\s*(\d+)/), tires = text.match(/tires:\s*(\d+)/);
  if (!laps || !tires) throw new Error(`${mod}: laps/tires not found`);
  if (t.tires !== undefined && t.tires !== Number(tires[1])) {
    throw new Error(`${t.id}: registry tires ${t.tires} != module tires ${tires[1]}`);
  }
  // full 7-key theme from the module's makeTrack block; the registry theme (4 keys) must agree with it
  const thBlock = text.match(/theme:\s*\{([\s\S]*?)\}/);
  if (!thBlock) throw new Error(`${mod}: theme block not found`);
  const modTheme = {};
  for (const m of thBlock[1].matchAll(/(\w+):\s*'([^']*)'/g)) modTheme[m[1]] = m[2];
  for (const k of Object.keys(t.theme || {})) {
    if (modTheme[k] !== t.theme[k]) throw new Error(`${t.id}: theme.${k} registry '${t.theme[k]}' != module '${modTheme[k]}'`);
  }
  o.theme = sortTop(modTheme);
  o.laps = Number(laps[1]);
  o.tires = Number(tires[1]);
  o.module = mod;
  return sortTop(o);
});
emit('tracks', 'js/track-registry.js', { TRACKS: tracks });

// ── achievements: catalog built by code from the live registries ─────────────
const ach = await load('js/achievements.js');
const content = ach.buildContent(reg.TRACKS, shop.CATALOG, cfg.CARS);
const catalog = ach.buildCatalog(content).map((e, i) => clean(e, `catalog[${i}]`));   // clean() drops `check`
emit('achievements', 'js/achievements.js', {
  catalog,
  categoryOrder: clean(ach.CATEGORY_ORDER, 'CATEGORY_ORDER'),
  content: clean(content, 'content'),
});

// ── engine: local numeric constants of game-engine.js, read from the text ────
const eng = readFileSync(join(DD, 'js/game-engine.js'), 'utf8').split('\n');
const consts = {}, lines = {}, notNumeric = [];
eng.forEach((line, i) => {
  const m = line.match(/^\s+const ([A-Z][A-Z0-9_]+)\s*=\s*([^;]+);/);
  if (!m) return;
  const expr = m[2].trim();
  if (!/^[0-9eE.\s+\-*/()]*$/.test(expr.replace(/Math\.PI/g, '3'))) { notNumeric.push(m[1]); return; }
  const val = new Function('Math', `return (${expr})`)(Math);
  if (typeof val !== 'number' || !Number.isFinite(val)) { notNumeric.push(m[1]); return; }
  consts[m[1]] = val; lines[m[1]] = i + 1;
});
emit('engine', 'js/game-engine.js', { ...consts, _lines: lines });

console.error(`exported ${written.length} files; skipped: ${skipped.length ? skipped.join('; ') : 'none'}; engine non-numeric: ${notNumeric.join(',') || 'none'}`);
