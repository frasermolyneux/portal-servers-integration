---
description: "Protect durable audit and operator-event semantics for game-server actions."
applyTo: "src/XtremeIdiots.Portal.Integrations.Servers.Api.V1/Controllers/V1/*.cs,src/XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1/Controllers/*.cs"
---

# Game-server action auditing

- Emit the audit record and operator event only after a state-changing RCON operation succeeds.
- Include the server, operation, operator, target, and material request context needed to reconstruct the action; do not include credentials.
- Keep read-only/status/list operations unaudited to avoid high-volume noise.
- When changing action classification or emission order, add or update controller tests for success and failure paths so failed commands cannot appear as completed actions.
