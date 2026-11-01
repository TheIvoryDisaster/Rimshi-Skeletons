// Renders scene.js for every rotation and pass into an output folder (default /tmp/spireart/out).
// Usage: node tools/art/spire/render.js [outDir]; then python3 tools/art/spire/finish.py <outDir>.
const http = require('http');
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const here = __dirname;
const threeDir = process.env.THREE_DIR || '/tmp/spireart/node_modules/three/build';
const outDir = process.argv[2] || '/tmp/spireart/out';
fs.mkdirSync(outDir, { recursive: true });

const server = http.createServer((req, res) => {
  const name = path.basename(req.url.split('?')[0]);
  const file = name.startsWith('three.') ? path.join(threeDir, name) : path.join(here, name);
  if (!fs.existsSync(file)) { res.writeHead(404); res.end(); return; }
  res.writeHead(200, { 'Content-Type': file.endsWith('.html') ? 'text/html' : 'text/javascript' });
  res.end(fs.readFileSync(file));
});

server.listen(0, async () => {
  const port = server.address().port;
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
  const page = await browser.newPage();
  page.on('console', m => console.log('page:', m.text()));
  page.on('pageerror', e => console.log('page error:', e.message));
  await page.goto(`http://localhost:${port}/page.html?w=1224&h=2016`);
  await page.waitForFunction('window.ready === true', null, { timeout: 30000 });
  const images = await page.evaluate(() => window.renderAll());
  for (const [name, url] of Object.entries(images)) {
    fs.writeFileSync(path.join(outDir, name + '.png'), Buffer.from(url.split(',')[1], 'base64'));
  }
  console.log('wrote', Object.keys(images).length, 'passes to', outDir);
  await browser.close();
  server.close();
});
