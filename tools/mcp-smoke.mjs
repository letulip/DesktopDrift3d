#!/usr/bin/env node
// Smoke test for the official Unity MCP server (unity mcp) against an OPEN editor.
// Usage: node tools/mcp-smoke.mjs [--tool <name>] [--args '<json>']
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const DEFAULT_TOOL = 'eval';
const DEFAULT_ARGS = '{"code":"return UnityEngine.Application.unityVersion;"}';

const argv = process.argv.slice(2);
const opt = (n, d) => { const i = argv.indexOf(n); return i >= 0 && argv[i + 1] !== undefined ? argv[i + 1] : d; };
const tool = opt('--tool', DEFAULT_TOOL);
const toolArgs = JSON.parse(opt('--args', DEFAULT_ARGS));
const projectAbsPath = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'unity');

const child = spawn('sh', ['-c', '. "$HOME/.unity/env" && exec unity mcp --project-path "$1"', 'sh', projectAbsPath],
  { stdio: ['pipe', 'pipe', 'pipe'] });

let errBuf = '';
child.stderr.on('data', (d) => {
  errBuf += d.toString();
  let i;
  while ((i = errBuf.indexOf('\n')) >= 0) { process.stderr.write('[unity mcp] ' + errBuf.slice(0, i) + '\n'); errBuf = errBuf.slice(i + 1); }
});

const pending = new Map();
let nextId = 1;
let outBuf = '';
child.stdout.on('data', (d) => {
  outBuf += d.toString();
  let i;
  while ((i = outBuf.indexOf('\n')) >= 0) {
    const line = outBuf.slice(0, i).trim(); outBuf = outBuf.slice(i + 1);
    if (!line) continue;
    let msg; try { msg = JSON.parse(line); } catch { continue; }
    if (msg.id !== undefined && pending.has(msg.id)) { const p = pending.get(msg.id); pending.delete(msg.id); clearTimeout(p.t); p.resolve(msg); }
  }
});
child.on('exit', () => { for (const [, p] of pending) { clearTimeout(p.t); p.reject(new Error('server exited')); } pending.clear(); });

const send = (o) => child.stdin.write(JSON.stringify(o) + '\n');
const request = (method, params) => new Promise((resolve, reject) => {
  const id = nextId++;
  const t = setTimeout(() => { pending.delete(id); reject(new Error(`timeout 60s on ${method}`)); }, 60000);
  pending.set(id, { resolve, reject, t });
  send({ jsonrpc: '2.0', id, method, params });
});

let code = 1;
try {
  const init = await request('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'desktopdrift-mcp-smoke', version: '0.1.0' } });
  if (init.error) throw new Error('initialize: ' + JSON.stringify(init.error));
  const r = init.result;
  console.log(`MCP_SERVER=${r.serverInfo?.name} ${r.serverInfo?.version} protocol=${r.protocolVersion}`);
  send({ jsonrpc: '2.0', method: 'notifications/initialized' });

  const list = await request('tools/list', {});
  if (list.error) throw new Error('tools/list: ' + JSON.stringify(list.error));
  const tools = list.result?.tools ?? [];
  console.log(`MCP_TOOLS=${tools.length}`);
  console.log(tools.slice(0, 15).map((t) => t.name).join(', '));
  if (tools.length === 0) throw new Error('no tools');

  const call = await request('tools/call', { name: tool, arguments: toolArgs });
  if (call.error) { console.log('MCP_CALL_ERROR ' + JSON.stringify(call.error).slice(0, 300)); }
  else {
    const text = (call.result?.content ?? []).map((c) => c.text ?? '').join('\n').slice(0, 300);
    if (call.result?.isError) console.log(`MCP_CALL_ERROR ${tool} ${text}`);
    else { console.log(`MCP_CALL_OK ${tool} ${text}`); code = 0; }
  }
} catch (e) {
  console.log('MCP_FAIL ' + e.message);
} finally {
  child.kill();
  console.log(`MCP_EXIT=${code}`);
  process.exit(code);
}
