# AGENTS.md — portal-servers-integration

ASP.NET Core 9 API + three published NuGet packages (`Abstractions`, `Api.Client`, `Api.Client.Testing`) for game-server integration operations: live query, RCON moderation actions, and map-file FTP sync. Auth via Entra ID (`ServiceAccount` role).

This file is the brief for the **GitHub Copilot coding agent** (and any other agent that follows the [agents.md](https://agents.md) convention) when it runs in a cloud runner without the local VS Code multi-root workspace context.

> If you are a human reading this in VS Code, prefer `.github/copilot-instructions.md` for project orientation. `AGENTS.md` is the agent execution brief.

---

## Required reading (read these BEFORE doing any work)

The `copilot-setup-steps.yml` workflow checks out `frasermolyneux/.github-copilot` at `./.github-copilot/` in the runner, so the paths below resolve.

1. `.github/copilot-instructions.md` — repo-specific orientation, build commands, conventions
2. `.github-copilot/.github/instructions/personal.working-preferences.instructions.md`
3. `.github-copilot/.github/copilot-instructions.md` — org-wide catalog
4. Stack-specific files — see **Stack guardrails** below
5. `docs/testing.md`, `docs/development-workflows.md`, `docs/manual-steps.md`

---

## Stack guardrails

### Tenant facts (always-on)
- `tenant.subscriptions`, `tenant.regions`, `tenant.identity`, `tenant.dns`

### Enforceable standards
- `standards.oidc-and-secrets` — **no client secrets**
- `standards.dotnet-project`
- `standards.azure-naming`, `standards.azure-tagging`, `standards.terraform-style`
- `standards.branching-and-prs`

### Patterns
- `patterns.api-client` — three-package layout (Abstractions / Client / Client.Testing)
- `patterns.versioned-apis` — `v{version:apiVersion}/...` routes, runtime OpenAPI
- `patterns.nbgv-versioning`
- `patterns.terraform-remote-state`
- `dotnet-nuget-library.instructions.md`, `dotnet-api-client-libraries.instructions.md`

### Platform consumption contracts
- `platform.workloads`, `platform.monitoring`, `platform.connectivity`

### Shared
- `shared.api-client-abstractions` — `MX.Api.Abstractions` envelopes
- `shared.observability-appinsights` — `IAuditLogger` for RCON moderation actions

---

## Build, test, format

```pwsh
dotnet clean src/XtremeIdiots.Portal.Integrations.Servers.Api.V1/XtremeIdiots.Portal.Integrations.Servers.Api.V1.csproj
dotnet build src/XtremeIdiots.Portal.Integrations.Servers.sln
dotnet test src --filter "FullyQualifiedName!~IntegrationTests"
dotnet format src/XtremeIdiots.Portal.Integrations.Servers.sln --verify-no-changes

terraform -chdir=terraform fmt -check -recursive
terraform -chdir=terraform init -backend-config=backends/dev.backend.hcl
terraform -chdir=terraform validate
terraform -chdir=terraform plan -var-file=tfvars/dev.tfvars
```

---

## Do NOT

- ❌ Do not `git commit`, `git push`, force-push, rebase, or branch-mutate. Work on the assigned branch only.
- ❌ Do not introduce client secrets / FTP credentials in code. Settings come from App Configuration + Key Vault via managed identity.
- ❌ Do not bypass `dotnet format`, `dotnet test`, `terraform fmt`, or `terraform validate`.
- ❌ Do not bypass the `IQueryClientFactory` / `IRconClientFactory` abstractions — direct protocol-client instantiation breaks testability.
- ❌ Do not emit RCON moderation actions (kick, ban, etc.) without `IAuditLogger` audit events.
- ❌ Do not break the published Abstractions / Client NuGet contract without bumping the package version and updating consumers.
- ❌ Do not modify `.github/workflows/`, `.github/dependabot.yml`, or `version.json` unless that is the explicit task.
- ❌ Do not add a `/api/` prefix to controller routes — routes are `v{version:apiVersion}/...`. APIM owns the segment.

---

## Opening the PR

You MUST use `.github/PULL_REQUEST_TEMPLATE.md` as your PR body — do **not** write a freeform body. The org template is inherited from `frasermolyneux/.github` and GitHub pre-populates it when you open the PR. Concretely:

1. Fill `## Summary` (one line) and `Closes #<issue>`.
2. Tick the relevant `## Type of change` box.
3. Paste the **actual command output** from your Build, Tests, and Format check runs into `## Validation evidence`. Show the real summary line, not "tests passed".
4. Fill `## Risk and rollout` — blast radius, auto-deploy?, manual steps post-merge, rollback plan.
5. Tick **every** box in `## Agent attestation`.
6. Delete `## Consumer impact` only if no published contract (Abstractions / Client NuGet / Service Bus DTO / Terraform output) changed.

Complete the `## Agent attestation` section before requesting review; reviewers use it as a readiness checklist.

---

## Pre-PR checks (run before you open the PR)

- [ ] `dotnet build` succeeds (clean)
- [ ] `dotnet test --filter "FullyQualifiedName!~IntegrationTests"` passes
- [ ] `dotnet format --verify-no-changes` passes
- [ ] `terraform fmt -check -recursive` passes
- [ ] `terraform validate` + `terraform plan -var-file=tfvars/dev.tfvars` succeed
- [ ] If Abstractions / Client DTOs changed, consumer impact noted in PR body
- [ ] New moderation actions emit `IAuditLogger` events
- [ ] No new secrets / GUIDs / connection strings
- [ ] PR body cites each acceptance criterion
- [ ] Risk/rollout section filled in

---

## Escalation

If you hit any of the conditions below, **open the PR as draft** and **apply the `needs-decision` label** instead of pushing forward to ready-for-review. Post a comment on the originating issue summarising what's blocking you and what decision is needed.

Stop and escalate when:

- A change requires a breaking Abstractions / Client contract change (also apply the `breaking-contract` label).
- A new game-server protocol implementation needs a NuGet dependency not yet vetted.
- A `code-review` finding is **High** and cannot be resolved in-scope.
- The FTP cert thumbprint configuration (`xtremeidiots_ftp_certificate_thumbprint`) is missing in the dev environment.
