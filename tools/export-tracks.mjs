// SPEC-01 step 4b: exports the reference geometry of every track to tools/tracks-ref/<id>.json (+ <id>.rev.json).
// Runs the 2D game's OWN track modules (DesktopDrift/js/track-<id>.js -> makeTrack) in Node on a linkedom DOM.
// Nothing is retyped. Deterministic: keys sorted recursively, 2-space indent, trailing newline.
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { DOMParser } from 'linkedom';

const DD = process.env.DESKTOPDRIFT_DIR ?? '/Users/letulip/Projects/Claude/DesktopDrift';
const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const OUT = join(ROOT, 'tools/tracks-ref');
mkdirSync(OUT, { recursive: true });

// Browser shims the 2D track code needs, installed BEFORE the (dynamic, so not hoisted) imports.
globalThis.Path2D = class Path2D { constructor() {} };   // config.js builds Path2D at load
globalThis.DOMParser = DOMParser;                         // track-factory.js parses the SVG
globalThis.fetch = async (p) => ({ text: async () => readFileSync(join(DD, p), 'utf8') });   // './tracks/<id>.svg'

const commit = execFileSync('git', ['-C', DD, 'rev-parse', 'HEAD']).toString().trim();
const { reverseTrack } = await import(pathToFileURL(join(DD, 'js/track-util.js')).href);
const ids = JSON.parse(readFileSync(join(ROOT, 'unity/Assets/Content/_json/tracks.json'), 'utf8')).TRACKS.map(t => t.id);

const num = (v, where) => {
  if (typeof v !== 'number' || !Number.isFinite(v)) throw new Error(`non-finite/non-number at ${where}: ${v}`);
  return v;
};
const pt = (p, where) => ({ x: num(p.x, where + '.x'), y: num(p.y, where + '.y') });
const pick = (o, keys, where) => {
  const r = {};
  for (const k of keys) if (o[k] !== undefined) r[k] = typeof o[k] === 'number' ? num(o[k], `${where}.${k}`) : o[k];
  return r;
};

// Plain-data projection of a track object (drops image/colour fields of props and collectibles).
const project = (T, id, svgSha256) => {
  const out = {
    _source: { commit, svg: `tracks/${id}.svg`, svgSha256 },
    id: T.id, laps: T.laps, theme: T.theme,
    TRACK_HALF: T.TRACK_HALF, CONE_R: T.CONE_R, K: T.K, CP_R: T.CP_R,
    center: T.center.map((p, i) => pt(p, `center[${i}]`)),
    outer: T.outer.map((p, i) => pt(p, `outer[${i}]`)),
    inner: T.inner.map((p, i) => pt(p, `inner[${i}]`)),
    cones: T.cones.map((c, i) => pick(c, ['x', 'y', 'x0', 'y0', 'vx', 'vy', 'ang', 'spin', 'knocked', 'ci'], `cones[${i}]`)),
    TABLE: pick(T.TABLE, ['w', 'h', 'shape'], 'TABLE'),
    props: T.props.map((o, i) => pick(o, ['x', 'y', 'ang', 'hl', 'r', 'kind', '_cos', '_sin'], `props[${i}]`)),
    collectibles: T.collectibles.map((o, i) => pick(o, ['kind', 'x', 'y', 'r', 'value', 'capId', 'hl'], `collectibles[${i}]`)),
    checkpoints: T.checkpoints.map((p, i) => pt(p, `checkpoints[${i}]`)),
    startPos: pt(T.startPos, 'startPos'),
    startAngle: num(T.startAngle, 'startAngle'),
  };
  if (T.reversed) out.reversed = true;
  out.summary = {
    center: out.center.length, outer: out.outer.length, inner: out.inner.length, cones: out.cones.length,
    props: out.props.length, collectibles: out.collectibles.length, checkpoints: out.checkpoints.length,
  };
  return out;
};

const sortKeys = (v) => {
  if (Array.isArray(v)) return v.map(sortKeys);
  if (v && typeof v === 'object') return Object.fromEntries(Object.keys(v).sort().map(k => [k, sortKeys(v[k])]));
  return v;
};
const write = (name, obj) => writeFileSync(join(OUT, name), JSON.stringify(sortKeys(obj), null, 2) + '\n');

for (const id of ids) {
  const T = await import(pathToFileURL(join(DD, 'js/track-' + id + '.js')).href);
  const svgSha256 = createHash('sha256').update(readFileSync(join(DD, 'tracks', id + '.svg'))).digest('hex');
  // module namespace -> plain object (reverseTrack spreads it)
  const track = { ...T };
  write(id + '.json', project(track, id, svgSha256));
  write(id + '.rev.json', project(reverseTrack(track), id, svgSha256));
  const s = project(track, id, svgSha256).summary;
  console.error(`${id}: ${JSON.stringify(s)}`);
}
