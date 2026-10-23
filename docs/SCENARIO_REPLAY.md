# Scenario replay

This slice supersedes the fixed-A1 replay assumptions in INITIAL_SLICE.md.
The same 120 frames, half-second clock, playback singleton, SignalR snapshots,
pure lane analyzer and stateless counterfactual endpoint are retained.

Five independent 12-second clips explicitly restart the scenario at frames
0, 24, 48, 72 and 96. These are curated situations, not contiguous match play.
Team A attacks right, B left. Six players per side include goalkeepers A6/B6.

- Midfield denial: B1 screens A2; A1 passes through A4 to progress.
- Backward recycling: B3 covers A3; A2 passes backward to A1 then A5.
- Wide redirection: B4 covers the inside option; A3 finds A5 farther from goal.
- Recovery: B5 moves into the forward lane, intercepts A2's attempted pass
  toward A3, then finds B3 for the transition.
- Combination and shot: B2 screens the direct option; A1 combines with A4,
  A2 shoots, and B6 gathers. Coverage does not imply an attack must fail.

Event vocabulary: ScenarioStart, Pressure, Carry, PassAttempt, PassCompleted,
Interception, ShotSetup, ShotAttempt, Save. Outcomes and movements are scripted
observations; geometry is calculated from positions, never assigned by scenario.
PlayerId identifies the possessing player (the releasing player during flight).
TeamId is the possessing/releasing team; it changes on reception/interception/save.
BallCarrierId is null in flight. BallFrom/BallTo describe the travel segment;
TargetPlayerId is the intended receiver (A3 on the intercepted attempt).
No possession-based lane analysis runs while the ball is in flight.

GET /api/match/moments returns 25 reproducible observation beats with freshly
calculated evidence. Selecting one freezes a local observation using the same
explicit hypothetical mode as evidence inspection. It does not seek or mutate
shared playback. Return to live, Start, Replay and Resume clear that selection.
The timeline exposes both defensive geometry and subsequent scripted outcomes.

Evidence boundaries: measured perpendicular distance and blocker positions link
to the exact observation ID. A backward pass or redirection can be observed
alongside coverage, but why the attacker chose it remains unproven. Purple
positions and lane comparisons are hypothetical. No prevented goals, causal
attribution, probabilities or confidence scores are inferred.

Limitations: linear interpolation, coarse half-second sampling, independent clip
resets, no acceleration/physics, offside, fatigue, pressure detector, tackling or
shot-quality model. Pressure labels are scripted events, not analytical detections.
Only passing-lane geometry is detected; recovery/redirection explanations combine
that measurement with explicit observed events. All data is synthetic; no AI or
cloud resources are used.

Defensive insight detection, deterministic ranking, grouping and replay selection
are documented in [DEFENSIVE_INSIGHTS.md](DEFENSIVE_INSIGHTS.md), which supersedes
the per-frame-only detector and replay timeline scope above.
