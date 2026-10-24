# Foundry explanations

The narration slice supersedes earlier documentation that deferred AI. Deterministic analytics, shared playback, replay, and positional counterfactuals remain authoritative and unchanged. No cloud resources are provisioned.

Select a Key Defensive Moment to see its evidence immediately. Choose Casual Fan or Analyst, then click **Generate AI Explanation**. Selection, mode changes, loading, playback and SignalR messages never trigger inference. The button is disabled during that selection's request. Responses are bound to the selection and request revision, so late results cannot appear against another moment or mode. The pitch and playback controls remain usable.

## Backend configuration

Configuration is under `Narration` in ASP.NET Core configuration. Environment variables use two underscores:

| Variable | Default | Purpose |
| --- | --- | --- |
| `Narration__Provider` | `Disabled` | `Foundry`, `Mock`, or `Disabled`. Disabled returns deterministic fallback; Mock uses local templates, consumes no tokens and is labeled Mock. |
| `Narration__Endpoint` | empty | Existing resource's Azure OpenAI **v1 inference endpoint**, ending `/openai/v1/`. Use the deployment's inference endpoint, not a Foundry project URL. HTTPS required. |
| `Narration__Deployment` | empty | Existing GPT-4.1 mini deployment name (may differ from model name). |
| `Narration__ManagedIdentity` | `false` | Set `true` in Azure to exclusively use managed identity. |
| `Narration__ManagedIdentityClientId` | unset | Optional user-assigned managed identity client ID; otherwise system-assigned. |
| `Narration__MaxOutputTokens` | `650` | Clamped to 128–2000. |
| `Narration__TimeoutSeconds` | `15` | Total authentication/inference timeout, clamped to 1–60 seconds. No model retries. |
| `Narration__CacheExpirationSeconds` | `3600` | Absolute expiration, clamped to 1–86400 seconds. |
| `Narration__CacheSize` | `256` | Maximum cached entries, clamped to 1–10000. |

Only set these on the API. Never put credentials or configuration secrets in Vite variables. There is no API-key requirement or API-key storage. For local development, `DefaultAzureCredential` supports Azure CLI (`az login`), developer tooling or standard Azure.Identity environment credentials. In Azure use `ManagedIdentityCredential`, with permission to invoke the existing resource (typically Cognitive Services OpenAI User). Assigning roles or enabling identity is outside this task. Keep any service-principal secret in user secrets/environment configuration, never source control.

