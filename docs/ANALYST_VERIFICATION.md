# Repeated uncached Analyst verification — 2026-10-24

## Current implementation: separate facts and commentary (v4.3)

Analyst now receives qualitative structured evidence rather than quantities to
transcribe. C# remains authoritative for full semantic measurements/units,
actual/previous snapshots, significance points/factors and supporting event IDs.
These are returned in `verifiedFacts` for generated, cached, mock and fallback
responses, and the UI displays server measurements before any inference.
Foundry writes original commentary and cites fact IDs. The backend validates
those IDs and resolves event references. No exact number, event ID or prescribed
observation/derivation phrase is required in Analyst prose. Optional numerical
claims still face the existing relationship/value/unit checks.

The final validator also rejects explicit invented event types, unknown or
misassigned player identities/roles, incorrect event actors/pass targets,
invented pressure agents, physical barriers, predictive effects and hedged
causal claims. Narrow explicit uncertainty predicates are distinguished from
positive assertions; regression tests ensure a disclaimer cannot hide a separate
causal assertion. Casual prompts, output schema and validation behavior remain
unchanged. Button initiation, rate limiting, fallback, bounded caching and
deduplication remain in place. Prompt/cache version is `v4.3`.

### New diagnostic batch and final validation

The new batch generated 15 uncached Analyst responses on the same five ranked
moments, three rounds, 650 output tokens, 15-second timeout, 20-second pacing,
using local Azure credentials and the existing deployment. All 15 responses
completed; there were no Foundry HTTP failures or timeouts. Generation used the
qualitative v4.2 prompt. v4.3 retains that identical inference prompt/payload and
adds the final validation corrections/cache version. All exact responses were
replayed through v4.3; the results below are those final decisions, not the
earlier intermediate validator's decisions.

**7 accepted; 8 rejected.** The original baseline was 4 accepted/11 rejected.
This small sample does not establish a future success rate or statistical
improvement, and fallback remains frequent.

| UI rank | Moment (trigger / defender) | Accepted / attempted | First rejection categories |
| --- | --- | --- | --- |
| 1 | PassingLaneDenial (077 / B5) | 2/3 | unsupported physical/predictive claim ×1 |
| 2 | DefensiveRecovery (084 / B5) | 1/3 | unsupported causation ×2 |
| 3 | AttackingRedirection (012 / B1) | 2/3 | pressure actor unrecorded ×1 |
| 4 | AttackingRedirection (036 / B3) | 1/3 | pressure actor unrecorded ×1; unsupported event ×1 |
| 5 | AttackingRedirection (060 / B4) | 1/3 | unsupported causation ×1; pressure actor unrecorded ×1 |

Totals: `unsupported_causation` 3; `pressure_actor_unrecorded` 3;
`unsupported_physical_or_predictive_claim` 1; `unsupported_event` 1. There were
no numerical-grounding, spelled-measurement or mandatory-wording rejections in
the final batch. These are first failures, not exhaustive semantic judgments.
The all-generated live gate still correctly fails whenever any response rejects.

All 22 prior diagnostic fixtures and the original two-audience captured fixtures
are replayed, together with all 15 new responses. Expected v3 outcomes remain in
the old fixture file beside explicit v4 outcomes. Under final checks, the 22
prior diagnostic samples yield 12 accepted/10 rejected; removing mandatory
wording does not excuse incorrect numerical claims. New responses and final
decisions are in `AnalystV4Regression.json`. For category-only offline replay:

```powershell
$env:NARRATION_REPLAY_LOG = 'tests/Analytics.Tests/live-probe-v42.log'
dotnet run --project tests/Analytics.Tests --no-build
Remove-Item Env:NARRATION_REPLAY_LOG
```

Local build: zero warnings/errors; all 244 backend checks passed. Frontend build
and all four tests passed. The exact non-root production image passed local
smoke, visitor/SignalR isolation, fallback/authoritative-fact preservation,
invalid inputs/rate limiting, and API-backed frontend lifecycle tests.

The validator is still conservative and lexical, not a proof of semantic
entailment. Unrecognized paraphrases, implicit causal claims, subtle snapshot
substitutions and invented nonnumeric details may escape it. Known harmless
phrasing can still fall back. Production identity/ingress and cache reuse require
the separate public checks recorded in DEPLOYMENT.md.

## Historical v3.2 baseline

The user verified that `invisible-defender--analyst-final` is both latest and ready,
with 100% of production traffic. An outdated revision is excluded from this
investigation. No routing, revision, infrastructure, identity, deployment, prompt,
token limit or production diagnostic setting was changed for these probes.

The existing public integration suite accepts cached output and selects one moment.
It verifies the public endpoint/cache path, but cannot establish repeated uncached
inference reliability. The expanded opt-in backend probe calls the existing Foundry
deployment directly through the application adapter, then the same validator.
It uses all five deterministic UI-ranked moments, prompt v3.2, 650 output tokens,
and a 15-second timeout. Each attempt is independent and bypasses the application
cache. Authentication is local `DefaultAzureCredential`, so this does not repeat
the production ingress or managed-identity checks.

