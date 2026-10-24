import assert from 'node:assert/strict';
import { visitor } from './visitor.mjs';
const base = process.env.API_URL ?? 'http://127.0.0.1:5080';
const a = visitor(base), b = visitor(base);
const json = async (client, path, method = 'GET') => {
  const response = await client.fetch(path, { method });
  assert.equal(response.status, 200, path); return response.json();
};
for (const path of ['/', '/judge/replay']) {
  const response = await fetch(`${base}${path}`);
  assert.equal(response.status, 200);
  const html = await response.text(); assert.match(html, /<div id="root">/); assert.doesNotMatch(html, /@vite\/client/);
  for (const asset of html.matchAll(/(?:src|href)="(\/assets\/[^" ]+)"/g)) assert.equal((await fetch(base + asset[1])).status, 200);
}
assert.equal((await fetch(`${base}/api/unknown`)).status, 404);
assert.equal((await fetch(`${base}/hubs/unknown`)).status, 404);
assert.equal((await fetch(`${base}/api/match/pause`, { method: 'POST', headers: { Origin: 'https://untrusted.example' } })).status, 403);
assert.equal((await fetch(`${base}/hubs/match/negotiate?negotiateVersion=1`, { method: 'POST', headers: { Origin: 'https://untrusted.example' } })).status, 403);
await json(a, '/api/match'); await json(b, '/api/match');
const negotiation = await json(b, '/hubs/match/negotiate?negotiateVersion=1', 'POST');
const ws = b.socket(`/hubs/match?id=${encodeURIComponent(negotiation.connectionToken)}`);
let updates = [];
ws.on('message', data => { for (const part of String(data).split('\x1e').filter(Boolean)) { const m = JSON.parse(part); if (m.target === 'MatchUpdated') updates.push(m.arguments[0]); } });
await new Promise((resolve, reject) => { ws.once('open', resolve); ws.once('error', reject); });
ws.send('{"protocol":"json","version":1}\x1e');
try {
  await json(a, '/api/match/start', 'POST');
  await new Promise(r => setTimeout(r, 1100));
  assert.equal((await json(b, '/api/match')).status, 'ready');
  assert.equal(updates.length, 0, 'visitor B receives no visitor A broadcasts');
  await json(b, '/api/match/start', 'POST');
  const paused = await json(a, '/api/match/pause', 'POST');
  await new Promise(r => setTimeout(r, 1100));
  assert.equal((await json(a, '/api/match')).frameIndex, paused.frameIndex);
  assert.ok((await json(b, '/api/match')).frameIndex > 0);
  await json(a, '/api/match/replay', 'POST');
  assert.ok((await json(b, '/api/match')).frameIndex > 0, 'replay does not reset another visitor');
  assert.ok(updates.length > 0);
  const malformed = await fetch(`${base}/api/counterfactual`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{' });
  assert.equal(malformed.status, 400); assert.doesNotMatch(await malformed.text(), /stack|exception|System\./i);
  console.log('PASS: production assets, SPA refresh, API 404, origin rejection, sanitized errors, two-visitor REST and WebSocket isolation.');
} finally { ws.close(); await json(a, '/api/match/pause', 'POST'); await json(b, '/api/match/pause', 'POST'); }

