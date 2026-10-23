// Node 22.12+/24 built-ins only. Start the API on port 5080 before running.
import assert from 'node:assert/strict';
const base = process.env.API_URL ?? 'http://127.0.0.1:5080';
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
async function request(path, method = 'GET') {
  const response = await fetch(`${base}${path}`, { method });
  assert.equal(response.ok, true, `${method} ${path}: ${response.status}`);
  return response.json();
}
const health = await request('/api/health');
assert.equal(health.status, 'ok');
const moments = await request('/api/match/moments');
assert.equal(moments.length, 25);
assert.equal(new Set(moments.map(m => m.event.scenario)).size, 5);
for (const moment of moments) {
  for (const contribution of moment.contributions) assert.equal(contribution.eventId, moment.event.id);
}
assert.ok(moments.some(m => m.event.eventType === 'Interception' && m.event.teamId === 'B'));
assert.ok(moments.some(m => m.event.eventType === 'Save' && m.event.teamId === 'B'));
const catalogue = await request('/api/match/replay');
assert.equal(catalogue.events.length, 120, 'full timeline contains every frame');
assert.ok(catalogue.insights.filter(i => i.isKeyMoment).length >= 3);
for (const insight of catalogue.insights) {
  assert.equal(insight.significanceScore, insight.significanceFactors.reduce((n, f) => n + f.points, 0));
  assert.ok(insight.supportingEventIds.includes(insight.observation.id));
  assert.ok(insight.supportingEventIds.includes(insight.triggerEvent.id));
  assert.deepEqual(insight.supportingObservations.map(f => f.id), insight.supportingEventIds);
  const observation = catalogue.events.find(m => m.event.id === insight.observation.id);
  assert.deepEqual(observation.event, insight.observation, 'selection maps to exact catalogue frame');
  if (insight.primaryDefenderId && insight.previousMeasurements) {
    assert.ok(insight.observation.playerPositions.some(p => p.playerId === insight.primaryDefenderId && p.teamId !== insight.observation.teamId), 'selected defensive evidence permits hypothetical dragging');
  }
}
const negotiation = await request('/hubs/match/negotiate?negotiateVersion=1', 'POST');
const ws = new WebSocket(`${base.replace(/^http/, 'ws')}/hubs/match?id=${encodeURIComponent(negotiation.connectionToken)}`);
let handshake = false;
let messages = [];
ws.addEventListener('message', event => {
  for (const part of String(event.data).split('\x1e').filter(Boolean)) {
    const message = JSON.parse(part);
    if (!('type' in message)) { assert.equal(message.error, undefined); handshake = true; }
    if (message.type === 1 && message.target === 'MatchUpdated') messages.push(message.arguments[0]);
  }
});
async function until(condition, label) {
  const deadline = Date.now() + 5000;
  while (!condition()) { assert.ok(Date.now() < deadline, `Timed out: ${label}`); await pause(20); }
}
try {
  await until(() => ws.readyState === WebSocket.OPEN, 'websocket open');
  ws.send(JSON.stringify({ protocol: 'json', version: 1 }) + '\x1e');
  await until(() => handshake, 'SignalR handshake');
  const start = await request('/api/match/start', 'POST');
  assert.equal(start.status, 'running'); assert.equal(start.frameIndex, 0);
  await until(() => messages.some(m => m.frameIndex > 0), 'real-time frame delivery');
  const paused = await request('/api/match/pause', 'POST');
  assert.equal(paused.status, 'paused');
  await pause(700);
  const stable = await request('/api/match');
  assert.equal(stable.frameIndex, paused.frameIndex);
  assert.equal(stable.revision, paused.revision);
  const hypotheticalResponse = await fetch(`${base}/api/counterfactual`, { method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ frame: stable.event, defenderId: 'B1', position: { x: 0, y: 0 } }) });
  assert.equal(hypotheticalResponse.status, 200);
  const hypothetical = await hypotheticalResponse.json();
  assert.equal(hypothetical.eventId, stable.event.id);
  assert.deepEqual(hypothetical.actualPosition, stable.event.playerPositions.find(p => p.playerId === 'B1').position);
  assert.deepEqual(hypothetical.hypotheticalPosition, { x: 0, y: 0 });
  assert.deepEqual(await request('/api/match'), stable, 'Hypothetical analysis must not mutate playback');
  const rejected = await fetch(`${base}/api/counterfactual`, { method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ frame: stable.event, defenderId: 'A1', position: { x: 0, y: 0 } }) });
  assert.equal(rejected.status, 400);
  const resumed = await request('/api/match/resume', 'POST');
  assert.equal(resumed.status, 'running');
  await until(() => messages.some(m => m.revision > resumed.revision && m.frameIndex > paused.frameIndex), 'resumed delivery');
  const duringPlayback = await fetch(`${base}/api/counterfactual`, { method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ frame: stable.event, defenderId: 'B1', position: { x: 50, y: 30 } }) });
  assert.equal(duringPlayback.status, 200);
  const revisionBefore = (await request('/api/match')).revision;
  await until(() => messages.some(m => m.revision > revisionBefore), 'SignalR continues during hypothetical analysis');
  assert.equal((await request('/api/match')).status, 'running');
  const replay = await request('/api/match/replay', 'POST');
  assert.deepEqual(replay.event, start.event);
  assert.deepEqual(replay.contributions, start.contributions);
  await until(() => messages.some(m => m.revision === replay.revision), 'replay broadcast');
  assert.deepEqual(replay.insights, start.insights, 'replay resets live insights');
  for (const state of messages) {
    assert.equal(state.events.length, state.frameIndex + 1);
    assert.ok(state.events.every(m => m.event.timestamp <= state.event.timestamp), 'live event history excludes future');
    assert.ok(state.insights.every(i => i.endTimestamp <= state.event.timestamp && i.supportingObservations.every(f => f.timestamp <= state.event.timestamp)), 'SignalR excludes future insight evidence');
  }
  await request('/api/match/pause', 'POST');
  assert.deepEqual(await request('/api/match/insights'), (await request('/api/match')).insights);
  for (const state of messages) for (const c of state.contributions) {
    assert.equal(c.eventId, state.event.id);
    assert.ok(state.event.playerPositions.some(p => p.playerId === c.defenderId));
  }
  for (let i = 1; i < messages.length; i++) assert.ok(messages[i].revision >= messages[i - 1].revision);
  const invalid = await fetch(`${base}/api/match/invalid`, { method: 'POST' });
  assert.equal(invalid.status, 400);
  console.log('PASS: API health, SignalR handshake/delivery, start, pause stability, resume, repeatable replay, evidence links, ordered revisions, invalid command, counterfactual isolation/validation, live playback during analysis.');
} finally {
  await request('/api/match/pause', 'POST');
  ws.close();
}

