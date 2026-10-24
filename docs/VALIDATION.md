# Validation results

- `dotnet build src/api/InvisibleDefender.slnx --no-restore`: passed, zero warnings and errors.
- `dotnet run --project tests/Analytics.Tests --no-build --no-restore`: all 22 analytical checks passed. This dependency-free executable exits nonzero on a failure; it is not a test-adapter project and is not run by `dotnet test`.
- `node tests/Integration/smoke.mjs` against the running API: passed. Checks HTTP health, actual SignalR WebSocket negotiation and JSON handshake, live updates, pause stability, resume, replay repeatability, evidence links, ordered revisions, and invalid-command rejection.
- `npm.cmd --prefix src/web install --cache .npm-cache`: blocked by sandbox network permissions (`EACCES` connecting to registry.npmjs.org). No frontend dependencies were installed and no lockfile could be generated.
- `npm.cmd --prefix src/web run build`: attempted, failed because `tsc` is unavailable without the dependency installation. Frontend type-check, bundle and browser verification remain outstanding; no frontend build success is claimed.

The environment also denies the default user-level NuGet configuration. Backend restore/build succeeded with APPDATA and DOTNET_CLI_HOME pointed at workspace-local ignored folders and `dotnet restore src/api/InvisibleDefender.slnx --configfile NuGet.Config --packages .packages`. Neither backend project needs external packages. Standard README commands should work in a normal local developer environment.

After registry access is available: run `npm.cmd install` in src/web, commit the generated package-lock.json, run `npm.cmd run build`, then open the app and verify playback, player selection, blocked lane overlays, and frozen evidence while live updates continue.


## Counterfactual validation
- Backend solution build: passed with zero warnings/errors.
- Dependency-free analytical suite: all 42 checks passed, including counterfactual opening/closing, boundary conditions, invalid inputs, multiple blockers, source immutability, repeatability and baseline restoration.
- HTTP/SignalR smoke suite: passed, including counterfactual validation, unchanged paused state and continued live delivery during hypothetical analysis.
- Frontend TypeScript/Vite production build: passed. Rollup reports existing SignalR annotation warnings.
- Frontend configuration tests: 3 passed via npm test. Pointer dragging has not been verified in a browser automation test.

## Interactive counterfactual follow-up (2026-10-22)
- Backend build passed with zero warnings/errors; all 42 analytical checks passed. Added differential coverage checks for every defender in all 120 synthetic frames, repeated-scenario source immutability, switching defenders, and segment endpoints.
- Frontend production build and all 3 frontend configuration tests passed. The only build warnings are SignalR dependency annotations removed by Rollup.
- HTTP/SignalR integration smoke passed, including paused-state immutability and continued live playback during counterfactual requests.
- The pitch rejects stale calculation overlays, clears dragging on lost pointer capture, and supports switching the hypothetical defender against the same actual observation. Pointer interactions have not been verified in browser automation.
- Vite now proxies to port 5080, matching the README API command and integration suite.
## Frozen pitch regression (2026-10-22)

Root cause: `scenario.frame` (or an evidence snapshot) took precedence over the
latest SignalR observation, and Resume never cleared that display selection.
The timer read live state while SVG players read the retained snapshot.

The frontend now uses a reducer with explicit `PitchMode`, latest live state,
one frozen observation and a separate hypothetical position override. Scenario
requests are derived from that state; SignalR updates do not replace the frozen
observation or trigger duplicate counterfactual requests. Resume returns to live
immediately and clears the snapshot, override, drag and analysis. Evidence
inspection shares the frozen observation rather than retaining a second frame.
Reset uses the same local transition without sending a playback command.

Validation:
- Backend build: zero warnings/errors; all 42 analytical checks passed.
- Frontend production build passed, with existing SignalR annotation warnings.
- `npm.cmd --prefix src/web test`: configuration checks and App SVG lifecycle test passed.
- With the API running on port 5080, run the same suite with
  `$env:PITCH_API_URL = 'http://127.0.0.1:5080'` to additionally exercise actual
  HTTP and SignalR delivery. This intentionally replays and pauses the local demo.
- Real delivery check passed: pause at 10 seconds, drag B2 into/out of a lane,
  render a real counterfactual comparison, verify unchanged server snapshot,
  Resume, then observe successive SVG B2 Y coordinates of 44.5, 45.454915 and
  46.477358. Resume retained the paused observation before subsequent ticks.
