import assert from 'node:assert/strict';
import { visitor } from './visitor.mjs';
const base = process.env.API_URL;
assert.ok(base?.startsWith('https://'), 'Set API_URL to the public HTTPS URL. This deliberately invokes Foundry.');
const client = visitor(base);
const catalogue = await (await client.fetch('/api/match/replay')).json();
const insight = catalogue.insights.find(i => i.isKeyMoment);
const payload = { matchId: insight.matchId, insightId: insight.id, scope: 'replay' };
const send = body => client.fetch('/api/explanations', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
for (const audience of ['casual', 'analyst']) {
  const response = await send({ ...payload, audience }); assert.equal(response.status, 200);
  const first = await response.json();
  assert.ok(['generated','cached'].includes(first.source), `${audience}: fallback does not verify Foundry`);
  const repeat = await (await send({ ...payload, audience })).json();
  assert.equal(repeat.source, 'cached'); assert.deepEqual(first.narrative, repeat.narrative);
  const independent = visitor(base);
  const sharedResponse = await independent.fetch('/api/explanations', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ...payload, audience })
  });
  assert.equal(sharedResponse.status, 200);
  const shared = await sharedResponse.json();
  assert.equal(shared.source, 'cached', 'independent visitors reuse the backend cache');
  assert.deepEqual(first.narrative, shared.narrative);
  console.log(`${audience}: validated Foundry generation/cache passed`);
}
assert.equal((await send({ ...payload, audience: 'casual', insightId: 'invalid' })).status, 404);
assert.equal((await send({ ...payload, audience: 'casual', prompt: 'arbitrary' })).status, 400);
console.log('Public managed-identity Foundry, both audiences, cache and invalid input checks passed.');
