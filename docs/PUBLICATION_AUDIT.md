# Public submission audit — 2026-10-25

## Scope and Git baseline

Read AGENTS.md, README.md and the product, architecture, data model, acceptance, initial slice, replay, defensive insight, Foundry, deployment and validation specifications. Changes are documentation and ignore rules only; application behavior, Azure infrastructure and repository visibility are unchanged.

The initial working tree was clean. `main` tracked `origin/main`, both at `a811a01`; no other branches were listed. Origin fetch/push URL: `https://github.com/andrelashley/invisible-defender.git`. The repository is not shallow. `git fsck --full --no-reflogs` reported no issues. No fetch, push, deployment, visibility change or history rewrite was performed.

All four existing commits were audited:

| Commit | Description | Files in snapshot |
| --- | --- | ---: |
| `1bc8397` | Initial Invisible Defender implementation | 34 |
| `c1d02cc` | Grounded Foundry narration v3.1 | 39 |
| `cebcb0b` | Deployment configuration | 46 |
| `a811a01` | Analyst commentary / verified evidence separation | 53 |

## Findings

- Enumerated every tracked path in every snapshot and scanned all **83 distinct Git blobs**, including earlier file versions. Checked credential assignments, Azure/Foundry authentication values, bearer/JWT/GitHub tokens, SAS signatures, storage keys, private keys, local absolute paths and high-entropy strings. Package integrity hashes and image digests were excluded from entropy alerts. Reviewed authentication/configuration references, deployment scripts/docs and regression fixture contents. No committed credential, access token, private key or developer-specific absolute path was identified. This targeted audit is not a guarantee against every possible secret format; a dedicated secret scanner was not installed.
- No cache, generated build output, deployment log, temporary validation file or local environment configuration was tracked in any of the four snapshots. `git ls-files -ci --exclude-standard` returned no paths before or after the ignore changes. Therefore no `git rm --cached` operation was necessary.
- Local deployment/backend/Foundry logs, `.artifacts/`, development appsettings, package caches, build directories and node_modules are present but ignored and untracked. Their contents were not copied into this report or staged.
- `appsettings.json` has empty endpoint/deployment values and disables narration. Launch settings contain localhost URLs only. NuGet configuration points to the public feed without credentials. Authentication uses Azure.Identity; tokens are obtained at runtime, not stored in source.
- Preserved `LiveNarrationRegression.json`, `AnalystProbeRegression.json` and `AnalystV4Regression.json`: these contain synthetic model prose/evidence and expected validation outcomes, are referenced by the test project, and support offline regressions. They contain no identified authentication material.
- Historical deployment documentation exposes resource names, a Foundry endpoint, managed identity principal and role-assignment identifiers, image/revision details and the public demo URL. These are resource identifiers, not credentials. Commit metadata exposes the author's name and personal email in all four commits. Owner review of this public exposure remains necessary. Removing current text alone would not remove historical values; no history rewrite was attempted.
- Strengthened ignore coverage for local agent/cloud configuration, environment-specific appsettings (with example exceptions), private-key/certificate files, binary logs, test/coverage output and OS artifacts. Synthetic JSON fixtures and useful source files remain trackable.
- README now links the recorded Azure demo, explains synthetic evidence, counterfactual geometry, Foundry grounding, architecture, local setup, tests and limitations. Screenshot slots are explicitly placeholders. No license was added because none was explicitly selected.

## Verification performed in this audit

| Check | Result |
| --- | --- |
| Backend solution build | Passed; zero warnings/errors |
| Backend executable suite | All 244 checks passed |
| Frontend TypeScript/Vite build | Passed; existing SignalR PURE-annotation warnings only |
| Frontend test suite | All four tests passed |
| Local API HTTP/SignalR smoke | Passed; playback/replay, evidence, counterfactual validation/isolation |
| Local Disabled-provider explanation integration | Passed; validation, evidence-preserving fallback and rate limits |
| API-backed frontend suite | All four tests passed |

The sandbox initially blocked user-level NuGet configuration and Windows runtime access. The approved build and local-only API execution succeeded outside that restriction. The local API used `Narration__Provider=Disabled`; no live model inference was requested. Missing API wwwroot produced the expected warning for a source-only API run: frontend assets were verified through the separate Vite build. No fresh production container, Azure health, live Foundry, native pointer-capture or screenshot verification was performed. Historical validation records are retained as historical evidence, not new test results.

## Publication steps and unresolved decisions

1. Review this change and the personal email/resource identifier exposure above. If any actual credential is subsequently discovered, rotate/revoke it first and obtain approval for any history cleanup before publication.
2. Explicitly choose a license and check hackathon submission requirements. A license decision is pending; public visibility alone does not make the project open source.
3. Review the development dependency advisories previously recorded in DEPLOYMENT.md. Dependency vulnerabilities were not reassessed or dependencies changed during this credential/artifact audit.
4. Manually check the live demo and native dragging, then replace screenshot placeholders if desired. Known AI fallback frequency and lexical-validator limitations are disclosed; historical 7/15 accepted responses are not a future success guarantee.
5. Review the prepared commit, push it to the existing origin when authorized, and change visibility only after explicit approval. Do not redeploy the already working Azure application as part of publication.

No credential-removal blocker was identified. License selection, owner privacy review and explicit visibility approval remain pending.