- The deterministic lifecycle also covers new live observations behind the frozen
  display, immediate latest-frame selection on Resume, cleared mode/frame/override,
  source immutability, stale revisions and reset without any playback request.
- `node tests/Integration/smoke.mjs`: HTTP/SignalR integration passed.

The App test executes its actual JSX, handlers, effects and frame selection with
controlled hooks and inspects SVG coordinates; the optional integration feeds
real WebSocket messages through the registered SignalR callback. It does not
exercise browser layout or native pointer capture. No browser was available in
this session for a visual pointer-drag check.


## Scenario replay (2026-10-22)

- Backend solution build passed with zero warnings/errors.
- Analytical executable: all 52 checks passed. Includes deterministic replay,
  possession transfer, carrier/ball consistency, flight segments, suppression of
  in-flight lane insights, five defensive contributors, contribution distribution,
  per-scenario evidence and exact event/position references.
- Frontend TypeScript/Vite build passed (existing SignalR annotation warnings).
- Frontend suite: all 4 tests passed with PITCH_API_URL set. Actual App handlers
  cover timeline selection/return, frozen observations, drag, reset and Resume.
  Real HTTP/SignalR delivery verified counterfactual isolation and subsequent
  SVG player movement after resuming the paused observation at 10 seconds.
- HTTP/SignalR smoke passed, including all 25 timeline moments, five scenario
  names, interception/save possession changes and event-to-contribution links.
- No native browser layout/pointer-capture visual QA was performed. The SVG
  lifecycle test renders actual JSX through controlled React hooks.

The regression checks caught a static test-player assumption and excessive
B1/B2 contributions during implementation. Supporting-player positioning was
adjusted; the final analytical suite explicitly checks contribution distribution.
See SCENARIO_REPLAY.md for semantics and football realism limitations.

## Defensive insight engine and replay (2026-10-22)

- Backend solution build: passed, zero warnings/errors. One intermediate build
  encountered the running API executable's Windows file lock; stopping that
  process and rebuilding resolved it. The final build is clean.
- Analytical executable: all 79 checks passed (52 previous and 27 added).
  Covers opening/closure, reopening semantics, recovery, supported redirection,
  unsupported pressure rejection, grouping, reset boundaries, repeated evidence,
  ranking reproducibility, exact event/position links, empty input, and all
  120 playback prefixes excluding future evidence. Static coverage does not
  create an action, and repeated coverage does not increase scores.
- Frontend TypeScript/Vite production build: passed. Existing SignalR dependency
  annotation warnings remain; no new package was added.
- Frontend suite: all 4 tests passed with PITCH_API_URL set to the running API.
  The JSX lifecycle test now verifies ranked insight selection, lane and defender
  highlights, the exact frozen frame, raw timeline navigation, explicit complete
  replay browsing, exclusion of future replay content after returning to live,
  pause/drag/reset/resume and stale revision rejection.
- Real HTTP/SignalR lifecycle: passed. At 10 seconds, pause; browse actual ranked
  insights; select one and verify actual evidence coordinates and trigger ID;
  drag its defender and render a real counterfactual response; reset to the
  paused live observation; verify the server is unchanged; drag B2, Resume,
  then observe SVG B2 Y coordinates 63.36 -> 63.44 -> 63.52 from live messages.
- Extended existing HTTP/SignalR smoke suite: passed. Includes all 120 replay
  events, ranked insight evidence/score integrity, selectable defensive frames,
  live history length, prefix-bounded SignalR insights/supporting observations,
  insight endpoint parity, replay reset and existing playback/counterfactual checks.
- Browser inventory contained no available browser. These tests execute actual
  App JSX, handlers and effects and inspect SVG props with controlled React hooks;
  they do not replace browser visual/layout or native pointer-capture QA.

See DEFENSIVE_INSIGHTS.md for ranking weights, grouping windows, snapshot
selection semantics, endpoint contracts and analytical limitations.

## Foundry narration (2026-10-23)

