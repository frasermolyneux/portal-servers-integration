# Copilot instructions

Make focused changes within this repository and follow its existing .NET, test, API, client, and Terraform patterns.

- Preserve versioned routes, authorization, published NuGet contracts, typed settings contracts, and external server-interaction safety.
- Keep protocol access behind the existing query, RCON, and file-transport abstractions.
- Audit successful state-changing RCON operations; do not audit routine read-only queries.
- Put tests beside the affected implementation and exclude integration tests unless their external prerequisites are available.
- Use managed identity/OIDC configuration; never add credentials or client secrets.
- Treat Terraform backend, remote-state, provider, output, and deployment wiring as compatibility-sensitive.

Repository structure, commands, and material operational constraints are documented in `AGENTS.md`.
