# Initial slice decisions and specification review

The four original specification files describe the full MVP. For this phase the user's explicit scope overrides Acceptance's three-detector requirement and the Foundry/Azure requirements. Only passing-lane denial is implemented; no AI, cloud provisioning, authentication, or persistence is included.

## Decisions filling specification gaps
- .NET 10, React/TypeScript/Vite; one API process, an in-memory singleton match, pure C# analytical service, one hosted playback loop, one SignalR hub. No database or separate services.
- Coordinates use metres: x = 0–105, y = 0–68. Team A attacks toward increasing x. This synthetic demonstration uses six players per side, including keepers, with known possession (A1). The event vocabulary allows PositionSnapshot: this slice emits positional observations only, not fabricated pass/tackle outcomes.
- 120 repeatable frames, 0.5 seconds apart. Two defenders move periodically across passing lanes; other players and the ball retain fixed positions. The deliberately constrained scenario exercises blocked and open lanes, rather than simulating a complete football match.
- Analyze same-team receivers 5–40 m from the ball. A defender blocks a lane when their perpendicular projection is strictly inside the ball–receiver segment and their perpendicular distance is <= 2 m. No offside, ball speed, reaction time, reach, pressure, or pass probability model is implied. Distances are calculated at full precision and rounded only for display.
- A detection is a per-frame geometric observation, not proof of intent, an attempted pass prevented, or a unique action over time. Multiple blockers can each receive evidence. No cumulative totals, confidence scores, or counterfactual estimates are produced. Calibration and episode aggregation remain future decisions.
- Every detection includes stable event/defender/receiver IDs, endpoints, defender position, distance and threshold. The UI can freeze the complete event and positional evidence while playback continues. Explanations are templates.
- Start and replay reset to frame zero; pause preserves the frame; resume continues a paused match. The final frame remains visible with completed status. Replay repeats identical events and IDs. All browser clients share the same match and controls. Process restart resets state.
- SignalR sends the complete state on each tick and control. A monotonic revision prevents a late HTTP snapshot overwriting newer hub data. Clients subscribe before fetching their initial snapshot and fetch again after reconnect. No event history or guaranteed delivery is needed because snapshots are complete.

## Material contradictions and remaining decisions
The full-MVP acceptance criteria conflict with the requested phase only in scope, as resolved above. The data model does not define player coordinates, possession identity, action outcomes, or timestamp units; these are specified above. The PRODUCT's causal phrase 'denial' cannot be established from positions alone: the UI explicitly calls it a geometric indicator. A realistic multi-possession scenario, empirical threshold calibration, other detectors, AI explanations, deployment packaging, and multi-user/session isolation remain deferred. No external data or credentials are required for this slice.


## Interactive counterfactual slice
- Select and drag a defending player on the existing pitch. The first drag freezes the displayed observation for a controlled comparison; incoming SignalR snapshots continue independently.
- Only the selected defender changes. Selecting another defender starts a fresh hypothetical move against the same frozen actual observation. The hollow marker retains the actual position, the purple marker and connector show the hypothetical position, and all other positions remain from the labeled frozen event. Reset discards the scenario and displays the latest live observation.
- POST /api/counterfactual accepts a frame, defenderId and position, and returns before/after candidate lanes with blocker IDs. This stateless endpoint has no playback dependency. The pure CounterfactualAnalyzer copies positions and reuses PassingLaneAnalyzer, including its shared 5–40 m candidate rule and 2 m corridor.
- A lane is covered if any defender blocks it; multiple blockers count as one covered lane. Gold dashed lanes are covered; green solid lanes are uncovered. The comparison table reports changes and blockers. Hypothetical coverage is geometric, not a predicted pass outcome.
- No AI, cloud resources, dependencies, or live-state mutations were introduced.
