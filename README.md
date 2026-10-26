# Invisible Defender

Football intelligence that makes overlooked defensive positioning visible. Built for the **Microsoft Inside the Game Hackathon**.

**[Open the live Azure demo](https://invisible-defender.purplemushroom-8d506c25.westus3.azurecontainerapps.io)**

A defender can close a passing option without making a tackle or interception. Traditional action counts miss that contribution. Invisible Defender connects positional geometry with recorded events so viewers can inspect the evidence and explore how a different position changes available passing lanes.

## Try it

1. Start playback, or choose **Browse complete synthetic replay**.
2. Select a **Key Defensive Moment** to freeze its pitch evidence. Explore passing-lane denial, attacking redirection and defensive recovery, each linked to supporting events and positions.
3. Drag a coral defender to compare actual and hypothetical lane coverage. A hollow marker preserves the actual position; purple marks the hypothetical position. Counterfactual analysis recalculates geometry, not pass success or goals prevented.
4. Explicitly select **Generate AI Explanation** in Casual Fan or Analyst mode, then return to live playback.

SignalR streams live updates. Visitor cookies isolate playback; tabs sharing a cookie share controls. Ranked moments use transparent heuristic points, not confidence scores.

## Synthetic evidence and grounded AI

All football data is synthetic: five scripted situations, 120 half-second frames, six players per team. Events, outcomes and movement are authored demonstration inputs; lane measurements and insight rules are calculated deterministically in C#. No real match footage, Premier League data or proprietary dataset is included.

**Microsoft Foundry / GPT-4.1 mini** explains supplied evidence on demand. It does not calculate match statistics. Analyst commentary cites fact IDs while server-owned measurements, units and event references remain separate. The backend validates responses, caches accepted explanations, applies request limits, and returns a labeled deterministic fallback when generation is unavailable or rejected. Playback and selection do not trigger AI requests.

Foundry authentication stays on the server: managed identity in Azure and `DefaultAzureCredential` locally. Credentials never enter browser configuration. See [Foundry configuration and safeguards](docs/FOUNDRY_EXPLANATIONS.md).

## Architecture and stack

```text
Synthetic events -> pure C# domain analysis -> ASP.NET Core API / SignalR
                                                 |              |
                                   Foundry explanations     React pitch
```

- **Backend:** ASP.NET Core / C#, .NET 10, dependency-injected domain services.
- **Frontend:** React 19, TypeScript, Vite, SVG pitch and evidence viewer.
- **Hosting:** one Azure Container App serves frontend assets, API and SignalR from one origin. Docker uses Node and .NET SDK build stages.
- **State:** bounded in-memory visitor sessions and explanation cache; no database or authentication in this MVP.

Source: [`src/api`](src/api), [`src/web`](src/web), [`tests`](tests). See [architecture](docs/ARCHITECTURE.md), [analytical rules](docs/DEFENSIVE_INSIGHTS.md), and the [deployment runbook](docs/DEPLOYMENT.md).

## Local development

Install **.NET SDK 10** and **Node.js 22.12+ or 24**. From the repository root, use separate terminals:

```powershell
# Terminal 1: no Azure credentials needed
$env:Narration__Provider = 'Disabled'
dotnet run --project src/api/InvisibleDefender.Api.csproj --no-launch-profile --urls http://127.0.0.1:5080
```

```powershell
# Terminal 2
npm.cmd --prefix src/web ci
npm.cmd --prefix src/web run dev
```

Open http://127.0.0.1:5173. Vite proxies API and SignalR to port 5080. On macOS/Linux use `npm` and `export Narration__Provider=Disabled`. Set `Narration__Provider=Mock` for labeled, token-free explanation templates. Optional live Foundry configuration is documented separately; keep local settings and credentials out of Git.

## Tests and limitations

```powershell
dotnet build src/api/InvisibleDefender.slnx
dotnet run --project tests/Analytics.Tests --no-build --no-restore
npm.cmd --prefix src/web run build
npm.cmd --prefix src/web test
# With the local Disabled-provider API above running:
node tests/Integration/smoke.mjs
node tests/Integration/explanations.mjs
$env:PITCH_API_URL = 'http://127.0.0.1:5080'
npm.cmd --prefix src/web test
Remove-Item Env:PITCH_API_URL
```

The backend suite is an executable test harness: use `dotnet run`, rather than `dotnet test`. See [submission verification](docs/PUBLICATION_AUDIT.md) for current results and [historical validation](docs/VALIDATION.md) for earlier checks. Live Foundry probes are opt-in and consume inference tokens.

This is a bounded demonstration, not a calibrated player evaluation system. Coarse scripted positions, fixed attack directions and simplified lane corridors omit physics, offside and pass probabilities. Pressure is scripted; temporal association does not prove intent or causation. Counterfactuals are hypothetical geometry. Sessions/cache disappear on restart. AI validation is conservative and lexical: fallback can be frequent, and validation does not prove every statement correct. Native browser layout and pointer-capture QA remain separate from automated JSX lifecycle checks.

## Screenshots

Screenshots have not been supplied. Submission placeholders:

- **Pitch and key moments:** replay with a selected defender and supporting evidence.
- **Counterfactual comparison:** actual/hypothetical positions and lane comparison table.
- **Grounded explanation:** Analyst commentary alongside server-verified facts.

## License

A license decision is pending. No license has been selected or added; public visibility alone does not grant an open-source license.
