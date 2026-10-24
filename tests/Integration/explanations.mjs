// Run against Provider=Disabled; this verifies HTTP validation and fallback without Azure tokens.
import assert from 'node:assert/strict';
const base = process.env.API_URL ?? 'http://127.0.0.1:5080';
const catalogue = await (await fetch(`${base}/api/match/replay`)).json();
const insight = catalogue.insights.find(i => i.isKeyMoment);
const payload = { matchId: insight.matchId, insightId: insight.id, audience: 'casual', scope: 'replay' };
const send = body => fetch(`${base}/api/explanations`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
for (const audience of ['casual', 'analyst']) {
  const response = await send({ ...payload, audience });
  assert.equal(response.status, 200);
  const result = await response.json();
  assert.equal(result.source, 'fallback', 'Run this test only against Provider=Disabled');
  assert.equal(result.audience, audience);
  assert.deepEqual(result.narrative.evidenceEventIds, insight.supportingEventIds);
}
assert.equal((await send({ ...payload, audience: 'arbitrary prompt' })).status, 400);
assert.equal((await send({ ...payload, insightId: 'unknown' })).status, 404);
assert.equal((await send({ ...payload, prompt: 'invent stats' })).status, 400);
assert.equal((await send({ ...payload, scope: 'unknown' })).status, 400);
const statuses = await Promise.all(Array.from({ length: 15 }, async () => (await send(payload)).status));
assert.ok(statuses.includes(429), 'server rate limit is independent of button state');
assert.equal((await fetch(`${base}/api/health`)).status, 200, 'rate limit does not block playback APIs');
console.log('Explanation HTTP validation, evidence-preserving fallback and rate limit passed.');
