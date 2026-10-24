# Production deployment and judging runbook

## Current release: Analyst facts/commentary separation (2026-10-24)

Final revision: **`invisible-defender--analyst-v43`**. Latest and ready both match;
the revision is Healthy/Provisioned with one replica and 100% latest-revision
traffic. Image: `invdefenderde31c4da.azurecr.io/invisible-defender:analyst-v43-20261024164952`.
Image digest: `sha256:2668f9f8a805573ce5ca9bcc30eb34b89740c2a40f747c69220203358eb096d6`.

Only the image/revision was updated. Existing Container App, registry, environment,
identity, RBAC, model deployment, resource allocation, single-revision mode and
routing settings were reused. Production mode and `Narration__DiagnosticsEnabled=false`
were confirmed after rollout. No Azure resources were provisioned.

Before deployment, all 244 backend checks, frontend build/four tests and exact
non-root container smoke, production/isolation, explanation fallback/facts/rate-limit
and API-backed frontend tests passed. The 15-response diagnostic batch revalidated
through the final safety checks accepted 7 and rejected 8; see
[counts, rejection reasons and limitations](ANALYST_VERIFICATION.md).

Public verification on the new ready revision passed:

- `AUDIENCES=analyst REQUIRE_GENERATED=1 INSIGHT_INDEX=0` with
  `foundry-public.mjs`: first request was **generated**, followed by identical
  **cached** responses for repeat and independent visitor. Full server-owned
  actual/previous measurements, significance, event references and fact IDs matched
  domain evidence. Invalid input checks passed.
- A further read of the same cache entry retained the narrative and authoritative
  facts; synthetic response captured locally at `.artifacts/public-analyst-v43.json`.
- Public smoke, production assets, SPA refresh, real SignalR/WebSocket delivery,
  visitor isolation and all four API-backed frontend lifecycle tests passed.
- Prior revision `invisible-defender--analyst-final` remains retained, inactive,
  with image `invdefenderde31c4da.azurecr.io/invisible-defender:analyst-fix-20261024`.

Rollback remains available using the retained previous image (do not delete it):

```powershell
az containerapp update -g rg-football-hackathon -n invisible-defender --image invdefenderde31c4da.azurecr.io/invisible-defender:analyst-fix-20261024 --revision-suffix rollback-analyst-v43
```

A rollback/restart clears process-local sessions and cache. No rollback was executed.
Public success does not establish universal generation reliability: the final
diagnostic still rejected 8/15 outputs safely, and lexical guards are not a general
semantic entailment proof.

## Earlier analyst-final investigation

## Current release: Analyst facts/commentary separation (2026-10-24)

Final revision: **`invisible-defender--analyst-v43`**. Latest and ready both match;
the revision is Healthy/Provisioned with one replica and 100% latest-revision
traffic. Image: `invdefenderde31c4da.azurecr.io/invisible-defender:analyst-v43-20261024164952`.
Image digest: `sha256:2668f9f8a805573ce5ca9bcc30eb34b89740c2a40f747c69220203358eb096d6`.

Only the image/revision was updated. Existing Container App, registry, environment,
identity, RBAC, model deployment, resource allocation, single-revision mode and
routing settings were reused. Production mode and `Narration__DiagnosticsEnabled=false`
were confirmed after rollout. No Azure resources were provisioned.

Before deployment, all 244 backend checks, frontend build/four tests and exact
non-root container smoke, production/isolation, explanation fallback/facts/rate-limit
and API-backed frontend tests passed. The 15-response diagnostic batch revalidated
through the final safety checks accepted 7 and rejected 8; see
[counts, rejection reasons and limitations](ANALYST_VERIFICATION.md).

Public verification on the new ready revision passed:

- `AUDIENCES=analyst REQUIRE_GENERATED=1 INSIGHT_INDEX=0` with
  `foundry-public.mjs`: first request was **generated**, followed by identical
  **cached** responses for repeat and independent visitor. Full server-owned
  actual/previous measurements, significance, event references and fact IDs matched
  domain evidence. Invalid input checks passed.
- A further read of the same cache entry retained the narrative and authoritative
  facts; synthetic response captured locally at `.artifacts/public-analyst-v43.json`.
- Public smoke, production assets, SPA refresh, real SignalR/WebSocket delivery,
  visitor isolation and all four API-backed frontend lifecycle tests passed.
