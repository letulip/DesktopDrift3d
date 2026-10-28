// SPEC-01 fix-round (review И4/И5): JS reference vectors for CoreMath / Neon, computed by node itself.
//   tools/jsfixed-ref.json – Number.prototype.toFixed: [x as a 17-significant-digit string ("-0" for -0), digits, expected]
//   tools/neon-ref.json    – zoneColors(neon, t) from the REAL DesktopDrift/js/neon.js: colour strings + intensities
// Usage: node tools/js-refs.mjs   (deterministic: the "random" doubles come from a seeded LCG)
import { writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const DD = process.env.DESKTOPDRIFT_DIR ?? '/Users/letulip/Projects/Claude/DesktopDrift';
const HERE = dirname(fileURLToPath(import.meta.url));

// ── toFixed ────────────────────────────────────────────────────────────────────────────────────
let s = 12345;
const rnd = () => { s = (Math.imul(1664525, s) + 1013904223) >>> 0; return s / 4294967296; };
const xs = [2.675, 1.005, 8.345, 1.2345, -0.0005, 0.125, 0.5, 1.5, 2.5, -2.5, 1e-7, 123456.789, 0, -0];
for (let i = 0; i < 120; i++) {
  // mixed magnitude 1e-8 .. 1e9, both signs
  const x = rnd() * Math.pow(10, Math.floor(rnd() * 18) - 8) * (rnd() < 0.3 ? -1 : 1);
  xs.push(x);
}
for (let i = 0; i < 30; i++) {
  // decimal "ties" whose binary value sits just above or below the half (like 2.675)
  const d = 1 + (i % 3);
  xs.push((Math.floor(rnd() * 100000) + 0.5) / Math.pow(10, d) * (i % 4 === 0 ? -1 : 1));
}
const str = (x) => (Object.is(x, -0) ? '-0' : x.toPrecision(17));
const fixed = [];
for (const x of xs) for (const d of [0, 2, 3]) fixed.push([str(x), d, x.toFixed(d)]);
writeFileSync(join(HERE, 'jsfixed-ref.json'), '{\n"source": "node ' + process.version + ' Number.prototype.toFixed",\n"cases": [\n' +
  fixed.map((c) => JSON.stringify(c)).join(',\n') + '\n]\n}\n');

// ── neon ───────────────────────────────────────────────────────────────────────────────────────
const { zoneColors } = await import(pathToFileURL(join(DD, 'js/neon.js')).href);
const colors = ['#ff2d95', '#00e5ff', '#39FF14', '#fa0', '#7b2dff'];
const cases = [];
for (const anim of ['rainbow', 'flow', 'pulse']) {
  for (const t of [0.1, 0.1234, 7.5, 123.456]) {
    const neon = { layout: 'per-zone', anim, colors, speed: 1.3 };
    cases.push({ neon, t, zones: zoneColors(neon, t).map((z) => [z.color, z.intensity]) });
  }
}
writeFileSync(join(HERE, 'neon-ref.json'), '{\n"source": "DesktopDrift/js/neon.js zoneColors, node ' + process.version + '",\n"cases": [\n' +
  cases.map((c) => JSON.stringify(c)).join(',\n') + '\n]\n}\n');
console.log(`js-refs: ${fixed.length} toFixed cases, ${cases.length} neon cases`);
