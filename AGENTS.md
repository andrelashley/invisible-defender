

# Invisible Defender — Agent Instructions

## Project
Build a football intelligence application for the
Microsoft Inside the Game Hackathon.

The system identifies defensive contributions that
traditional football statistics fail to capture.

## Technology
- Backend: ASP.NET Core / C#
- Frontend: React, TypeScript, Vite
- Live updates: SignalR
- AI: Microsoft Foundry (GPT-4.1 mini)
- Data: Synthetic football events and positions
- Deployment: Azure Container Apps

## Engineering Rules
- Read the relevant docs/ specifications before coding.
- Implement the smallest complete vertical slice first.
- Keep domain logic independent of AI and infrastructure.
- Calculate statistics and spatial metrics deterministically.
- Never ask an LLM to invent match statistics.
- Link every generated insight to supporting evidence.
- Keep counterfactual estimates separate from observed facts.
- Use dependency injection and testable service boundaries.
- Avoid unnecessary packages, microservices and databases.
- Do not implement authentication for the MVP.
- Keep Azure secrets out of source control.
- Do not deploy or provision paid cloud resources
  without explicit approval.

## Implementation Workflow
1. Inspect the repository and applicable specifications.
2. Identify assumptions and unresolved dependencies.
3. Implement a bounded feature.
4. Add automated tests.
5. Run build and tests.
6. Report what changed, what passed and what's missing.

## Priorities
1. Working end-to-end pipeline
2. Correct analytical results
3. Explainability and evidence
4. Functional, intuitive UI
5. AI integration
6. Visual polish

## Restrictions
- Use only synthetic football data in the MVP.
- No real Premier League footage or proprietary datasets.
- No unnecessary distributed infrastructure.
- No fabricated AI confidence scores.
- Never expose Foundry credentials in the browser.

## Repository Structure

- `src/api/` — ASP.NET Core Web API, C# solution and projects
- `src/web/` — React, TypeScript, Vite frontend
- `tests/` — Backend and frontend test projects
- `docs/` — Product and technical specifications

All backend application code must live under `src/api/`.
All frontend application code must live under `src/web/`.

Do not create alternative application directories
or reorganize the repository without approval.