- Prior revision `invisible-defender--analyst-final` remains retained, inactive,
  with image `invdefenderde31c4da.azurecr.io/invisible-defender:analyst-fix-20261024`.

Rollback remains available using the retained previous image (do not delete it):

```powershell
az containerapp update -g rg-football-hackathon -n invisible-defender --image invdefenderde31c4da.azurecr.io/invisible-defender:analyst-fix-20261024 --revision-suffix rollback-analyst-v43
```

A rollback/restart clears process-local sessions and cache. No rollback was executed.
Public success does not establish universal generation reliability: the final
diagnostic still rejected 8/15 outputs safely, and lexical guards are not a general
semantic entailment proof.

## Earlier analyst-final investigation

Status (2026-10-24, narration follow-up): final revision `invisible-defender--analyst-final` runs image `invdefenderde31c4da.azurecr.io/invisible-defender:analyst-fix-20261024` with `ASPNETCORE_ENVIRONMENT=Production` and `Narration__DiagnosticsEnabled=false`. Fresh public Analyst generation, both-audience repeated and independent-visitor caching, playback, WebSockets, visitor isolation and API-backed frontend lifecycle passed. The original revision `invisible-defender--0000009` remains retained and inactive for rollback (verified by revision show; use revision list --all to include inactive revisions). No resources, identity, RBAC or billing settings were changed.

Additional verification: the user confirmed latest=ready=`invisible-defender--analyst-final` and 100% traffic on that revision. Repeated uncached Analyst probes across all five ranked moments still reproduce validation rejections. The earlier isolated success/cache checks do not establish generation reliability. See [per-moment investigation](ANALYST_VERIFICATION.md). No routing or infrastructure changes were made; the local diagnostic attribution fix is not deployed.

