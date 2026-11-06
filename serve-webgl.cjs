const http = require('http');
const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, 'Build/ExpandedWebGL');
http.createServer((req, res) => {
  const name = decodeURIComponent(new URL(req.url, 'http://127.0.0.1').pathname);
  const target = path.resolve(root, '.' + (name === '/' ? '/index.html' : name));
  if (!target.startsWith(root + path.sep)) return res.writeHead(403).end();
  fs.stat(target, (err, stat) => {
    if (err || !stat.isFile()) return res.writeHead(404).end();
    const type = ({'.html':'text/html; charset=utf-8','.js':'text/javascript','.wasm':'application/wasm','.css':'text/css','.json':'application/json'})[path.extname(target).toLowerCase()] || 'application/octet-stream';
    res.writeHead(200, {'Content-Type':type, 'Content-Length':stat.size});
    fs.createReadStream(target).pipe(res);
  });
}).listen(5196, '127.0.0.1', () => console.log('Open http://127.0.0.1:5196/ and keep this window open.'));
