## Architecture

- Backend: ASP.NET Core / C#
- Frontend: React, TypeScript, Vite
- Live updates: SignalR
- AI: Microsoft Foundry (GPT-4.1 mini)
- Data: Synthetic football events and positions
- Deployment: Azure Container Apps

All numerical football analysis must be deterministic. The LLM explains results; it does not calculate or invent them.

The football analytics in a separate C# service or library so it can be tested without SignalR, Foundry, or a running web server.