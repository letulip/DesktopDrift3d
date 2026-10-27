// SPEC-00 A4: generates tools/parity-trace.json from the LIVE JS stepCar (DesktopDrift/js/physics.js).
// Profile and constants are read from DesktopDrift/js/config.js, never retyped.
import { execFileSync } from 'node:child_process';
import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const DD = process.env.DESKTOPDRIFT_DIR ?? '/Users/letulip/Projects/Claude/DesktopDrift';

// config.js builds Path2D objects at module load; Node has no Path2D. Stub it BEFORE the
// (dynamic, so not hoisted) import.
globalThis.Path2D = class Path2D { constructor() {} };

const { stepCar } = await import(pathToFileURL(join(DD, 'js/physics.js')).href);
const cfg = await import(pathToFileURL(join(DD, 'js/config.js')).href);
const { CARS, PHYS_HZ, GRIP_WOBBLE, STEER_WOBBLE } = cfg;

if (!CARS || CARS[0].id !== 'bismark') {
  throw new Error(`CARS[0].id must be 'bismark', got '${CARS && CARS[0] && CARS[0].id}'`);
}

const commit = execFileSync('git', ['-C', DD, 'rev-parse', 'HEAD']).toString().trim();
const drive = CARS[0]._drive;
const k = { PHYS_HZ, GRIP_WOBBLE, STEER_WOBBLE };
const dt = 1 / 60;
const FRAMES = 1200;
const PATTERN = [0, 1, 1, 0, -1, -1, 0, 1, -1, 0];

const car = { x: 0, y: 0, vx: 0, vy: 0, angle: 0 };
const sm = { steerSmooth: 0, physT: 0 };
const steer = [];
const frames = [];
for (let i = 0; i < FRAMES; i++) {
  const s = PATTERN[Math.floor(i / 20) % 10];
  steer.push(s);
  stepCar(car, sm, s, drive, k, dt);
  frames.push([car.x, car.y, car.angle, car.vx, car.vy]);
}

const J = JSON.stringify;
const out =
  '{\n' +
  `"source": ${J({ commit, car: 'bismark' })},\n` +
  `"drive": ${J(drive)},\n` +
  `"k": ${J(k)},\n` +
  `"dt": ${J(dt)},\n` +
  `"steer": ${J(steer)},\n` +
  '"frames": [\n' +
  frames.map((f) => J(f)).join(',\n') +
  '\n]\n}\n';

const outPath = join(dirname(fileURLToPath(import.meta.url)), 'parity-trace.json');
writeFileSync(outPath, out);
console.log(`parity-trace: ${FRAMES} frames, source ${commit.slice(0, 7)}, car bismark -> tools/parity-trace.json`);
