// SPEC-03 step 6: exports the canvas command list of the 2D track thumbnail to tools/thumb-ref/<id>.json.
// Runs the 2D game's OWN drawThumb (DesktopDrift/js/track-thumb.js) in Node on a linkedom DOM against a FAKE canvas
// whose 2D context records every property assignment and method call. Nothing is retyped.
// Deterministic: same input -> byte-identical output (commands keep call order, one command per line).
import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { DOMParser } from 'linkedom';

const DD = process.env.DESKTOPDRIFT_DIR ?? '/Users/letulip/Projects/Claude/DesktopDrift';
const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const OUT = join(ROOT, 'tools/thumb-ref');
mkdirSync(OUT, { recursive: true });

// Browser shims (same as tools/export-tracks.mjs), installed BEFORE the dynamic import.
globalThis.Path2D = class Path2D { constructor() {} };
globalThis.DOMParser = DOMParser;
globalThis.fetch = async (p) => ({ text: async () => readFileSync(join(DD, p), 'utf8') });

const commit = execFileSync('git', ['-C', DD, 'rev-parse', 'HEAD']).toString().trim();
const { drawThumb } = await import(pathToFileURL(join(DD, 'js/track-thumb.js')).href);
const tracks = JSON.parse(readFileSync(join(ROOT, 'unity/Assets/Content/_json/tracks.json'), 'utf8')).TRACKS;

const PROPS = ['fillStyle', 'strokeStyle', 'lineWidth', 'lineCap', 'lineJoin'];
const METHODS = ['fillRect', 'beginPath', 'roundRect', 'fill', 'stroke', 'moveTo', 'lineTo', 'closePath'];

function makeRecorder(log) {
  const rec = {};
  for (const name of PROPS) {
    Object.defineProperty(rec, name, { get: () => undefined, set: (v) => { log.push(['set', name, v]); }, enumerable: true });
  }
  for (const name of METHODS) rec[name] = (...args) => { log.push([name, ...args]); };
  return rec;
}

for (const t of tracks) {
  const commands = [];
  const recorder = makeRecorder(commands);
  const canvas = { width: 260, height: 140, getContext: () => recorder };
  await drawThumb(canvas, t.svgSrc, t.theme);
  if (commands.length === 0) throw new Error(`no commands recorded for ${t.id} (drawThumb returned early)`);
  const text = '{\n' +
    `  "_source": ${JSON.stringify({ commit })},\n` +
    '  "width": 260,\n  "height": 140,\n  "commands": [\n' +
    commands.map(c => '    ' + JSON.stringify(c)).join(',\n') +
    '\n  ]\n}\n';
  writeFileSync(join(OUT, `${t.id}.json`), text);
  console.log(`${t.id}: ${commands.length} commands`);
}
