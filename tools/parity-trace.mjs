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

// SPEC-01 step 7a: --scenario wall|props|cones replays the frame orchestration of
// DesktopDrift/js/game-engine.js (frame(), lines 405-440) around the LIVE pure collision functions.
const argi = process.argv.indexOf('--scenario');
const SCENARIO = argi >= 0 ? process.argv[argi + 1] : null;
if (SCENARIO !== null) {
  if (!['wall', 'props', 'cones'].includes(SCENARIO)) throw new Error(`unknown --scenario '${SCENARIO}'`);
  const { resolveWall, resolveProps, stepKnockedCone } = await import(pathToFileURL(join(DD, 'js/collision.js')).href);
  const { prepProp } = await import(pathToFileURL(join(DD, 'js/track-util.js')).href);
  const M = CARS[0];
  const CR = M.wid * 0.55;            // game-engine.js line 424
  const NOSE = M.len * 0.3;           // game-engine.js line 425
  const SFRAMES = 600;
  const CONE_R = 9;
  const steerAt = (i) => PATTERN[Math.floor(i / 20) % 10];
  const capsule = (car) => {
    const hx = Math.cos(car.angle), hy = Math.sin(car.angle), nose = NOSE;
    const bodyPts = [[car.x + hx * nose, car.y + hy * nose], [car.x, car.y], [car.x - hx * nose, car.y - hy * nose]];
    return { hx, hy, nose, bodyPts };
  };
  const J = JSON.stringify;
  const head = (inputs) =>
    '{\n' +
    `"source": ${J({ commit, car: M.id })},\n` +
    `"scenario": ${J(SCENARIO)},\n` +
    `"drive": ${J(drive)},\n` +
    `"k": ${J(k)},\n` +
    `"dt": ${J(dt)},\n` +
    `"wid": ${J(M.wid)},\n` +
    `"len": ${J(M.len)},\n` +
    `"cr": ${J(CR)},\n` +
    `"nose": ${J(NOSE)},\n` +
    `"steerPattern": ${J({ pattern: PATTERN, block: 20 })},\n` +
    `"frameCount": ${J(SFRAMES)},\n` +
    inputs;
  const framesJson = (fr) => '[\n' + fr.map((f) => J(f)).join(',\n') + '\n]';
  // The steer pattern bends the car's path away from y = 0 (y ~ 360 by x = 400), so obstacles at
  // fixed y would never be touched. Obstacles are therefore anchored to the path actually driven:
  // y = pathY(x) + offset, where pathY(x) is the car's y at the first frame with x >= the given x in
  // a run WITH the obstacles placed so far (props: placed one by one, since each hit bends the path).
  const pathYWith = (props) => {
    const car = { x: 0, y: 0, vx: 0, vy: 0, angle: 0 };
    const sm = { steerSmooth: 0, physT: 0 };
    const pts = [];
    for (let i = 0; i < SFRAMES; i++) {
      stepCar(car, sm, steerAt(i), drive, k, dt);
      if (props.length) resolveProps(car, props, CR, capsule(car).bodyPts);
      pts.push([car.x, car.y]);
    }
    return (x) => { const q = pts.find((p) => p[0] >= x); if (!q) throw new Error(`path never reaches x=${x}`); return q[1]; };
  };
  const freeY = pathYWith([]);
  let out;
  if (SCENARIO === 'wall') {
    const start = { x: 1500, y: 0, angle: 0 };
    const runs = [];
    const wallContactFrames = {};   // summary: frames with wallHit > 0, per table shape
    for (const shape of ['rect', 'round']) {
      const TABLE = { w: 3400, h: 2900, shape };
      const car = { x: start.x, y: start.y, vx: 0, vy: 0, angle: start.angle };
      const sm = { steerSmooth: 0, physT: 0 };
      const fr = [];
      for (let i = 0; i < SFRAMES; i++) {
        stepCar(car, sm, steerAt(i), drive, k, dt);
        const { hx, hy, nose, bodyPts } = capsule(car);
        const wallHit = resolveWall(car, TABLE, CR, hx, hy, nose, bodyPts);
        fr.push([car.x, car.y, car.angle, car.vx, car.vy, wallHit]);
        if (wallHit > 0) wallContactFrames[shape] = (wallContactFrames[shape] ?? 0) + 1;
      }
      runs.push(`{"table": ${J(TABLE)},\n"frames": ${framesJson(fr)}}`);
    }
    out = head(`"summary": ${J({ wallContactFrames })},\n"start": ${J(start)},\n"runs": [\n` + runs.join(',\n') + '\n]\n}\n');
  } else if (SCENARIO === 'props') {
    const propsIn = [
      { x: 400, y: 0, ang: 0, hl: 75, r: 15, kind: 'knife' },
      { x: 900, y: 60, ang: 1.3, hl: 0, r: 164, kind: 'plate' },
      { x: 1300, y: -80, ang: 0.4, hl: 88, r: 117, kind: 'board' },
      { x: 1700, y: 20, ang: 2.0, hl: 170, r: 20, kind: 'fork' },
    ];
    const props = [];
    for (const o of propsIn) {
      o.yOffset = o.y;
      o.y = pathYWith(props)(o.x) + o.yOffset;
      props.push(prepProp({ x: o.x, y: o.y, ang: o.ang, hl: o.hl, r: o.r, kind: o.kind }));
    }
    const car = { x: 0, y: 0, vx: 0, vy: 0, angle: 0 };
    const sm = { steerSmooth: 0, physT: 0 };
    const fr = [];
    const touched = new Set();   // summary: props that produced a positive crash at least once
    for (let i = 0; i < SFRAMES; i++) {
      stepCar(car, sm, steerAt(i), drive, k, dt);
      const { bodyPts } = capsule(car);
      // Shadow pass (does not touch the recorded car): the same props one at a time on a copy – the
      // sequential loop of resolveProps – to see which prop gave a positive crash.
      const shadow = { ...car };
      props.forEach((o, n) => { if (resolveProps(shadow, [o], CR, bodyPts) > 0) touched.add(n); });
      const crash = resolveProps(car, props, CR, bodyPts);
      fr.push([car.x, car.y, car.angle, car.vx, car.vy, crash]);
    }
    out = head(`"summary": ${J({ propsTouched: touched.size })},\n"anchor": "driven-path y + offset (props placed one by one)",\n"props": ${J(propsIn)},\n"frames": ${framesJson(fr)}\n}\n`);
  } else {
    const cones = [];
    for (let i = 0; i < 6; i++) {
      // Offset +-10 (not +-40): the hit radius is CR + CONE_R = 25.6, so +-40 off the path never touches.
      const x = 300 + 250 * i, y = freeY(x) + (i % 2 ? 10 : -10);
      cones.push({ x, y, x0: x, y0: y, vx: 0, vy: 0, ang: 0, spin: 0, knocked: false, ci: i });
    }
    const conesIn = cones.map((c) => ({ ...c }));
    const car = { x: 0, y: 0, vx: 0, vy: 0, angle: 0 };
    const sm = { steerSmooth: 0, physT: 0 };
    let penalty = 0;   // S.score penalty (100 per hit), recorded as a counter
    // game-engine.js lines 216-231 (hitConeAt), arithmetic verbatim; Math.random() -> 0.5.
    const hitConeAt = (c, px, py, r) => {
      if (c.knocked) return;
      const dx = px - c.x, dy = py - c.y;
      const rr = r + CONE_R;
      if (dx * dx + dy * dy >= rr * rr) return;
      c.knocked = true;
      const d = Math.hypot(dx, dy) || 1;
      c.vx = car.vx * 0.6 - (dx / d) * 80;
      c.vy = car.vy * 0.6 - (dy / d) * 80;
      c.spin = (0.5 - 0.5) * 18;
      car.vx *= 0.96; car.vy *= 0.96;
      penalty += 100;
    };
    const fAdj = dt * PHYS_HZ;
    const fr = [];
    for (let i = 0; i < SFRAMES; i++) {
      stepCar(car, sm, steerAt(i), drive, k, dt);
      const { bodyPts } = capsule(car);
      for (const c of cones) {
        for (const p of bodyPts) hitConeAt(c, p[0], p[1], CR);
        if (c.knocked) stepKnockedCone(c, [], CONE_R, dt, fAdj);
      }
      fr.push([[car.x, car.y, car.angle, car.vx, car.vy], penalty,
        cones.map((c) => [c.x, c.y, c.vx, c.vy, c.ang, c.spin, c.knocked])]);
    }
    out = head(`"summary": ${J({ conesKnocked: cones.filter((c) => c.knocked).length })},\n"anchor": "free-run y + offset",\n"coneR": ${J(CONE_R)},\n"fAdj": ${J(fAdj)},\n"random": 0.5,\n"cones": ${J(conesIn)},\n"frames": ${framesJson(fr)}\n}\n`);
  }
  const p = join(dirname(fileURLToPath(import.meta.url)), `parity-${SCENARIO}.json`);
  writeFileSync(p, out);
  console.log(`parity-trace: scenario ${SCENARIO}, ${SFRAMES} frames, source ${commit.slice(0, 7)}, cr ${CR} -> tools/parity-${SCENARIO}.json`);
  process.exit(0);
}

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