The diagnostic investigation confirmed complete HTTP 200 responses rejected locally for `spelled_measurement`; a separate older `analyst_requirements` rejection remains unclassified beyond that composite rule. Prompt v3.2 repeats the supplied numeric coverage threshold for Analyst; validation acceptance and the 650-token limit remain unchanged. See [diagnosis and limits](FOUNDRY_EXPLANATIONS.md#production-analyst-investigation-2026-10-24). Casual's unchanged prompt still occasionally produces correctly rejected output. Successful inference does not guarantee every stochastic response will validate. Final console verification found zero custom narration diagnostic lines. Native browser pointer-capture QA remains separate.

## Original deployment results (2026-10-24, before narration follow-up)

Public URL: https://invisible-defender.purplemushroom-8d506c25.westus3.azurecontainerapps.io

Created only these resources in `rg-football-hackathon`, West US 3, under the existing enabled `Azure subscription 1`:

| Resource | Name | Verified configuration |
| --- | --- | --- |
| Container Registry | `invdefenderde31c4da` | Basic; admin user disabled; image pushed locally |
| Container Apps environment | `invisible-defender-env` | Consumption profile only; no Log Analytics workspace |
| Container App | `invisible-defender` | Healthy; single active revision; one actual replica; min=max=1; 0.25 vCPU/0.5 GiB; external HTTPS only, port 8080, Auto transport |

Final image: `invdefenderde31c4da.azurecr.io/invisible-defender:20261024213458`. Final verified ready revision: `invisible-defender--0000009`. Production configuration includes the exact PublicOrigin, Foundry provider, existing `/openai/v1/` endpoint, `gpt-4.1-mini-1` and ManagedIdentity=true. No billing/subscription type was changed. No new model, database, workspace or other service was created.

System identity principal: `ea8ee73e-65a7-430e-a490-267f9f6ba177`. Verified grants: AcrPull on this registry and Cognitive Services OpenAI User on the existing Foundry account only. Role assignment IDs end in `b50737ce-f623-48cd-9f2b-411b13acf591` and `af3199a2-9b3c-4dbb-b89c-98b3b96eed62`, respectively; query full IDs before cleanup.

Verification performed:

- Production Docker build and non-root container execution passed. Existing deployment files were retained; PowerShell native flag/stderr handling and public ingress readiness waiting were corrected.
- Backend build passed with zero warnings/errors; all 163 ordinary backend checks passed. Frontend production build and four frontend tests passed.
- Local and public smoke passed: health, real SignalR WebSocket handshake/delivery, start/pause/resume, repeatable replay, prefix evidence, counterfactual validation/isolation, and live updates during analysis.
- Local and public production suite passed: compiled React assets, nested SPA refresh, API/hub 404s, rejected foreign origins, sanitized errors, independent REST state and WebSocket broadcasts for two visitors.
- Public API-backed frontend lifecycle passed after replacing a 120 ms test-only timing assumption with a bounded condition wait. Actual JSX handlers rendered ranked evidence, hypothetical comparisons and resumed player movement: B2 Y 63.36 -> 63.44 -> 63.52. No automatic explanation request occurred during insight selection.
- Public outage test passed on the final image with Provider=Disabled: both audience fallbacks retained evidence; invalid input was rejected; rate limiting returned 429; health/playback and the full frontend lifecycle stayed functional. Foundry was restored in a finally block and its final configuration was verified.
- Public testing exposed requests distributed across proxy IP partitions. Added and tested a process-wide 12/minute ceiling alongside existing IP/concurrency limits; a burst with rotating forged forwarded addresses cannot bypass it. This is a shared demo ceiling, including cache hits, not a monetary spending cap.
- Live Casual Fan output passed validation and repeat requests returned identical cached narratives, including an independent visitor. Public Analyst generation repeatedly returned fallback. The strict `foundry-public.mjs` suite correctly exits nonzero; full two-audience public AI verification is **not passed**. Initial backend transport logs showed HTTP 200 on the rejected calls. Local opt-in diagnostics produced valid Analyst prose but also reproduced rejected Casual geometry wording. No validator or prompt was weakened; inference and prose compliance remain variable.

Remaining issues: public Analyst generation/fallback reliability; native browser layout/pointer capture could not be tested because browser inventory was empty. npm audit reports existing Vite development-server advisories; Vite/Node are build stages only and excluded from the final runtime. The app and registry remain running/billable under the approved plan; cleanup is not automatic.

## Architecture and isolation

One ASP.NET Core .NET 10 process hosts Vite production assets from `wwwroot`, `/api`, and `/hubs/match`. Node 24 and SDK 10 are build stages only. The final ASP.NET 10 image runs as its built-in non-root `APP_UID`. `ASPNETCORE_HTTP_PORTS` defaults to 8080; Azure ingress targets 8080. Frontend fetches and SignalR use same-origin relative URLs. SPA routes fall back to index.html; missing API/hub/assets return 404.

Playback was process-global before this change. It now uses a cryptographically random, HttpOnly, SameSite=Strict session cookie and per-session SignalR groups. Independent browser visitors cannot pause/reset one another. Tabs in the same browser cookie profile intentionally share a session. Separate browser profiles/incognito contexts test independent visitors. Refresh retains state while the cookie exists. Sessions expire after one hour without a REST/negotiation request; a connected hub alone does not renew this lease. Reload after expiry. A restart/revision clears sessions and cache. At most 128 sessions are retained; further visitors receive 503 until expiry. No authentication or persistence is introduced. The singleton timer advances independent states every 500 ms. Analytics and narration prompts remain unchanged.

The narration cache remains shared across sessions, bounded and deduplicated. AI remains explicitly button-triggered. No AI calls occur on load, playback, selection, or replay. Only server-known insight IDs/audiences/scopes enter the existing grounding pipeline. Existing timeout (15 seconds), cache (256 entries/3600 seconds), prompt v3.1, validation and fallback remain. Limits are 12 explanation requests per process/minute, 12 per remote IP/minute and 8 concurrent requests. The process-wide ceiling was added after public testing showed rotating ACA proxy addresses could spread requests across IP partitions. Untrusted forwarded headers cannot bypass the global ceiling. Playback is exempt. These limits are not a monetary spending cap.

## Discovered resource inventory

Read-only Azure CLI discovery on 2026-10-24 confirmed:

| Item | Result |
| --- | --- |
| Subscription | Existing enabled personal subscription, display name `Azure subscription 1`; discover ID with `az account show` |
| Resource group | `rg-football-hackathon` (resource-group metadata location westus2) |
| Foundry account | `drellybochelly-2601-resource`, kind AIServices, westus3, public network enabled |
| Existing project | `drellybochelly-2601` beneath that account |
| Existing deployment | `gpt-4.1-mini-1`, GPT-4.1 mini version 2025-04-14, GlobalStandard capacity 10, Succeeded |
| OpenAI inference endpoint | `https://drellybochelly-2601-resource.openai.azure.com/openai/v1/` from account endpoint metadata |
| Hosting/registry/Log Analytics in subscription | None; subscription-wide inventory also checked |
| Azure CLI containerapp extension | Not installed |

No personal subscription ID or credential is hardcoded. The deploy script rediscovers the endpoint and deployment and verifies the selected subscription. It will stop on ambiguous account/registry/environment selection. Review suitable resources outside this RG manually before creating new ones; the script's reuse scope is the selected RG.

## Approved billable plan

The user explicitly approved this plan on 2026-10-24 for the existing personal subscription and resource group. A material infrastructure or cost change requires new approval.

Create only a Basic ACR (globally unique name chosen at execution), one Consumption-capable environment `invisible-defender-env` in westus3, and one app `invisible-defender`. Allocate 0.25 vCPU/0.5 GiB, minimum=maximum=1 during judging, single active revision. Use external HTTPS ingress with automatic HTTP/WebSocket transport. No separate frontend, new model, database, Redis, AKS, Front Door, VNet or dedicated compute. Use platform metrics and live log streaming; `--logs-destination none` avoids provisioning Log Analytics. Logs are not retained for historical queries.

Compute and memory accrue per second at active/idle rates, plus requests and egress. For scale, illustrative USD rates of $0.000024/vCPU-second and $0.000003/GiB-second imply about $0.65/day or $19.44/30 days if continuously active at this allocation, before free allowances. Basic ACR is approximately $5/month (daily prorated), plus excess storage/egress if applicable. Four continuously active days would be roughly $3.30 combined before allowances, excluding inference/requests/egress. These are planning estimates, not a quote; verify westus3/account/currency pricing before approval. The subscription-wide monthly free grants may already be used. Foundry charges depend on actual input/output tokens for user-triggered uncached requests; model failures can still consume tokens. This script builds locally, so it does not add paid ACR Tasks builds. Minimum replicas=1 can still incur idle charges. Budget alerts do not stop spending.

References: [ACA billing](https://learn.microsoft.com/en-us/azure/container-apps/billing), [ACA pricing](https://azure.microsoft.com/en-us/pricing/details/container-apps/), [ACR pricing](https://azure.microsoft.com/en-us/pricing/details/container-registry/).

## Local container gate

Install/start Docker with a Linux container engine before proceeding. Do not substitute Azure deployment for this gate.

```powershell
npm.cmd --prefix src/web ci
dotnet build src/api/InvisibleDefender.slnx
dotnet run --project tests/Analytics.Tests --no-build
npm.cmd --prefix src/web run build
npm.cmd --prefix src/web test
docker build --pull -t invisible-defender:local .
docker run --rm --name invisible-defender-local -p 127.0.0.1:8080:8080 -e Narration__Provider=Disabled invisible-defender:local
# Another terminal:
$env:API_URL='http://127.0.0.1:8080'
node tests/Integration/smoke.mjs
node tests/Integration/production.mjs
node tests/Integration/explanations.mjs
$env:PITCH_API_URL=$env:API_URL
npm.cmd --prefix src/web test
Remove-Item Env:API_URL,Env:PITCH_API_URL
```

Open http://localhost:8080 and complete browser checks below with deterministic fallback. Production cookies are Secure; localhost browser treatment is useful for this local-only HTTP check. No Azure credentials are mounted or passed to the container. `.dockerignore` excludes environment files, local secrets/settings, caches, node_modules, .git, build outputs, tests and docs.

## PowerShell deployment

Use PowerShell and Azure CLI signed into the intended personal subscription. The account needs resource creation permission plus role assignment permission at ACR and the existing Foundry account. No runtime personal credentials are used.

```powershell
az account list --query '[].{name:name,id:id,state:state}' -o table
# Copy the intended ID; do not infer it from a similar display name.
$subscription = '<verified-subscription-id>'
az account set --subscription $subscription
# Read-only plan, including discovered endpoint/deployment:
./scripts/Deploy-Azure.ps1 -SubscriptionId $subscription -RegistryName '<unique-lowercase-acr-name>'
# Only after explicit approval of the billable plan:
./scripts/Deploy-Azure.ps1 -SubscriptionId $subscription -RegistryName '<same-acr-name>' -Apply -BillingApproved
```

Every apply rebuilds/tests locally, runs a Disabled-provider container smoke test, pushes a unique timestamp tag, then updates the same app. The script checks CLI extension/provider registration, reuses selected resources, creates a system-assigned app identity and assigns `AcrPull` at ACR scope and `Cognitive Services OpenAI User` at the existing account scope. For initial private image bootstrapping it briefly creates the same app with the public Microsoft quickstart image and **internal** ingress; it then grants image-pull permission and replaces that image before external ingress. No registry admin password is enabled. The app remains private if bootstrapping fails. RBAC propagation may delay image pull/inference: inspect logs and retry the update, rather than creating another identity/model. Single revision mode is enforced. A new deployment clears in-memory playback and cache; tell judges before redeploying.

The inference role's actual dataActions were inspected and include `Microsoft.CognitiveServices/accounts/OpenAI/deployments/chat/completions/action`. Scope is the AIServices account that owns the deployment, not the Foundry project or entire subscription. [OpenAI identity guidance](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/managed-identity) and [ACR managed identity guidance](https://learn.microsoft.com/en-us/azure/container-apps/managed-identity-image-pull).

## Equivalent Azure CLI sequence

The PowerShell script is the executable source of truth with error checks and discovery. The corresponding core commands below use variables resolved from inventory, not invented endpoints/IDs. Run local container checks before these commands, and obtain billing approval first.

```powershell
az account show
az resource list -g $rg
az extension list
az extension add --name containerapp
az cognitiveservices account show -g $rg -n $foundryName
az cognitiveservices account deployment show -g $rg -n $foundryName --deployment-name gpt-4.1-mini-1
az role definition list --name 'Cognitive Services OpenAI User'
# Only for resources that are absent:
az acr create -g $rg -n $acr -l $location --sku Basic --admin-enabled false
az containerapp env create -g $rg -n $environment -l $location --enable-workload-profiles true --logs-destination none
az acr login -n $acr
docker tag invisible-defender:local $image
docker push $image
az containerapp create -g $rg -n $app --environment $environment --image mcr.microsoft.com/k8se/quickstart:latest --ingress internal --target-port 80 --system-assigned --cpu 0.25 --memory 0.5Gi --min-replicas 1 --max-replicas 1 --workload-profile-name Consumption --revisions-mode single
az containerapp identity assign -g $rg -n $app --system-assigned
# principalId from identity output; resource IDs from acr/account show:
az role assignment create --assignee-object-id $principalId --assignee-principal-type ServicePrincipal --role AcrPull --scope $acrId
az role assignment create --assignee-object-id $principalId --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope $foundryId
az containerapp registry set -g $rg -n $app --server $registryServer --identity system
az containerapp revision set-mode -g $rg -n $app --mode single
az containerapp update -g $rg -n $app --image $image --cpu 0.25 --memory 0.5Gi --min-replicas 1 --max-replicas 1 --set-env-vars ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_HTTP_PORTS=8080 Narration__Provider=Foundry "Narration__Endpoint=$endpoint" Narration__Deployment=gpt-4.1-mini-1 Narration__ManagedIdentity=true
az containerapp ingress enable -g $rg -n $app --type external --target-port 8080 --transport auto --allow-insecure false
# Discover FQDN, then set exact allowed browser origin:
az containerapp show -g $rg -n $app --query properties.configuration.ingress.fqdn -o tsv
az containerapp update -g $rg -n $app --set-env-vars "PublicOrigin=$url"
```

## Production configuration

| Variable | Value |
| --- | --- |
| ASPNETCORE_ENVIRONMENT | Production (no development exception page) |
| ASPNETCORE_HTTP_PORTS | 8080 (override for other hosts) |
| PublicOrigin | Exact public `https://<fqdn>`; validated for REST Origin and WebSocket upgrade Origin |
| Narration__Provider | Foundry; Disabled for deterministic no-credential container tests |
| Narration__Endpoint | Discovered Azure OpenAI `/openai/v1/` endpoint |
| Narration__Deployment | gpt-4.1-mini-1 |
| Narration__ManagedIdentity | true in ACA; local development defaults false/DefaultAzureCredential |

Other narration settings retain existing defaults; see [full configuration reference](FOUNDRY_EXPLANATIONS.md). Browser origins are denied unless they match PublicOrigin; CORS is not enabled. Health probes and nonbrowser CLI clients may omit Origin. Do not set PublicOrigin from arbitrary request headers. No credentials, API keys or tokens are required in the browser, image or repo. The runtime uses ManagedIdentityCredential exclusively in Azure.

## Verification and release checklist

The deployment script prints the HTTPS URL and runs HTTP, real SignalR WebSocket, SPA assets, multi-visitor and deliberate Foundry/cache tests. It fails if Foundry returns fallback instead of validated generated/cached output. A pre-existing cached success is acceptable on redeploy; restart the revision if a fresh inference must be established. Actual public results and the failing Analyst gate are recorded above. Node WebSocket tests use the existing SignalR `ws` dependency for cookie headers; run npm ci first.

```powershell
$env:API_URL='https://<discovered-fqdn>'
node tests/Integration/smoke.mjs
node tests/Integration/production.mjs
node tests/Integration/foundry-public.mjs # deliberately invokes existing model
$env:PITCH_API_URL=$env:API_URL
npm.cmd --prefix src/web test
```

Use two independent browser contexts and record results before public release:

- React loads with no Vite process; refresh `/judge/replay` also loads.
- Events load; SignalR negotiate succeeds and browser Network shows WSS 101/frames.
- Start, pause, resume and replay work; players and ball advance.
- Ranked moments display; select one and verify its exact supporting observation freezes.
- Drag a defender, verify labeled hypothetical marker and lane comparisons; Return to live restores the latest frame.
- Page load, replay, insight selection and audience change make zero explanation requests.
- Explicit Casual Fan and Analyst requests each return generated, supported prose; repeat returns cached with identical narrative.
- Unknown insight/prompt is rejected; rate limiting returns 429 without blocking playback.
- Pause/replay in one browser context does not change the other context's state.
- Temporarily deploy `Narration__Provider=Disabled` (or a known unreachable endpoint), generate fallback while playback continues, then restore Foundry. Do not leave failure settings live. Automated fake-transport timeout tests establish local behavior; this public outage check remains mandatory.

Native browser pointer capture/layout, public WSS, real managed identity and public outage behavior cannot be inferred from local tests. Record their actual outcomes here after deployment.

## Operations and cleanup

```powershell
az containerapp show -g $rg -n $app --query '{state:properties.provisioningState,revision:properties.latestReadyRevisionName,fqdn:properties.configuration.ingress.fqdn}'
az containerapp revision list -g $rg -n $app -o table
az containerapp logs show -g $rg -n $app --type system --tail 100
az containerapp logs show -g $rg -n $app --type console --follow
```

ImagePull failures: check AcrPull scope, identity principal, registry RBAC mode and propagation. Foundry fallback: check discovered v1 endpoint, deployment, ManagedIdentity=true and inference role at account scope; validation rejection can also cause fallback. Production intentionally does not expose custom development narration diagnostics or model prose to logs/API errors. Reproduce with synthetic evidence locally if needed. API 429: wait a minute; proxy-level grouping can affect all visitors. API 503: session capacity reached, wait for idle expiry. WSS/403: verify PublicOrigin exactly matches the HTTPS URL and ingress transport is auto. Missing assets: confirm multi-stage copy into publish/wwwroot; do not run Vite in Azure.

After judging, scale to zero (visitors can wake the app and resume billing):

```powershell
az containerapp update -g $rg -n $app --min-replicas 0 --max-replicas 1
```

For no further app compute, remove the demo app. After verifying they are dedicated to this demo, remove its environment and registry too. ACR continues daily billing even when the app scales to zero. Delete obsolete images to limit storage during redeployments. Remove app-specific Foundry role assignments by their assignment IDs if the identity is deleted. Do not delete the existing Foundry account/project/deployment or the whole resource group.

```powershell
az containerapp delete -g $rg -n $app --yes
az containerapp env delete -g $rg -n $environment --yes
az acr delete -g $rg -n $acr --yes
# For each recorded demo-only role assignment:
az role assignment delete --ids $assignmentId
```

No cleanup command is run automatically. Budget alerts are notifications, not spending stops.
