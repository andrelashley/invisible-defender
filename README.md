# Invisible Defender

Initial local vertical slice: repeatable synthetic positional events to deterministic passing-lane analysis to SignalR to an interactive React pitch and evidence viewer.

Requires .NET SDK 10 and Node.js 22.12+ (or 24).

From the repository root, run in separate terminals:

```powershell
dotnet run --project src/api/InvisibleDefender.Api.csproj --no-launch-profile --urls http://127.0.0.1:5080
```

```powershell
cd src/web
npm.cmd install
npm.cmd run dev
```

Open http://127.0.0.1:5173. Start, pause, resume, and replay your synthetic match. Select a player to inspect coordinates; inspect a detection to freeze its supporting frame, lane and raw evidence. Return to live to follow playback again. Vite proxies API and SignalR traffic to port 5080. No secrets, AI service or Azure resources are needed.

Validation:

```powershell
dotnet build src/api/InvisibleDefender.slnx
dotnet run --project tests/Analytics.Tests
npm.cmd --prefix src/web run build
```

The production Docker build copies src/web/dist into the published API wwwroot. ASP.NET Core serves the frontend, API and SignalR from one origin. Playback is in memory and isolated by visitor cookie (tabs in one browser share a session). Read [the scope review and detector decisions](docs/INITIAL_SLICE.md) before extending the detector.

The analytical tests are a dependency-free executable suite; run the command above rather than dotnet test. With the API running, exercise HTTP and SignalR using:

```text
node tests/Integration/smoke.mjs
```

See [validation results and environment limitations](docs/VALIDATION.md).

To explore a counterfactual, drag a coral defender on the pitch. The observation freezes for comparison while SignalR playback continues. A hollow marker shows the actual position; a purple marker labels the hypothetical position. Gold dashed lanes are covered and green solid lanes are available. The table compares actual and hypothetical blockers and availability. Drag another defender to start a fresh comparison against the same observation, or use **Reset to actual positions / live pitch** to discard the scenario and return to the latest live frame. Calculations reuse the deterministic passing-lane rules; they do not predict pass outcomes.

Run frontend configuration checks with `npm.cmd --prefix src/web test`.
The replay now separates **Key Defensive Moments** (up to five ranked,
evidence-backed insights) from an expandable **All Match Events** timeline.
Live mode contains only observed events. **Browse complete synthetic replay**
opens the full catalogue explicitly. Select a moment to freeze its defensive
positioning and explore hypothetical moves; **Return to live** or **Resume**
restores current playback. See [insight rules and API](docs/DEFENSIVE_INSIGHTS.md).

On-demand explanations are available for selected defensive moments in Casual Fan and Analyst modes. Click **Generate AI Explanation** explicitly; playback, selection and mode changes consume no AI tokens. Without Foundry configuration, the application returns a labeled deterministic fallback. See [Foundry configuration, caching, authentication and manual verification](docs/FOUNDRY_EXPLANATIONS.md). For token-free UI development set `Narration__Provider=Mock` on the API.

## Production and Azure

See [deployment plan, verified resource inventory, Docker commands, managed identity, cost drivers, smoke tests and cleanup](docs/DEPLOYMENT.md). Use `scripts/Deploy-Azure.ps1` for read-only discovery; applying requires explicit approval for new billable resources. Production container and public Azure validation remain pending; no verified public URL exists yet.
