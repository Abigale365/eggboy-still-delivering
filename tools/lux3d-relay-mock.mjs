// 本机假转发服务：按 RelayGateway 约定的接口，过几秒返回保底库里的一个 GLB。不联网、不花积分。
// 真正的转发服务以后放哪还没定（工程书 §2）；到时候服务端照这两个接口实现、把密钥放在服务端即可。
//   node tools/lux3d-relay-mock.mjs [端口=8787] [延迟秒=6]
//   游戏里：Resources/Lux3D/lux3d_config.json 写 "mode":"relay","relayUrl":"http://127.0.0.1:8787"
//   POST /lux3d/text-to-3d   {prompt, style, faceCount}  → {"taskId":"..."}
//   GET  /lux3d/task/{id}                               → {"status":"running|done|failed","glbUrl":"...","error":""}
//   GET  /glb/{file}                                    → GLB 字节
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const LIB = path.join(ROOT, 'Assets', 'TideTown', 'Resources', 'Lux3DLibrary');
const port = +(process.argv[2] || 8787), delay = +(process.argv[3] || 6) * 1000;
const tasks = new Map();
const cors = { 'access-control-allow-origin': '*', 'access-control-allow-headers': 'content-type', 'access-control-allow-methods': 'GET,POST,OPTIONS' };
const json = (res, code, obj) => { res.writeHead(code, { 'content-type': 'application/json', ...cors }); res.end(JSON.stringify(obj)); };

http.createServer((req, res) => {
  const url = new URL(req.url, `http://127.0.0.1:${port}`);
  if (req.method === 'OPTIONS') { res.writeHead(204, cors); return res.end(); }
  if (req.method === 'POST' && url.pathname === '/lux3d/text-to-3d') {
    let body = '';
    req.on('data', c => body += c);
    req.on('end', () => {
      let prompt = '';
      try { prompt = JSON.parse(body).prompt || ''; } catch { return json(res, 400, { error: 'bad json' }); }
      const files = fs.readdirSync(LIB).filter(f => f.endsWith('.glb'));
      // 粗略按字面挑一个，挑不到就随便给一个
      const hit = files.find(f => /鸭/.test(prompt) && f.includes('duck')) || files.find(f => /龟/.test(prompt) && f.includes('turtle'))
        || files.find(f => /猫/.test(prompt) && f.includes('cat')) || files[Math.floor(Math.random() * files.length)];
      const id = Date.now().toString(36) + Math.random().toString(36).slice(2, 6);
      tasks.set(id, { ready: Date.now() + delay, file: hit });
      console.log('create', id, JSON.stringify(prompt), '→', hit);
      json(res, 200, { taskId: id });
    });
    return;
  }
  let m = url.pathname.match(/^\/lux3d\/task\/([\w-]+)$/);
  if (req.method === 'GET' && m) {
    const t = tasks.get(m[1]);
    if (!t) return json(res, 404, { status: 'failed', error: 'no such task' });
    if (Date.now() < t.ready) return json(res, 200, { status: 'running' });
    return json(res, 200, { status: 'done', glbUrl: `http://127.0.0.1:${port}/glb/${encodeURIComponent(t.file)}` });
  }
  m = url.pathname.match(/^\/glb\/(.+)$/);
  if (req.method === 'GET' && m) {
    const file = path.join(LIB, path.basename(decodeURIComponent(m[1])));
    if (!fs.existsSync(file)) return json(res, 404, { error: 'not found' });
    res.writeHead(200, { 'content-type': 'model/gltf-binary', ...cors });
    return fs.createReadStream(file).pipe(res);
  }
  json(res, 404, { error: 'not found' });
}).listen(port, '127.0.0.1', () => console.log(`lux3d relay mock on http://127.0.0.1:${port} delay=${delay / 1000}s`));
