import { visitor } from './visitor.mjs';
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { readFile } from 'node:fs/promises';
import ts from '../../src/web/node_modules/typescript/lib/typescript.js';

// Render the actual App JSX and execute its handlers with deterministic hooks and SignalR delivery.
// No DOM layout is needed: inspect the circle coordinates passed to React's SVG renderer.
const encode = source => `data:text/javascript;base64,${Buffer.from(source).toString('base64')}`;
const compile = source => ts.transpileModule(source, { compilerOptions: {
  target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.React,
  jsxFactory: 'globalThis.__pitchTest.element', jsxFragmentFactory: 'globalThis.__pitchTest.fragment',
} }).outputText;
const reducerUrl = encode(compile(await readFile(new URL('../../src/web/src/pitchState.ts', import.meta.url), 'utf8')));
let source = await readFile(new URL('../../src/web/src/main.tsx', import.meta.url), 'utf8');
source = source.replace(/import .* from 'react';/, 'const { useEffect, useMemo, useReducer, useRef, useState } = globalThis.__pitchTest;')
  .replace(/import .* from 'react-dom\/client';/, '')
  .replace(/import .* from '@microsoft\/signalr';/, 'const { HubConnectionBuilder, HubConnectionState } = globalThis.__pitchTest;')
  .replace("import './style.css';", '')
  .replace("from './pitchState'", `from '${reducerUrl}'`)
  .replace("function App()", 'export function App()')
  .replace(/createRoot\(document[\s\S]*$/, '');

test('rendered pitch: live / pause at 10s / B2 drag into and out of lane / resume / moving frames', async () => {
  let slots = [], cursor = 0, effects = [], tree, receive, server, requests = [];
  const same = (a, b) => a && b && a.length === b.length && a.every((x, i) => Object.is(x, b[i]));
  const useState = initial => {
    const i = cursor++; if (!(i in slots)) slots[i] = typeof initial === 'function' ? initial() : initial;
    return [slots[i], value => { slots[i] = typeof value === 'function' ? value(slots[i]) : value; }];
  };
  const useMemo = (fn, deps) => { const i = cursor++; if (!slots[i] || !same(slots[i].deps, deps)) slots[i] = { deps, value: fn() }; return slots[i].value; };
  const hub = { state: 'Connected', on: (_, handler) => { receive = handler; }, onreconnecting() {}, onreconnected() {}, onclose() {}, async start() {}, async stop() {} };
  globalThis.__pitchTest = {
    element: (type, props, ...children) => ({ type, props: props ?? {}, children: children.flat(Infinity).filter(x => x != null && x !== false) }),
    useState, useMemo,
    useRef: initial => useState(() => ({ current: initial }))[0],
    useReducer: (reducer, initial) => { const [state, set] = useState(initial); return [state, action => set(old => reducer(old, action))]; },
    useEffect: (fn, deps) => { const i = cursor++; if (!slots[i] || !same(slots[i].deps, deps)) { slots[i]?.cleanup?.(); slots[i] = { deps }; effects.push(() => { slots[i].cleanup = fn(); }); } },
    HubConnectionBuilder: class { withUrl() { return this; } withAutomaticReconnect() { return this; } build() { return hub; } },
    HubConnectionState: { Disconnected: 'Disconnected' },
  };
  let realApi, realClient, replayCatalogue;
  const explanationReplies = [];
  const originalFetch = globalThis.fetch;
  const originalPoint = globalThis.DOMPoint;
  globalThis.fetch = async (url, options) => {
    requests.push({ url, options });
    if (url === '/api/explanations') return new Promise(resolve => explanationReplies.push(resolve));
    if (realApi) return realClient.fetch(url, options);
    if (url === '/api/match/replay' && !options?.method) return { ok: true, json: async () => replayCatalogue };
    if (url.endsWith('/resume')) server = { ...server, revision: server.revision + 1, status: 'running' };
    if (url.endsWith('/pause')) server = { ...server, revision: server.revision + 1, status: 'paused' };
    return { ok: true, json: async () => server };
  };
  globalThis.DOMPoint = class { constructor(x, y) { this.x = x; this.y = y; } matrixTransform() { return this; } };
  let revisionEpoch = 0;
  const frame = (revision, timestamp, x) => ({ revision: revision + revisionEpoch, status: 'running', frameIndex: timestamp * 2, frameCount: 120,
    event: { id: `event-${timestamp}`, timestamp, teamId: 'A', ballPosition: { x: 20, y: 34 }, playerPositions: [
      { playerId: 'B2', teamId: 'B', position: { x, y: 30 } },
      { playerId: 'A1', teamId: 'A', position: { x: 20 + timestamp, y: 34 } },
    ] }, contributions: [] });
  const all = node => !node || typeof node !== 'object' ? [] : [node, ...node.children.flatMap(all)];
  const find = predicate => all(tree).find(predicate);
  const button = name => find(n => n.type === 'button' && n.children.includes(name));
  const player = () => find(n => n.props['aria-label']?.startsWith('Inspect B2'));
  const position = () => player().children.find(n => n.type === 'circle').props.cx;
  const svg = () => find(n => n.type === 'svg');
  const render = () => { cursor = 0; tree = App(); const pending = effects; effects = []; pending.forEach(fn => fn()); };
  let App;
  try {
    const moment = { ...frame(0, 3, 25), event: { ...frame(0, 3, 25).event, eventType: 'Pressure', scenario: 'Midfield screen' } };
    const insight = { id: 'insight-1', insightType: 'PassingLaneDenial', significanceScore: 45, isKeyMoment: true,
      title: 'Defender B2 closes a passing lane', explanation: 'Measured transition', primaryDefenderId: 'B2',
      affectedReceiverId: 'A2', observation: moment.event, triggerEvent: moment.event, supportingObservations: [moment.event], supportingEventIds: [moment.event.id],
      actualMeasurements: { eventId: moment.event.id, defenderDistanceMetres: 1.25, corridorMetres: 2, ballPosition: { x: 20, y: 34 }, receiverPosition: { x: 40, y: 34 }, blockerIds: ['B2'] },
      previousMeasurements: { eventId: 'previous-event', defenderDistanceMetres: 3, corridorMetres: 2, blockerIds: [] },
      significanceFactors: [{ reason: 'Available-to-covered transition', points: 30 }] };
    const futureMoment = frame(0, 30, 70); futureMoment.event.eventType = 'Interception';
    replayCatalogue = { events: [moment, futureMoment], insights: [{ ...insight, id: 'future', title: 'Future replay recovery', observation: futureMoment.event, triggerEvent: futureMoment.event }] };
    server = { ...frame(1, 9.5, 40), events: [moment], insights: [insight] };
    ({ App } = await import(encode(compile(source))));
    render(); await new Promise(resolve => setImmediate(resolve)); render();
    assert.equal(position(), 40);
    const rankedButton = find(n => n.type === 'button' && n.children.includes('Defender B2 closes a passing lane'));
    assert.ok(rankedButton, 'ranked insight is displayed');
    rankedButton.props.onClick(); render();
    assert.equal(position(), 25, 'ranked selection displays the exact supporting observation');
    assert.ok(find(n => n.props['aria-label'] === 'Selected insight passing lane'), 'affected lane is highlighted');
    assert.equal(player().children.find(n => n.type === 'circle').props.r, 2.3, 'primary defender is highlighted');
    const inferenceCalls = () => requests.filter(r => r.url === '/api/explanations').length;
    assert.equal(inferenceCalls(), 0, 'load, SignalR and ranked selection never generate');
    const audience = () => find(n => n.type === 'select');
    audience().props.onChange({ target: { value: 'analyst' } }); render();
    const verifiedText = () => all(find(n => n.props['aria-label'] === 'Verified Analyst facts')).flatMap(n => n.children.filter(c => typeof c === 'string' || typeof c === 'number')).join(' ');
    assert.ok(verifiedText().includes('1.25 metres from defender B2 to the candidate passing segment'));
    assert.ok(verifiedText().includes('3.00 metres from defender B2 to the candidate passing segment'));
    assert.ok(verifiedText().includes('coverage threshold  2  metres'));
    assert.ok(verifiedText().includes('45') && verifiedText().includes('previous-event'), 'server measurements, scores and references render before inference');
    audience().props.onChange({ target: { value: 'casual' } }); render();
    assert.equal(inferenceCalls(), 0, 'switching audiences never generates');
    button('Generate AI Explanation').props.onClick(); render();
    assert.equal(inferenceCalls(), 1, 'explicit click sends one request');
    assert.equal(button('Generate AI Explanation').props.disabled, true, 'generation disabled while in progress');
    // Keep SignalR delivery active during the unresolved inference.
    for (let i = 0; i < 120; i++) { receive(frame(10 + i, i / 2, 40 + i / 10)); render(); }
    assert.equal(slots[0].liveState.frameIndex, 119, 'entire match advances while inference is pending');
    assert.equal(inferenceCalls(), 1, 'entire playback triggers zero additional inference');
    audience().props.onChange({ target: { value: 'analyst' } }); render();
    audience().props.onChange({ target: { value: 'casual' } }); render();
    explanationReplies.shift()({ ok: true, json: async () => ({ source: 'generated', narrative: { title: 'STALE CASUAL', explanation: 'Wrong audience', evidenceEventIds: [], limitations: [] } }) });
    await new Promise(resolve => setImmediate(resolve)); render();
    assert.equal(find(n => n.children.includes('STALE CASUAL')), undefined, 'late response is rejected even after an audience round trip');
    audience().props.onChange({ target: { value: 'analyst' } }); render();
    button('Generate AI Explanation').props.onClick(); render();
    explanationReplies.shift()({ ok: true, json: async () => ({ source: 'fallback', narrative: { title: 'VALID ANALYST', explanation: 'Deterministic evidence explanation', evidenceEventIds: ['e1'], limitations: ['Synthetic only'] } }) });
    await new Promise(resolve => setImmediate(resolve)); render();
    assert.ok(find(n => n.children.includes('VALID ANALYST')), 'current explanation is displayed');
    assert.ok(find(n => n.children.includes('Deterministic fallback')), 'fallback source is visible');
    button('Generate AI Explanation').props.onClick(); render();
    button('Return to live').props.onClick(); render();
    explanationReplies.shift()({ ok: true, json: async () => ({ source: 'generated', narrative: { title: 'STALE INSIGHT', explanation: 'Wrong insight', evidenceEventIds: [], limitations: [] } }) });
    await new Promise(resolve => setImmediate(resolve)); render();
    assert.equal(find(n => n.children.includes('STALE INSIGHT')), undefined, 'late response cannot reopen a cleared insight');
    // Restore the baseline fixture and selection for existing replay/drag regressions.
    server = { ...server, revision: 200 }; receive(server); render();
    find(n => n.type === 'button' && n.children.includes('Defender B2 closes a passing lane')).props.onClick(); render();
    revisionEpoch = 1000;

    button('Return to live').props.onClick(); render();
    assert.equal(position(), 40, 'ranked selection returns to live');
    const timelineButton = find(n => n.type === 'button' && n.children.some(c => typeof c === 'string' && c.includes('Midfield screen')));
    assert.ok(timelineButton, 'timeline contains meaningful event');
    timelineButton.props.onClick(); render();
    assert.equal(position(), 25, 'timeline jump displays the selected observation');
    assert.equal(slots[0].pitchMode, 'hypothetical');
    button('Return to live').props.onClick(); render();
    assert.equal(position(), 40, 'return restores latest live observation');
    assert.equal(find(n => n.type === 'button' && n.children.includes('Future replay recovery')), undefined, 'future catalogue is absent from live view');
    await button('Browse complete synthetic replay').props.onClick(); await new Promise(resolve => setImmediate(resolve)); render();
    const replayButton = find(n => n.type === 'button' && n.children.includes('Future replay recovery'));
    assert.ok(replayButton, 'full replay requires explicit navigation');
    replayButton.props.onClick(); render();
    assert.equal(position(), 70, 'complete replay ranked insight selects its exact observation');
    button('Return to live').props.onClick(); render();
    assert.equal(position(), 40);
    assert.equal(find(n => n.type === 'button' && n.children.includes('Future replay recovery')), undefined, 'return to live removes future replay moments');
    server = frame(2, 10, 42); receive(server); render();
    assert.equal(position(), 42, 'normal SignalR frames move rendered players');
    await button('Pause').props.onClick(); await new Promise(resolve => setImmediate(resolve)); render();
    assert.equal(server.status, 'paused'); assert.equal(server.event.timestamp, 10);
    const original = JSON.stringify(server);
    const pointer = { pointerId: 1, button: 0, preventDefault() {}, currentTarget: { ownerSVGElement: { setPointerCapture() {} } } };
    player().props.onPointerDown(pointer); render();
    const drag = (x, y) => { svg().props.onPointerMove({ pointerId: 1, clientX: x, clientY: y, currentTarget: { getScreenCTM: () => ({ inverse() {} }) } }); render(); };
    drag(30, 34); drag(80, 60);
    assert.equal(position(), 42, 'actual observation stays frozen during dragging');
    assert.equal(find(n => n.props['aria-label'] === 'Drag B2 hypothetical position').children.find(n => n.type === 'circle').props.cx, 80);
    assert.equal(JSON.stringify(server), original, 'drag does not mutate source match');
    server = { ...frame(4, 10, 43), status: 'paused' }; receive(server); render();
    assert.equal(position(), 42, 'live state advances independently behind frozen display');
    await button('Resume').props.onClick(); render();
    assert.equal(position(), 43, 'Resume immediately renders latest received live positions');
    assert.equal(slots[0].pitchMode, 'live'); assert.equal(slots[0].hypotheticalFrame, null); assert.equal(slots[0].override, null);
    await new Promise(resolve => setImmediate(resolve)); render();
    assert.equal(server.event.timestamp, 10, 'resume preserves paused timestamp');
    for (const [revision, timestamp, x] of [[6, 10.5, 45], [7, 11, 48]]) {
      server = frame(revision, timestamp, x); receive(server); render(); assert.equal(position(), x, 'subsequent SignalR frames move rendered players');
    }
    player().props.onPointerDown(pointer); render(); drag(50, 40);
    const beforeReset = requests.length;
    button('Reset to actual positions / live pitch').props.onClick(); render();
    assert.equal(position(), 48); assert.equal(server.event.timestamp, 11); assert.equal(server.status, 'running');
    assert.equal(requests.length, beforeReset, 'reset makes no playback request');
    receive(frame(6, 10.5, 45)); render(); assert.equal(position(), 48, 'stale responses cannot rewind live display');
    assert.deepEqual(requests.filter(r => r.options?.method === 'POST' && r.url !== '/api/explanations').map(r => r.url), ['/api/match/pause', '/api/match/resume']);
    if (process.env.PITCH_API_URL) {
      // Start a fresh live-state fixture before switching from synthetic revisions to the real server.
      slots[0] = { ...slots[0], liveState: undefined };
      render();
      const positionY = () => player().children.find(n => n.type === 'circle').props.cy;
      realApi = process.env.PITCH_API_URL;
      realClient = visitor(realApi, originalFetch);
      const request = async command => (await realClient.fetch(`/api/match${command ? '/' + command : ''}`, command ? { method: 'POST' } : undefined)).json();
      const negotiation = await (await realClient.fetch(`/hubs/match/negotiate?negotiateVersion=1`, { method: 'POST' })).json();
      const ws = realClient.socket(`/hubs/match?id=${encodeURIComponent(negotiation.connectionToken)}`);
      const until = async (predicate, label) => {
        const deadline = Date.now() + 15000;
        while (!predicate()) { assert.ok(Date.now() < deadline, label); await new Promise(resolve => setTimeout(resolve, 20)); }
      };
      let latest, handshake = false;
      ws.addEventListener('message', event => {
        for (const part of String(event.data).split('\x1e').filter(Boolean)) {
          const message = JSON.parse(part);
          if (!('type' in message)) handshake = true;
          if (message.target === 'MatchUpdated') { latest = message.arguments[0]; receive(latest); render(); }
        }
      });
      try {
        await until(() => ws.readyState === WebSocket.OPEN, 'socket opens');
        ws.send(JSON.stringify({ protocol: 'json', version: 1 }) + '\x1e');
        await until(() => handshake, 'SignalR handshake');
        await request('replay');
        await until(() => latest?.event.timestamp >= 10, 'playback reaches 10 seconds');
        await button('Pause').props.onClick(); await until(() => latest.status === 'paused', 'pause broadcast'); render();
        const paused = await request();
        const catalogue = await (await realClient.fetch(`/api/match/replay`)).json();
        const ranked = catalogue.insights.find(i => i.isKeyMoment);
        await button('Browse complete synthetic replay').props.onClick();
        await until(() => { render(); return !!find(n => n.type === 'button' && n.children.includes(ranked.title)); }, 'real replay catalogue renders');
        const beforeSelections = requests.filter(r => r.url === '/api/explanations').length;
        for (const item of catalogue.insights.filter(i => i.isKeyMoment).slice(0, 5)) {
          find(n => n.type === 'button' && n.children.includes(item.title)).props.onClick(); render();
        }
        assert.equal(requests.filter(r => r.url === '/api/explanations').length, beforeSelections, 'selecting all five real ranked moments triggers zero inference');
        find(n => n.type === 'button' && n.children.includes(ranked.title)).props.onClick(); render();
        assert.equal(positionY(), ranked.observation.playerPositions.find(p => p.playerId === 'B2').position.y, 'real ranked insight displays exact evidence positions');
        assert.ok(find(n => n.type === 'pre').children[0].includes(ranked.triggerEvent.id), 'real outcome event is retained in evidence');
        assert.ok(find(n => n.props['aria-label'] === 'Selected insight passing lane'), 'real ranked lane is highlighted');
        find(n => n.props['aria-label'] === `Inspect ${ranked.primaryDefenderId}`).props.onPointerDown(pointer); render(); drag(50, 60);
        await until(() => { render(); return !!find(n => n.type === 'table'); }, 'ranked insight permits real hypothetical calculation');
        button('Reset to actual positions / live pitch').props.onClick(); render();
        assert.equal(positionY(), paused.event.playerPositions.find(p => p.playerId === 'B2').position.y, 'ranked analysis returns to paused live frame');
        assert.deepEqual(await request(), paused, 'ranked selection and drag do not mutate shared playback');
        const actualX = positionY();
        player().props.onPointerDown(pointer); render(); drag(30, 34);
        await until(() => { render(); return !!find(n => n.type === 'table'); }, 'real counterfactual comparison renders');
        drag(80, 60);
        assert.equal(positionY(), actualX, 'real observation stays frozen');
        assert.deepEqual(await request(), paused, 'real server remains unchanged by dragging');
        await button('Resume').props.onClick(); render();
        assert.equal(positionY(), actualX, 'resume immediately renders paused live observation');
        await until(() => latest.status === 'running', 'resume broadcast');
        assert.equal(slots[0].pitchMode, 'live'); assert.equal(slots[0].override, null);
        await until(() => latest.frameIndex > paused.frameIndex && positionY() !== actualX, 'real SignalR moves SVG player after resume');
        const firstMovingX = positionY();
        await until(() => positionY() !== firstMovingX, 'second real SignalR frame moves SVG player');
        assert.ok(latest.event.timestamp > paused.event.timestamp);
        assert.equal(positionY(), latest.event.playerPositions.find(p => p.playerId === 'B2').position.y);
        console.log(`Real SignalR lifecycle passed: paused ${paused.event.timestamp}s, resumed SVG B2 ${actualX} -> ${firstMovingX} -> ${positionY()}`);
      } finally { await request('pause'); ws.close(); }
    }

  } finally {
    slots.forEach(slot => slot?.cleanup?.()); globalThis.fetch = originalFetch; globalThis.DOMPoint = originalPoint; delete globalThis.__pitchTest;
  }
});