- `dotnet build src/api/InvisibleDefender.slnx --no-restore`: passed, zero warnings/errors.
- `dotnet run --project tests/Analytics.Tests --no-build --no-restore`: 113 checks passed (79 existing analytics/counterfactual/replay checks and 34 narration checks). Covers evidence construction, both audiences, schema/relevance/evidence-ID/numeric rejection, fallback, timeout, missing configuration, unchanged analytics, fake authenticated v1 transport, server cache reuse across callers, concurrent deduplication, expiration, evidence/deployment/prompt invalidation and invalid-output non-caching.
- `npm.cmd --prefix src/web run build`: passed; existing SignalR annotation warnings remain.
- `npm.cmd --prefix src/web test`: all 4 tests passed. The App lifecycle test now includes explicit-only generation, audience round trips and stale responses, disabled in-flight button, labeled fallback and full simulated playback while generation remains pending.
- Same frontend suite with `PITCH_API_URL=http://127.0.0.1:5080`: all 4 tests passed with real HTTP/SignalR. Includes selecting all five ranked moments without inference and the existing replay, frozen evidence, drag/reset and Resume regressions. B2's SVG Y position advanced 63.36 -> 63.44 -> 63.52 after Resume.
- `node tests/Integration/smoke.mjs`: passed against the API, including real SignalR negotiation/delivery and analytical/counterfactual isolation.
- `node tests/Integration/explanations.mjs`: passed against Provider=Disabled, including both audience fallbacks, preserved evidence, unknown insight/mode/scope/prompt rejection, independent HTTP 429 rate limiting and unaffected health endpoint.
- Live Foundry inference was **not verified**. Automated tests require no Azure credentials. No cloud deployment or resource provisioning occurred. Native browser layout and pointer-capture QA was not performed.

See [Foundry configuration and manual verification](FOUNDRY_EXPLANATIONS.md) for endpoint/authentication requirements, cache lifetime/size/invalidation and limitations of prose validation.


## Foundry evidence grounding follow-up (2026-10-23)

- Changed only narration implementation, narration regression checks and Foundry documentation in this follow-up. Playback, analytics, SignalR, counterfactual tools, cache architecture and rate limiting are unchanged.
- Prompt version is `v2`; the existing cache key includes this version, so `v1` entries cannot be returned. Existing expiry, concurrency deduplication and invalid-output non-caching checks pass.
- Foundry receives measurement semantics, epistemic categories and an evidence-specific approved narrative catalogue. Deterministic validation requires exact approved factual prose, units and values, complete evidence references, strict shape and audience limits. Non-catalogue wording falls back. This deliberately constrains Foundry to editorial selection rather than unrestricted prose generation.
- `dotnet build src/api/InvisibleDefender.slnx --no-restore`: passed, zero warnings/errors.
- `dotnet run --project tests/Analytics.Tests --no-build --no-restore`: **143 checks passed** (79 analytics and 64 narration checks). Added 30 checks covering all current insight/audience combinations, player-versus-segment distance, zero/contact, threshold/width, wrong units, derived-versus-observed claims, causal claims/intent, verbosity, analyst values, foreign IDs, old-version cache isolation and actual transport payload semantics/catalogue.
- `npm.cmd --prefix src/web run build`: passed; existing SignalR Rollup annotation warnings remain.
- `npm.cmd --prefix src/web test`: all 4 passed, both standalone and with `PITCH_API_URL=http://127.0.0.1:5080` (real HTTP/SignalR lifecycle).
- `node tests/Integration/smoke.mjs`: passed.
- `node tests/Integration/explanations.mjs`: passed with Provider=Disabled. Initial attempt encountered Windows Event Log write permission failure on deliberately invalid input; rerun passed with `Logging__EventLog__LogLevel__Default=None` set only for the local API test process. No application logging configuration changed.
- Live Azure inference was not exercised; no resources were provisioned or deployed. All Foundry transport tests use fake credentials/HTTP.


## Original Foundry narration v3 (2026-10-23)

- Replaced approved paragraph selection with original generation from backend semantic facts; fallback catalogue retained locally.
- Prompt/cache version v3 isolates v2 entries. Existing on-demand UI, authentication, transport, deduplication, cache and rate limits retained.
- Backend executable: 156 checks passed, including original prose acceptance, fact references, numerical relationship rejection, audience separation, fallback, cache and transport payloads.
- Backend build: passed without restore, zero warnings/errors. Restore was unnecessary; the default user NuGet configuration is inaccessible in this sandbox.
- Frontend production build passed (existing SignalR Rollup annotation warnings); standalone frontend tests: 4 passed.
- HTTP/SignalR smoke and explanation validation/fallback/rate-limit integration checks passed against a local Disabled-provider API.
- API-backed frontend tests: 4 passed, including real SignalR playback lifecycle.
- No live Foundry request completed; prose quality and actual deployment compatibility remain unverified. No deployment or provisioning performed.
- Validation is structural/numeric plus conservative lexical screening. It cannot guarantee full semantic entailment for unrestricted prose; see FOUNDRY_EXPLANATIONS.md.
