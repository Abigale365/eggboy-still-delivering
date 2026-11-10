// Lux3D 保底库预生成：照 tools/lux3d_pregen_manifest.json 逐件文生 3D，下载 GLB 到 tools/lux3d_out/。
// 每次下单前先在 docs/lux3d-ledger.md 记一行，账本满 30 行就拒绝（用户授权的上限）。
//   node tools/lux3d_pregen.mjs --check            只验 key，不花积分
//   node tools/lux3d_pregen.mjs --dry              只列出会生成哪些，不花积分
//   node tools/lux3d_pregen.mjs --go               生成清单里还没有产物的全部条目
//   node tools/lux3d_pregen.mjs --go bone cake     只生成指定 id
//   node tools/lux3d_pregen.mjs --resume bone:123  不重新下单，接着等已有任务（不花积分）
// key：环境变量 AHOLO_API_KEY，或 AHOLO_ENV 指向的 .env，默认 ~/Documents/say-and-slay/lux3d-test/.env
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const LEDGER = path.join(ROOT, 'docs', 'lux3d-ledger.md');
const MANIFEST = path.join(ROOT, 'tools', 'lux3d_pregen_manifest.json');
const OUT = path.join(ROOT, 'tools', 'lux3d_out');
const GATEWAY = 'https://api.aholo3d.cn';
const BUDGET = 30;
const POLL_MS = 3000, TIMEOUT_MS = 6 * 60 * 1000, MAX_PARALLEL = 4;

const args = process.argv.slice(2);
const has = f => args.includes(f);
const ids = args.filter(a => !a.startsWith('--') && !a.includes(':'));
const resume = Object.fromEntries(args.filter(a => /^[\w-]+:\w+$/.test(a)).map(a => a.split(':')));