## Reproduction

With existing Azure CLI sign-in and the existing endpoint/deployment configured:

```powershell
dotnet build src/api/InvisibleDefender.slnx
$env:NARRATION_LIVE_CHECK = '1'
$env:NARRATION_LIVE_ROUNDS = '3'
$env:NARRATION_LIVE_DELAY_SECONDS = '20'
# Optional synthetic response capture for qualitative review:
$env:NARRATION_LIVE_PROSE = '1'
dotnet run --project tests/Analytics.Tests --no-build
Remove-Item Env:NARRATION_LIVE_CHECK,Env:NARRATION_LIVE_ROUNDS,Env:NARRATION_LIVE_DELAY_SECONDS,Env:NARRATION_LIVE_PROSE
```

This deliberately consumes inference tokens. The probe completes all attempts and
prints per-moment/per-rule summaries before failing the gate if any attempt fails.
It never retries rejected prose automatically or treats a cached success as a new
generation. `LIVE_RESULT` includes the evidence fingerprint for comparison.

## Findings

The initial rapid 15-attempt run produced 7 complete model responses: 3 validated
and 4 failed validation. Another 8 calls received HTTP 429 from Foundry. Those
transport failures are separate from validation failures and are excluded from
the completed-response denominator. A second run adds 20 seconds between calls.

The paced run completed all 15 model responses without HTTP failures or timeouts:
**4 validated, 11 rejected**. The live gate failed as intended. This is a small
stochastic sample, not an estimated future failure probability.

| UI rank | Moment (trigger / defender) | Validated | First rejection categories |
| --- | --- | --- | --- |
| 1 | PassingLaneDenial (077 / B5) | 0/3 | numeric_grounding ×2; analyst_observation ×1 |
| 2 | DefensiveRecovery (084 / B5) | 2/3 | analyst_derivation ×1 |
| 3 | AttackingRedirection (012 / B1) | 1/3 | numeric_grounding ×2 |
| 4 | AttackingRedirection (036 / B3) | 1/3 | numeric_grounding ×1; spelled_measurement ×1 |
| 5 | AttackingRedirection (060 / B4) | 0/3 | numeric_grounding ×1; analyst_observation ×2 |

Aggregated first rejections: `numeric_grounding` 6, `analyst_observation` 3,
`analyst_derivation` 1, `spelled_measurement` 1. Five of the six numerical
rejections belong to `explanation`; one belongs to `title`. The corrected
diagnostic changes four previously mislabelled fields from title to explanation.
Every captured accept/reject decision remains unchanged after the fix.

Captured failures establish several distinct causes:

- `analyst_derivation`: the explanation contains neither `derived`, `analysis`
  nor `geometr`, which the current lexical distinction check requires.
- `analyst_observation`: the explanation contains neither `observ` nor `record`.
- `numeric_grounding`: a numerical claim uses “this segment” instead of the
  supplied “passing segment” relationship. Other responses put event IDs in
  prose; their numeric suffixes are processed as unsupported numerical claims.
  The prompt directs event IDs into `evidenceEventIds` instead.
  One title uses “Coverage Threshold” with capitals. The current subject check
  requires case-sensitive “coverage threshold”, so this harmless capitalization
  is also conservatively rejected. This investigation did not relax that check.
- HTTP 429: the existing deployment throttled the rapid probe; no quota change
  was made.

These are first rejection categories: validation returns on its first failure.
One sample also says “zero metre”; the existing spelled-measurement rule only
matches plural “metres/meters”, and that sample was rejected earlier for missing
derivation wording. These categories are not an exhaustive semantic audit.

The samples also exposed a diagnostic attribution defect: title and explanation
are joined before numerical validation, so an unpunctuated title shares the first
clause with the explanation. The old diagnostic used the clause start and labelled
some explanation failures `title`. The local fix uses the rejected number's exact
offset, preserves offsets when masking player IDs, and identifies individual
limitations. It leaves numeric acceptance and prompt/cache version unchanged.
This fix has not been deployed.

Captured synthetic responses are preserved in `AnalystProbeRegression.json` with
expected rules and corrected fields. Ordinary tests replay these without Azure
credentials. Passing cached/public checks and isolated successful generations
remain insufficient evidence that the Analyst generation gate is reliable.

Local verification: backend build completed with zero warnings/errors; all 194
backend checks passed, including replay of all 22 completed model responses from
both probes, unchanged accept/reject decisions, evidence fingerprints and exact
numeric rejection fields. The live generation gate remains failing. No production
deployment was performed.

Local verification: backend build completed with zero warnings/errors; all 194
backend checks passed, including replay of all 22 completed model responses from
both probes, unchanged accept/reject decisions, evidence fingerprints and exact
numeric rejection fields. The live generation gate remains failing. No production
deployment was performed.
