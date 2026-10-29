// SPEC-03 step 5: exports the 2D game's item-clearance verdict of every track to tools/clearance-ref/<id>.json.
// Runs the 2D game's OWN script (DesktopDrift/tools/check-item-clearance.js) as a child process, cwd = the 2D repo,
// and records its stdout (split into lines, emoji kept) and exit code. Nothing is retyped.
// A non-zero exit code is data, not an error (workbench fails in the 2D game itself: ITEM_COMB overlaps the track).
// Deterministic: sorted keys, 2-space indent, trailing newline.
import { execFileSync, spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const DD = process.env.DESKTOPDRIFT_DIR ?? '/Users/letulip/Projects/Claude/DesktopDrift';
const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const OUT = join(ROOT, 'tools/clearance-ref');
mkdirSync(OUT, { recursive: true });

const commit = execFileSync('git', ['-C', DD, 'rev-parse', 'HEAD']).toString().trim();
const ids = JSON.parse(readFileSync(join(ROOT, 'unity/Assets/Content/_json/tracks.json'), 'utf8')).TRACKS.map(t => t.id);

for (const id of ids) {
  const r = spawnSync(process.execPath, [join(DD, 'tools/check-item-clearance.js'), join(DD, 'tracks', id + '.svg')],
    { cwd: DD, encoding: 'utf8' });
  if (r.error) throw r.error;
  if (r.status === null) throw new Error(`${id}: script terminated by signal ${r.signal}`);
  const out = r.stdout.endsWith('\n') ? r.stdout.slice(0, -1) : r.stdout;
  const doc = { _source: { commit, svg: `tracks/${id}.svg` }, exitCode: r.status, lines: out.split('\n') };
  writeFileSync(join(OUT, id + '.json'), JSON.stringify(doc, null, 2) + '\n');
  console.log(`${id}: exit ${r.status}, ${doc.lines.length} stdout lines`);
}