function loadKey() {
  if (process.env.AHOLO_API_KEY) return process.env.AHOLO_API_KEY;
  const envPath = process.env.AHOLO_ENV || path.join(os.homedir(), 'Documents', 'say-and-slay', 'lux3d-test', '.env');
  if (!fs.existsSync(envPath)) return null;
  for (const line of fs.readFileSync(envPath, 'utf8').split(/\r?\n/)) {
    const m = line.match(/^\s*AHOLO_API_KEY\s*=\s*(.+?)\s*$/);
    if (m) return m[1].replace(/^["']|["']$/g, '');
  }
  return null;
}
const KEY = loadKey();
const sleep = ms => new Promise(r => setTimeout(r, ms));

async function api(method, url, body) {
  const res = await fetch(GATEWAY + url, {
    method,
    headers: { authorization: KEY, ...(body ? { 'content-type': 'application/json' } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  let json;
  try { json = JSON.parse(text); } catch { throw new Error(`${method} ${url} → HTTP ${res.status}，不是 JSON：${text.slice(0, 300)}`); }
  if (!res.ok) throw new Error(`${method} ${url} → HTTP ${res.status}：${text.slice(0, 400)}`);
  return json;
}

const ledgerRows = () => fs.readFileSync(LEDGER, 'utf8').split(/\r?\n/).filter(l => /^\|\s*\d+\s*\|/.test(l));
function ledgerAppend(purpose, prompt) {
  const n = ledgerRows().length + 1;
  if (n > BUDGET) throw new Error(`账本已有 ${n - 1} 次，达到上限 ${BUDGET}，停下来问用户。`);
  const t = new Date(Date.now() + 8 * 3600e3).toISOString().replace('T', ' ').slice(0, 16);
  fs.appendFileSync(LEDGER, `| ${n} | ${t} | text-to-3d G1-Turbo | ${purpose} | ${prompt.replace(/\|/g, '/')} | PENDING-${n} | 已下单 |\n`);
  return n;
}
function ledgerUpdate(n, taskId, result) {
  const txt = fs.readFileSync(LEDGER, 'utf8').replace(`| PENDING-${n} | 已下单 |`, `| ${taskId} | ${result} |`);
  fs.writeFileSync(LEDGER, txt);
}

// 在返回体里找 .glb 链接（字段名各版本不一样，直接扫字符串最稳）
function findGlbUrl(obj) {
  let hit = null;
  (function walk(v) {
    if (hit || v == null) return;
    if (typeof v === 'string') { if (/^https?:\/\/\S+\.glb(\?|$)/i.test(v)) hit = v; return; }
    if (typeof v === 'object') for (const k of Object.keys(v)) walk(v[k]);
  })(obj);
  return hit;
}

async function waitAndDownload(item, taskId) {
  const t0 = Date.now();
  while (Date.now() - t0 < TIMEOUT_MS) {
    await sleep(POLL_MS);
    const r = await api('GET', '/lux3d/v1/generate/task/get?taskid=' + encodeURIComponent(taskId));
    const d = r.d ?? r.data ?? r;
    const status = d.status ?? d.taskStatus;
    if (status === 4) throw new Error('任务失败：' + JSON.stringify(d).slice(0, 300));
    if (status === 3) {
      const url = findGlbUrl(d);
      if (!url) throw new Error('成功但没找到 glb 链接：' + JSON.stringify(d).slice(0, 400));
      const buf = Buffer.from(await (await fetch(url)).arrayBuffer());
      fs.mkdirSync(OUT, { recursive: true });
      const file = path.join(OUT, item.id + '.glb');
      fs.writeFileSync(file, buf);
      return { file, bytes: buf.length, secs: ((Date.now() - t0) / 1000).toFixed(0) };
    }
  }
  throw new Error('等待超时，可用 --resume ' + item.id + ':' + taskId + ' 接着等');
}

async function run(item) {
  const log = (...m) => console.log(`[${item.id}]`, ...m);
  let taskId = resume[item.id], row = null;
  try {
    if (!taskId) {
      row = ledgerAppend('保底库 ' + item.id, item.prompt);
      const body = { prompt: item.prompt, style: item.style || 'cartoon', version: 'G1-Turbo',
        faceCount: item.faceCount || 20000, outputFormat: ['glb'], enablePbr: true };
      const r = await api('POST', '/lux3d/v1/generate/text-to-3d/task/create', body);
      const d = r.d ?? r.data ?? r;
      taskId = String(d.taskId ?? d.taskid ?? d.id ?? d);
      log('已下单', taskId);
      ledgerUpdate(row, taskId, '生成中');
      row = { n: row, taskId };
    }
    const res = await waitAndDownload(item, taskId);
    log(`完成 ${res.secs}s ${(res.bytes / 1e6).toFixed(1)}MB → ${res.file}`);
    if (row) fs.writeFileSync(LEDGER, fs.readFileSync(LEDGER, 'utf8').replace(`| ${row.taskId} | 生成中 |`, `| ${row.taskId} | 成功 ${res.secs}s ${(res.bytes / 1e6).toFixed(1)}MB |`));
  } catch (e) {
    log('失败：', e.message);
    if (row && row.taskId) fs.writeFileSync(LEDGER, fs.readFileSync(LEDGER, 'utf8').replace(`| ${row.taskId} | 生成中 |`, `| ${row.taskId} | 失败：${e.message.slice(0, 60).replace(/\|/g, '/')} |`));
    else if (row) ledgerUpdate(row, '-', '下单失败：' + e.message.slice(0, 60).replace(/\|/g, '/'));
  }
}

if (!KEY) { console.error('没找到 AHOLO_API_KEY'); process.exit(1); }
if (has('--check')) {
  const res = await fetch(GATEWAY + '/lux3d/v1/generate/task/list?page=1&pageSize=1', { headers: { authorization: KEY } });
  console.log('HTTP', res.status, (await res.text()).slice(0, 200));
  process.exit(0);
}
const manifest = JSON.parse(fs.readFileSync(MANIFEST, 'utf8')).items;
// 只给了 --resume、没加 --go 时，只接着等指定的旧任务，绝不顺手下新单（2026-11-10 因此多花过 1 次积分）
const resumeOnly = Object.keys(resume).length > 0 && !has('--go');
let todo = manifest.filter(i => resume[i.id] || (!resumeOnly && (ids.length ? ids.includes(i.id) : true)));
todo = todo.filter(i => resume[i.id] || !fs.existsSync(path.join(OUT, i.id + '.glb')));
console.log(`账本已用 ${ledgerRows().length}/${BUDGET}；这次要生成 ${todo.filter(i => !resume[i.id]).length} 件：`, todo.map(i => i.id).join(', '));
if (has('--dry') || (!has('--go') && !Object.keys(resume).length)) { console.log('（没加 --go，不花积分）'); process.exit(0); }
for (let i = 0; i < todo.length; i += MAX_PARALLEL) await Promise.all(todo.slice(i, i + MAX_PARALLEL).map(run));
console.log(`结束。账本 ${ledgerRows().length}/${BUDGET}`);
