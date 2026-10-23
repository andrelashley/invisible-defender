# Defensive insights and replay

This specification supersedes the per-frame-only analytical scope in INITIAL_SLICE.md
and the 25-button timeline in SCENARIO_REPLAY.md. Existing lane geometry,
synthetic scripts, playback controls, evidence and counterfactual analysis remain.

## Detection and evidence

`DefensiveInsightEngine` is a stateless, dependency-injected C# domain service.
It processes ordered, unique observations and reuses `PassingLaneAnalyzer`.
No results depend on scenario names or numbers. Scenario changes, match changes
and explicit ScenarioStart events clear comparison history.

- Denial: available -> covered for the same carrier and candidate receiver in
  adjacent non-flight observations at most 1 second apart. The selected blocker
  must move between observations. Among eligible blockers choose the nearest
  to the lane, then ordinal defender ID. Geometry and movement coexist; intent
  and prevented passes are not established.
- Reopening: the reverse transition, requiring a moving former blocker.
  It is contextual information, never automatically a defensive success.
- Redirection: PassCompleted must match a same-team PassAttempt to that receiver
  within 2 seconds. The releasing carrier must have a preceding scripted Pressure
  event within 4 seconds of the attempt and measured coverage at/after that
  pressure. Signed pass x displacement <= 2 m is lateral/backward; below -2 m
  is backward. A attacks increasing x, B decreasing x. Pressure causing a pass
  is explicitly unproven. The affected lane is the measured covered option;
  the actual pass target remains available in the supporting attempt.
- Recovery: Interception or BallRecovery changes possession from the preceding
  opposing-team observation. Where the recovering player covered a lane within
  the previous 4 seconds, retain that geometry. Without it, report only the
  observed recovery, with no preceding positioning claim. Saves are excluded.

Every insight retains exact event IDs and complete supporting positional events.
ActualMeasurements and PreviousMeasurements are deterministic geometry, with
endpoints, blockers, defender position, corridor and perpendicular distance
(where the projection is interior and the ball is not in flight).
Explanation and significance factors are heuristic interpretations.
Counterfactuals remain separate, explicitly hypothetical endpoint responses.
No confidence percentages, goals prevented or causal attribution are generated.

`TriggerEvent` is the event establishing the insight. `Observation` is the pitch
snapshot selected for inspection: the after-frame for transitions, and preceding
measured positioning for recovery/redirection when available. Both are referenced
in evidence. This lets users drag the defender while the original attacking team
still possesses the ball. The UI labels trigger time and pitch evidence time.

## Ranking

All scores are sums of exposed, deterministic selection points:

| Factor | Points |
| --- | ---: |
| Distinct grouped sequence | 5 |
| Referenced synthetic observations | 5 |
| Available-to-covered transition | 30 |
| Reopening context | 0 |
| Observed possession recovery | 45 |
| Completed redirection with pressure and geometry | 25 |
| Denied receiver progresses at least 5 m toward goal | 10 |
| Denied receiver within 25 m of attacking goal line | 10 |
| Before/after or linked preceding positional evidence | 5 |

Rank descending score, then ascending start time and ordinal insight ID.
The backend marks scores >=35 as key moments, excluding reopenings. The UI
shows up to five; if fewer qualify, it shows the available moments or an empty
state. Scores are selection heuristics, not calibrated performance measures.

## Grouping

Group by match, scenario occurrence, category, defender, carrier and affected
receiver when the next trigger is within 4 seconds of the episode end.
Repeated transitions combine distinct evidence IDs and observations without
adding points. Sustained coverage extends a denial episode and retains its
frames. Static coverage alone never creates an insight. Keep the first detected
representative snapshot and ranking factors; later grouped observations extend
the episode without changing its original score. Resets never merge episodes,
even when a scenario name repeats. Replaying a prefix yields identical results.

## API and navigation

- GET /api/match and SignalR MatchUpdated contain prefix-bounded `insights` and
  `events` (inspection states with measured contributions). No future frame,
  insight, grouped endpoint or supporting observation is included.
- GET /api/match/insights returns the same current-prefix insights, including
  completed matches until the shared match is restarted.
- GET /api/match/replay explicitly returns all 120 inspection states and ranked
  insights for the reproducible synthetic replay. The browser fetches this only
  after Browse complete synthetic replay is selected and labels future content.
- GET /api/match/moments remains a legacy replay catalogue of 25 beats for
  compatibility, and is no longer fetched by the UI.
- POST /api/match/replay still restarts shared playback; the GET is read-only.

Key Defensive Moments are separate from the collapsed All Match Events list.
Raw events show time, readable event type, scenario and possession team. Selecting
any observation is local and freezes its evidence while live snapshots continue.
Selecting a ranked moment highlights its defender and affected measured lane.
Return to live, Reset, Start, Replay and Resume clear the selection; returning to
live also closes the complete replay catalogue. Stale replay fetches cannot reopen
it after returning to live. Existing drag/counterfactual calculations are retained.

## Limitations and next milestone

Only synthetic, half-second samples are supported. Teams have fixed attack
orientations. Movement can coincide with moving lane endpoints, so transition
attribution is geometric association, not an isolated causal estimate. Multiple
blockers are reduced to one primary defender. Pressure is a scripted label.
Temporal association does not prove causal pressure or tactical intent. Already
covered opening frames do not count as newly closed lanes. Ranking weights and
windows are transparent defaults, not empirically calibrated football values.

The singleton recomputes the short observed prefix and transmits complete history;
this is intentionally bounded to the 120-frame MVP, with no persistence or DB.
No Foundry, cloud deployment, real datasets or extra UI framework is introduced.
Next: expand synthetic fixtures (multiple blockers, mirrored attacks, longer
pressure sequences) and review ranking/grouping sensitivity before adding AI
explanations grounded exclusively in these evidence records.