The implementation uses `Azure.Identity` 1.17.0 and built-in HttpClient against the [Microsoft Foundry Azure OpenAI v1 chat-completions REST API](https://learn.microsoft.com/en-us/rest/api/aifoundry/azureopenai/chat). This keeps the small existing backend free of extra AI orchestration packages. It requests an Entra bearer token for `https://cognitiveservices.azure.com/.default`, supplies the deployment in `model`, and requires strict `json_schema` structured output. GPT-4.1 mini deployment availability and structured-output support must be verified on the existing resource.

## API and grounding

`POST /api/explanations` accepts only:

```json
{"matchId":"known-match-id","insightId":"known-insight-id","audience":"analyst","scope":"replay"}
```

Audience is `casual` or `analyst`; scope is `live` (default, current prefix) or `replay` (explicit complete synthetic catalogue). Unknown properties, modes, scopes and unknown insights are rejected. The server resolves evidence from its own analytical records; the browser cannot supply evidence, numbers, prompts or model settings. Requesting replay explanations does not seek or restart playback.

The backend constructs semantic facts deterministically from DefensiveInsight measurements and chronological supporting observations. Each fact has a stable ID, observed/derived type, factual statement, supporting event IDs, measurements with subject/relationship/value/unit, and permitted/prohibited interpretations. No scenario IDs are special-cased. Event facts describe recorded actions; moment facts describe the detected mechanism; actual and previous snapshots retain separate distance and coverage facts. Distances are rounded to two decimal places in the supplied semantic representation. No hypothetical facts are emitted because counterfactual evidence is outside this endpoint.

Foundry receives semantic facts and measurement definitions, never approved narratives. It composes original prose using strict JSON output: `title`, `explanation`, `evidenceFactIds`, `evidenceEventIds`, `limitations`. Casual targets 2?3 sentences and 35?60 words; Analyst targets 3?5 sentences and 65?110 words, including relevant measurements and observation/interpretation distinctions. The validator allows five words of upper-bound tolerance. Analyst output requires one separate concise limitation. Existing server-authored narratives remain deterministic fallback and local mock output only.

Server validation checks exact JSON shape, nonempty fields, bounded lengths, unique existing fact/event references and inclusion of each referenced fact's supporting events. Numeric clauses must use a supplied value with its subject, relationship and unit. Player IDs are excluded from numeric parsing. Known wrong player-distance relationships, contact/overlap assertions, unit changes, and common unsupported causal terms are rejected. Validation covers title and limitations as well as explanation. Invalid output returns source `fallback` and is never cached or labelled generated. Mock uses trusted local fallback text and source `mock`.

These are conservative structural, numeric and lexical safeguards, **not a proof of semantic correctness**. They may reject harmless paraphrases and cannot catch all implicit causation, invented nonnumeric actions, spelled-out quantities, or subtle subject/snapshot substitutions. A valid fact citation does not prove every sentence faithfully entails that fact. No semantic confidence score is assigned. Prompts carry the full relationship constraints; deterministic rejection catches known errors, while open-ended prose still needs qualitative review with the real deployment.

## Cache and cost controls

The singleton explanation service owns a bounded, process-local `MemoryCache`. Keys include match ID, insight ID, audience, SHA-256 stable evidence fingerprint, provider, endpoint, deployment identifier, and `FoundryNarrator.NarratorPromptVersion` (`v3`). Increment that constant whenever prompts, grounding or output requirements change. Changes to any evidence, model/deployment or prompt version cause a new key. Old entries remain bounded and expire naturally.

Only validated Foundry successes enter the cache. Fallbacks, failures, timeouts, invalid generations and local mock responses are not cached as AI. Concurrent identical requests share a `Lazy<Task>` operation. Cache is checked before generation and again inside the shared operation to avoid completion races. Disconnecting one caller does not cancel another caller's shared inference; the total timeout still bounds work. Cached results are shared by browser refreshes and separate sessions, with no browser storage dependency. Expiration triggers no inference by itself: a fresh explicit request is required.

Cache is cleared by process restart and is not shared across instances. No Redis, persistence, database, credentials or bearer tokens are stored in it. Rate limiting applies before the cache: 12 requests per remote IP per minute, plus at most 8 concurrent explanation HTTP requests globally, no queue. HTTP 429 asks the user to retry later. Playback endpoints are excluded. Behind a reverse proxy, remote-IP limiting may group viewers; configure trusted forwarded headers for a production host. This public MVP has no user authentication or per-account quota.

## Validation and manual inference

Automated tests use fake narrators, a fake credential and a fake HTTP transport; CI needs no Azure credentials:

```powershell
dotnet build src/api/InvisibleDefender.slnx
dotnet run --project tests/Analytics.Tests
npm.cmd --prefix src/web run build
npm.cmd --prefix src/web test
# Start the API separately on port 5080, then:
node tests/Integration/smoke.mjs
$env:PITCH_API_URL = 'http://127.0.0.1:5080'
npm.cmd --prefix src/web test
# Against an API configured with Provider=Disabled only:
node tests/Integration/explanations.mjs
```

For one deliberate live test using the existing deployment:

1. Authenticate locally with Azure CLI or your developer credential. Set `Narration__Provider=Foundry`, `Narration__Endpoint` to the existing resource v1 inference URL, and `Narration__Deployment` to its GPT-4.1 mini deployment name; restart the API. Do not paste secrets into browser code or logs.
2. Open the UI and browse the complete synthetic replay. Select a ranked moment; confirm deterministic evidence appears and no `/api/explanations` request occurs. Switching modes also sends no request.
3. Click Generate AI Explanation in Casual Fan mode. Inspect its evidence and source. Only **AI generated** verifies completed, validated live inference; **Deterministic fallback** does not.
4. Click again: expect Cached AI explanation. Refresh, reselect the same replay moment/mode and generate: expect the same cached response while the entry remains valid. Another browser session uses the same server cache.
5. Switch to Analyst: no automatic request. Clicking Generate deliberately makes one distinct inference. Verify numerical claims against displayed measurements and IDs, then check playback and counterfactual dragging still work.

Live Foundry inference was verified on 2026-10-23 for both Casual Fan and Analyst against the existing deployment. Local automated mocks and fallback behavior do not establish Azure endpoint, permission or deployment compatibility. No Azure resources were created or deployed. Native browser layout/pointer-capture visual verification remains separate from the JSX lifecycle tests.

## v3 fallback investigation (2026-10-23)

Before behavior changes, real requests returned HTTP 200 and parsed structured JSON.
Initial Casual output failed audience_length on explanation; Analyst failed numeric_grounding.
Further real output reproduced oversized Casual prose, geometry jargon, too many
limitations, missing supporting events for cited moment facts, and causal words in
disclaimers. These are local rejections, not an Azure deployment failure.
The previous catch-all concealed every distinction.

Prompt v3.1 spells out ranges (previous text contained literal question marks),
repeats audience-specific constraints alongside facts, and makes full supporting-event
citation and exact measurement wording explicit. No templates are supplied. The lexical
validator now accepts narrowly specified negative causal predicates, while still rejecting
positive causal assertions elsewhere in the same field. Other grounding rules remain.

Development-only structured logs expose generation start, HTTP call/status, envelope
parsing, rejection rule/field, validation success and fallback use. They contain no
credentials, tokens, response bodies or exception messages. Production emits none of
these custom diagnostics. Default HttpClient framework logging is separate.

Both live audiences passed validation; captured synthetic responses are regression
fixtures. For an explicit repeat with existing credentials/configuration, set
NARRATION_LIVE_CHECK=1 and run the backend check executable. This opt-in probe
calls Foundry once per audience, prints generated prose, and fails on rejection.
Ordinary tests require no credentials. Future noncompliant responses still correctly
fall back without being cached.
