import { useEffect, useMemo, useReducer, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import './style.css';

import { pitchReducer, initialPitchState, displayedFrame, type Point, type MatchEvent, type MatchState, type Contribution, type DefensiveInsight } from './pitchState';
type Lane = { receiverId: string; start: Point; end: Point; blockerIds: string[] };
type Comparison = { eventId: string; before: Lane[]; after: Lane[] };
type Scenario = { frame: MatchEvent; defenderId: string; position: Point };
function App() {
  const [pitch, dispatch] = useReducer(pitchReducer, initialPitchState);
  const state = pitch.liveState;
  const [connection, setConnection] = useState('Connecting');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [selected, setSelected] = useState<string>('B1');
  const [evidence, setEvidence] = useState<Contribution>();
  const [moments, setMoments] = useState<MatchState[]>([]);
  const [chosenInsight, setChosenInsight] = useState<DefensiveInsight>();
  const [replay, setReplay] = useState(false);
  const [replayInsights, setReplayInsights] = useState<DefensiveInsight[]>([]);
  const [inspected, setInspected] = useState<MatchState>();
  const [audience, setAudience] = useState('casual');
  type Narration = { narrative: { title: string; explanation: string; evidenceEventIds: string[]; limitations: string[] }; source: string };
  const [narration, setNarration] = useState<{ key: string; value?: Narration; loading?: boolean; error?: string }>();
  const narrationKey = JSON.stringify([chosenInsight, audience, replay]);
  const narrationSelection = useRef(narrationKey);
  const narrationRequest = useRef(0);
  if (narrationSelection.current !== narrationKey) {
    narrationSelection.current = narrationKey;
    narrationRequest.current++;
  }
  async function generateExplanation() {
    if (!chosenInsight || (narration?.key === narrationKey && narration.loading)) return;
    const key = narrationKey, request = ++narrationRequest.current;
    setNarration({ key, loading: true });
    try {
      const response = await fetch('/api/explanations', { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ matchId: chosenInsight.matchId, insightId: chosenInsight.id, audience, scope: replay ? 'replay' : 'live' }) });
      if (!response.ok) throw new Error(response.status === 429 ? 'Request limit reached. Please try again shortly.' : 'Explanation unavailable. The analytical evidence remains available.');
      const value: Narration = await response.json();
      if (narrationSelection.current === key && narrationRequest.current === request) setNarration({ key, value });
    } catch (e) {
      if (narrationSelection.current === key && narrationRequest.current === request) setNarration({ key, error: String(e) });
    }
  }
  const currentNarration = narration?.key === narrationKey ? narration : undefined;
  const replayRequest = useRef(0);
  async function loadReplay() {
    const request = ++replayRequest.current;
    try {
      const response = await fetch('/api/match/replay');
      if (!response.ok) throw new Error('Unable to load replay');
      const data = await response.json();
      if (request !== replayRequest.current) return;
      setMoments(data.events); setReplayInsights(data.insights); setReplay(true); resetScenario();
    } catch (e) { if (request === replayRequest.current) setError(String(e)); }
  }
  const visibleMoments: MatchState[] = replay ? moments : state?.events ?? [];
  const insights = replay ? replayInsights : state?.insights ?? [];
  function inspectMoment(moment: MatchState, insight?: DefensiveInsight) {
    const related = insight ?? insights.find(i => !i.isKeyMoment && i.observation.id === moment.event.id);
    resetScenario(); dispatch({ type: 'hypothetical', frame: moment.event, override: null }); setInspected(moment); setChosenInsight(related);
    if (related?.primaryDefenderId) setSelected(related.primaryDefenderId);
  }
  const readable = (value: string) => value.replace(/([a-z])([A-Z])/g, '$1 $2');
  const scenario = useMemo<Scenario | undefined>(() =>
    pitch.pitchMode === 'hypothetical' && pitch.hypotheticalFrame && pitch.override
      ? { frame: pitch.hypotheticalFrame, ...pitch.override } : undefined,
    [pitch.pitchMode, pitch.hypotheticalFrame, pitch.override]);
  const [analysis, setAnalysis] = useState<{ scenario: Scenario; result: Comparison }>();
  const [analysisError, setAnalysisError] = useState('');
  const dragging = useRef<number | null>(null);
  useEffect(() => {
    setAnalysis(undefined);
    setAnalysisError('');
    if (!scenario) return;
    const controller = new AbortController();
    const timer = setTimeout(() => {
      void fetch('/api/counterfactual', { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(scenario), signal: controller.signal })
        .then(async response => { if (!response.ok) throw new Error('Unable to calculate hypothetical coverage'); return response.json() as Promise<Comparison>; })
        .then(result => { if (!controller.signal.aborted) setAnalysis({ scenario, result }); })
        .catch(e => { if (!controller.signal.aborted) setAnalysisError(String(e)); });
    }, 80);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [scenario]);
  useEffect(() => {
    let disposed = false;
    const hub = new HubConnectionBuilder().withUrl('/hubs/match').withAutomaticReconnect().build();
    const accept = (next: MatchState) => { if (!disposed) dispatch({ type: 'live', next }); };
    const refresh = async () => { const response = await fetch('/api/match'); if (!response.ok) throw new Error('Unable to load match'); accept(await response.json()); };
    hub.on('MatchUpdated', accept);
    hub.onreconnecting(() => { if (!disposed) setConnection('Reconnecting'); });
    hub.onreconnected(() => { if (!disposed) { setConnection('Live'); void refresh().catch(e => setError(String(e))); } });
    hub.onclose(() => { if (!disposed) setConnection('Disconnected — reload to reconnect'); });
    async function connect() {
      try {
        await refresh();
        await hub.start();
        if (disposed) { await hub.stop(); return; }
        setConnection('Live'); await refresh();
      } catch (e) {
        if (!disposed) { setConnection('Connection failed — reload to retry'); setError(String(e)); }
      }
    }
    void connect();
    return () => { disposed = true; if (hub.state !== HubConnectionState.Disconnected) void hub.stop(); };
  }, []);
  async function control(command: string) {
    if (command === 'resume' || command === 'start' || command === 'replay') { returnToLive(); }
    setBusy(true); setError('');
    try {
      const response = await fetch(`/api/match/${command}`, { method: 'POST' });
      if (!response.ok) throw new Error(`Could not ${command} match`);
      const next: MatchState = await response.json();
      dispatch({ type: 'live', next });
    } catch (e) { setError(String(e)); } finally { setBusy(false); }
  }
  const frame = displayedFrame(pitch);
  // Only display a response for the exact current scenario, including between render and effect cleanup.
  const comparison = analysis?.scenario === scenario ? analysis?.result : undefined;
  const contributions = scenario ? [] : evidence ? [evidence] : inspected?.contributions ?? state?.contributions ?? [];
  function movePlayer(e: React.PointerEvent<SVGSVGElement>) {
    if (dragging.current !== e.pointerId) return;
    const matrix = e.currentTarget.getScreenCTM();
    if (!matrix) return;
    const point = new DOMPoint(e.clientX, e.clientY).matrixTransform(matrix.inverse());
    dispatch({ type: 'move', position: {
      x: Math.max(0, Math.min(105, point.x)), y: Math.max(0, Math.min(68, point.y)) } });
  }
  function returnToLive() { replayRequest.current++; setReplay(false); resetScenario(); }
  function resetScenario() { dragging.current = null; dispatch({ type: 'returnToLive' }); setAnalysis(undefined); setEvidence(undefined); setInspected(undefined); setChosenInsight(undefined); }
  return <main>
    <header><div><p className="eyebrow">FOOTBALL INTELLIGENCE / SYNTHETIC DEMO</p><h1>Invisible Defender</h1><p>See the passing lanes a defender takes away.</p></div><span className="badge">{connection}</span></header>
    <section className="toolbar" aria-label="Playback controls">
      <button disabled={busy || !state || state.status !== 'ready'} onClick={() => void control('start')}>Start match</button>
      <button disabled={busy || state?.status !== 'running'} onClick={() => void control('pause')}>Pause</button>
      <button disabled={busy || state?.status !== 'paused'} onClick={() => void control('resume')}>Resume</button>
      <button disabled={busy || !state} onClick={() => void control('replay')}>Replay</button>
      <span>{state ? `${state.status} · ${state.event.timestamp.toFixed(1)} s / 59.5 s` : 'Loading match…'}</span>
    </section>
    <section className="panel timeline" aria-label="Replay event timeline">
      <div className="section-title"><h2>Key Defensive Moments</h2><button onClick={() => { if (replay) { returnToLive(); } else void loadReplay(); }}>{replay ? 'Close replay / return to live' : 'Browse complete synthetic replay'}</button></div>
      <p>{replay ? 'Complete replay catalogue - includes later events.' : 'Live observations only - moments appear as evidence becomes available.'} Select a moment to freeze its evidence. Scores are heuristic selection points, not probabilities or goals prevented.</p>
      {insights.filter(i => i.isKeyMoment).length === 0 && <p>No significant defensive moments detected yet. All observed events remain available below.</p>}
      <div className="key-moments">{insights.filter(i => i.isKeyMoment).slice(0, 5).map(insight => <article key={insight.id}>
        <button aria-pressed={chosenInsight?.id === insight.id} onClick={() => inspectMoment({ revision: 0, status: 'inspection', frameIndex: 0, frameCount: 120, event: insight.observation, contributions: visibleMoments.find(m => m.event.id === insight.observation.id)?.contributions ?? [] }, insight)}>{insight.triggerEvent.timestamp.toFixed(1)} s | {insight.title}</button>
        <p>{readable(insight.insightType)} | Defender {insight.primaryDefenderId ?? 'unassigned'} | {insight.significanceScore} selection points</p><p>{insight.explanation}</p>
        <p>Measured lane to {insight.affectedReceiverId ?? 'unspecified receiver'}: {insight.actualMeasurements.blockerIds.length} blockers at {insight.observation.timestamp.toFixed(1)} s. {insight.previousMeasurements && insight.previousMeasurements.eventId !== insight.actualMeasurements.eventId && `Previously ${insight.previousMeasurements.blockerIds.length} blockers.`}</p>
        {typeof insight.actualMeasurements.defenderDistanceMetres === 'number' && <p>Defender distance from lane: {insight.actualMeasurements.defenderDistanceMetres.toFixed(2)} m | coverage threshold {insight.actualMeasurements.corridorMetres} m.</p>}
        <details><summary>Why this moment?</summary><ul>{insight.significanceFactors.map(f => <li key={f.reason}>{f.reason}: +{f.points}</li>)}</ul></details>
      </article>)}</div>
      <details><summary>All Match Events ({visibleMoments.length})</summary><p>Complete observed timeline, including lane reopenings and frames with no coverage.</p>
      <ol className="event-timeline">{visibleMoments.map(moment => <li key={moment.event.id}><button key={moment.event.id} aria-pressed={inspected?.event.id === moment.event.id} onClick={() => inspectMoment(moment)}>{`${moment.event.timestamp.toFixed(1)} s | ${readable(moment.event.eventType ?? 'Observation')} | ${moment.event.scenario ?? ''} ? Possession ${moment.event.teamId}`}</button></li>)}</ol>
      </details>
    </section>
    {chosenInsight && <section className="panel" aria-label="Insight explanation">
      <h2>Explain this defensive moment</h2>
      <p>{chosenInsight.title}: {chosenInsight.explanation}</p>
      <p>Supporting events: {chosenInsight.supportingEventIds.join(', ')}</p>
      <label>Audience <select value={audience} onChange={e => setAudience(e.target.value)}>
        <option value="casual">Casual Fan</option><option value="analyst">Analyst</option>
      </select></label>{' '}
      <button disabled={currentNarration?.loading} onClick={() => void generateExplanation()}>Generate AI Explanation</button>
      <div aria-live="polite">
        {currentNarration?.loading && <p>Generating explanation? Playback continues.</p>}
        {currentNarration?.error && <p role="alert">{currentNarration.error}</p>}
        {currentNarration?.value && <>
          <p className="badge">{{ generated: 'AI generated', cached: 'Cached AI explanation', fallback: 'Deterministic fallback', mock: 'Mock explanation (local testing)' }[currentNarration.value.source] ?? 'Deterministic fallback'}</p>
          <h3>{currentNarration.value.narrative.title}</h3><p>{currentNarration.value.narrative.explanation}</p>
          <p>Evidence: {currentNarration.value.narrative.evidenceEventIds.join(', ')}</p>
          <ul>{currentNarration.value.narrative.limitations.map((item, index) => <li key={index}>{item}</li>)}</ul>
        </>}
      </div>
    </section>}
    {error && <p role="alert" className="error">{error}</p>}
    <div className="layout"><section className="panel">
      <div className="section-title"><h2>{scenario ? 'Hypothetical analysis' : (evidence || inspected) ? 'Evidence snapshot' : 'Live pitch'}</h2>{(evidence || inspected) && !scenario && <button onClick={returnToLive}>Return to live</button>}</div>
      <p>6-a-side | 105 x 68 m | Possession Team {frame?.teamId} | attacks {frame?.teamId === 'A' ? 'right' : 'left'}</p>
      <p>Select and drag a player from the team out of possession to test a hypothetical position. Live playback continues independently.</p>
      {scenario && <div className="scenario"><strong>Hypothetical position | frozen event {scenario.frame.id} | {scenario.frame.timestamp.toFixed(1)} s</strong><p>Hollow marker: actual position. Purple marker: hypothetical position.</p><button onClick={returnToLive}>Reset to actual positions / live pitch</button></div>}
      <svg onPointerMove={movePlayer} onPointerUp={e => { movePlayer(e); dragging.current = null; }} onPointerCancel={() => { dragging.current = null; }} onLostPointerCapture={() => { dragging.current = null; }} viewBox="-3 -3 111 74" role="group" aria-label="Football pitch showing players, ball, and blocked passing lanes">
        <rect x="0" y="0" width="105" height="68" fill="#175442" />
        <g fill="none" stroke="#b8d9c5" strokeWidth=".3"><rect width="105" height="68"/><path d="M52.5 0V68"/><circle cx="52.5" cy="34" r="9.15"/><rect y="13.84" width="16.5" height="40.32"/><rect x="88.5" y="13.84" width="16.5" height="40.32"/><rect y="24.84" width="5.5" height="18.32"/><rect x="99.5" y="24.84" width="5.5" height="18.32"/></g>
        {comparison?.after.map(lane => <line key={lane.receiverId} x1={lane.start.x} y1={lane.start.y} x2={lane.end.x} y2={lane.end.y}
          stroke={lane.blockerIds.length ? '#f6c965' : '#80efb6'} strokeWidth=".7" strokeDasharray={lane.blockerIds.length ? '1 1' : undefined} />)}
        {contributions.map(c => <g key={c.id}><line x1={c.start.x} y1={c.start.y} x2={c.end.x} y2={c.end.y} stroke="#f6c965" strokeWidth="4" opacity=".2"/><line x1={c.start.x} y1={c.start.y} x2={c.end.x} y2={c.end.y} stroke="#f6c965" strokeWidth=".45" strokeDasharray="1 1"/></g>)}
        {!scenario && chosenInsight?.actualMeasurements.receiverPosition && <line aria-label="Selected insight passing lane" x1={chosenInsight.actualMeasurements.ballPosition.x} y1={chosenInsight.actualMeasurements.ballPosition.y} x2={chosenInsight.actualMeasurements.receiverPosition.x} y2={chosenInsight.actualMeasurements.receiverPosition.y} stroke={chosenInsight.actualMeasurements.blockerIds.length ? '#f6c965' : '#80efb6'} strokeWidth="1" />}
        {frame?.playerPositions.map(p => <g key={p.playerId} role="button" tabIndex={0} aria-label={`Inspect ${p.playerId}${scenario ? " actual position" : ""}`} onPointerDown={e => {
          if (p.teamId === frame.teamId || e.button !== 0) return;
          e.preventDefault(); setSelected(p.playerId); dragging.current = e.pointerId;
          e.currentTarget.ownerSVGElement?.setPointerCapture(e.pointerId);
          if (!scenario || scenario.defenderId !== p.playerId) dispatch({ type: 'hypothetical', frame, override: { defenderId: p.playerId, position: { ...p.position } } });
        }} onClick={() => setSelected(p.playerId)} onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); setSelected(p.playerId); } }}>
          <circle cx={p.position.x} cy={p.position.y} r={selected === p.playerId ? 2.3 : 1.8} fill={scenario?.defenderId === p.playerId ? 'none' : p.teamId === 'A' ? '#6fc9ff' : '#ff9384'} stroke={selected === p.playerId ? '#fff' : '#132e32'} strokeWidth=".5"/>
          <text x={p.position.x} y={p.position.y - 2.8} textAnchor="middle" fontSize="1.7" fill="#edf9f5" fontWeight="bold">{p.playerId}</text>
        </g>)}
        {scenario && <g role="button" aria-label={`Drag ${scenario.defenderId} hypothetical position`} onPointerDown={e => { e.preventDefault(); dragging.current = e.pointerId; e.currentTarget.ownerSVGElement?.setPointerCapture(e.pointerId); }}><line x1={scenario.frame.playerPositions.find(p => p.playerId === scenario.defenderId)!.position.x} y1={scenario.frame.playerPositions.find(p => p.playerId === scenario.defenderId)!.position.y} x2={scenario.position.x} y2={scenario.position.y} stroke="#dbafff" strokeWidth=".4" strokeDasharray="1 1"/><circle cx={scenario.position.x} cy={scenario.position.y} r="2.3" fill="#b57aef" stroke="white" strokeWidth=".5"/><text x={scenario.position.x} y={scenario.position.y - 3} textAnchor="middle" fontSize="1.8" fill="white">{scenario.defenderId} hypothetical</text></g>}
        {frame?.ballFrom && frame.ballTo && <line aria-label="Ball movement" x1={frame.ballFrom.x} y1={frame.ballFrom.y} x2={frame.ballTo.x} y2={frame.ballTo.y} stroke="white" strokeWidth=".8" strokeDasharray="3 1"/>}
        {frame && <circle cx={frame.ballPosition.x} cy={frame.ballPosition.y} r=".8" fill="white" stroke="#142e2e" strokeWidth=".2"/>}
      </svg>
      <p className="legend">Blue: Team A &nbsp; Coral: Team B &nbsp; White dashed: ball travel &nbsp; <span>--- Gold: covered lane</span> | Green solid: uncovered lane</p>
      <p>{selected}{scenario ? " actual" : ""}: {frame?.playerPositions.filter(p => p.playerId === selected).map(p => `(${p.position.x.toFixed(1)}, ${p.position.y.toFixed(1)}) m`).join('')} · Select any player to inspect position.</p>
    </section><aside className="panel">
      {scenario && <section aria-live="polite"><h2>Hypothetical passing-lane availability</h2><p>Compared with actual positions in event {scenario.frame.id}. Covered means at least one defender blocks the lane; this is a geometric estimate.</p>
        <p>{scenario.defenderId} hypothetical: ({scenario.position.x.toFixed(1)}, {scenario.position.y.toFixed(1)}) m</p>
        {analysisError ? <p role="alert">{analysisError}</p> : !comparison ? <p>Calculating hypothetical coverage...</p> : <>
          <p>Available lanes: {comparison.before.filter(l => !l.blockerIds.length).length} → {comparison.after.filter(l => !l.blockerIds.length).length} (actual → hypothetical)</p>
          <table><thead><tr><th>Receiver</th><th>Actual</th><th>Hypothetical</th><th>Change</th></tr></thead><tbody>{comparison.before.map((before, i) => {
            const after = comparison.after[i]; const was = before.blockerIds.length > 0; const now = after.blockerIds.length > 0;
            return <tr key={before.receiverId}><td>{before.receiverId}</td><td>{was ? `Covered (${before.blockerIds.join(', ')})` : 'Available'}</td><td>{now ? `Covered (${after.blockerIds.join(', ')})` : 'Available'}</td><td>{was === now ? 'Unchanged' : now ? 'Newly covered' : 'Newly available'}</td></tr>;
          })}</tbody></table>
        </>}
      </section>}
      {!scenario && chosenInsight && <section><h2>{chosenInsight.title}</h2><p>{chosenInsight.explanation}</p><p>Observed event: {readable(chosenInsight.triggerEvent.eventType)} at {chosenInsight.triggerEvent.timestamp.toFixed(1)} s. Pitch evidence: {chosenInsight.observation.timestamp.toFixed(1)} s. Measurements are deterministic; significance is heuristic. Purple positions are hypothetical.</p><ul>{chosenInsight.significanceFactors.map(f => <li key={f.reason}>{f.reason}: +{f.points}</li>)}</ul><p>Supporting events: {chosenInsight.supportingEventIds.join(', ')}</p></section>}
      {!scenario && <><h2>Passing-lane denial</h2><p>Defenders within 2 m of a passing segment, strictly between ball and receiver. Candidate passes are 5–40 m long.</p>
      <p className="count">{contributions.length} <small>blocked lanes in this frame</small></p>
      {contributions.length === 0 && <p>No passing lanes meet the denial rule in this frame.</p>}
      {contributions.map(c => <article key={c.id}><h3>{c.defenderId}: lane to {c.receiverId}</h3><p>{c.explanation}</p><button onClick={() => { if (frame) { dispatch({ type: 'hypothetical', frame, override: null }); setEvidence(c); setSelected(c.defenderId); } }}>Inspect evidence · {frame?.timestamp.toFixed(1)} s</button><p className="event-id">Event: {c.eventId}</p></article>)}
      <p className="note">This is a geometric indicator, not proof of a prevented pass. Counts are per frame and are not cumulative defensive actions. All data is synthetic; explanations use deterministic templates.</p>
    </>}</aside></div>
    {(evidence || inspected) && <section className="panel"><h2>Supporting event and positional data</h2><p>This snapshot stays fixed while live playback continues.</p><pre>{JSON.stringify({ event: frame, contribution: evidence, insight: chosenInsight }, null, 2)}</pre></section>}
  </main>;
}
createRoot(document.getElementById('root')!).render(<App />);



