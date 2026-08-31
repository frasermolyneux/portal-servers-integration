# portal-servers-integration

ASP.NET Core API for live game-server query, RCON administration, and file transport. It also publishes the versioned `Abstractions`, `Api.Client`, and `Api.Client.Testing` NuGet packages.

## Ownership and paths

- `src/XtremeIdiots.Portal.Integrations.Servers.Api.V1/`: authenticated API, protocol factories, Repository API integration, runtime OpenAPI, and external RCON/query/FTP/SFTP calls.
- `src/XtremeIdiots.Portal.Integrations.Servers.Abstractions.V1/`: public DTO and interface contract.
- `src/XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1/`: public typed client.
- `src/XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing/`: consumer-test fake and DTO factories.
- Matching `*.Tests*` projects contain unit tests; `*.IntegrationTests*` require their documented external prerequisites and are excluded from the default test command.
- `terraform/`: App Service, APIM integration, monitoring, and environment wiring.

## Commands

```pwsh
dotnet build src/XtremeIdiots.Portal.Integrations.Servers.slnx
dotnet test src --filter "FullyQualifiedName!~IntegrationTests"
dotnet format src/XtremeIdiots.Portal.Integrations.Servers.slnx --verify-no-changes
terraform -chdir=terraform fmt -check -recursive
terraform -chdir=terraform init -backend-config=backends/dev.backend.hcl
terraform -chdir=terraform validate
```

Run integration tests only when their API identity, configuration, and reachable service dependencies are available.

## Material contracts and constraints

- Preserve the published Abstractions, Client, and Testing package surfaces and their multi-targeting unless a contract/version change is intentional.
- Routes are versioned as `v{version:apiVersion}/...`; APIM owns any external prefix. Keep anonymous health/info endpoints and `ServiceAccount` authorization boundaries intact.
- Use `IQueryClientFactory`, `IRconClientFactory`, and the file-transport resolver/session abstractions. Keep external calls cancellable, observable, and testable.
- RCON actions that change server or player state need a durable audit record and operator event; read-only/high-frequency queries do not. Keep audit/event emission after successful execution and cover changes in controller tests.
- File transport and RCON configuration use `XtremeIdiots.Portal.Settings.Contracts.V1` typed documents and validators, including required legacy-schema compatibility. Do not replace them with ad-hoc JSON parsing.
- Map sync communicates with external FTP/SFTP hosts. Preserve certificate-thumbprint validation, credential boundaries, path handling, and cleanup behavior.
- Repository API supplies server metadata and configuration. Preserve its readiness check and App Configuration/Key Vault managed-identity setup.

## Infrastructure and delivery

- Terraform requires `>= 1.15.6`, AzureRM `~> 5.0.1`, and the `azurerm` backend. Environment backend files are under `terraform/backends/`; variables are under `terraform/tfvars/`.
- Remote state is consumed from platform workloads, platform monitoring, portal environments, and portal core. Do not change backend/state keys or output contracts casually.
- Development and production deployment use GitHub environments, OIDC, Terraform, App Service deployment, and APIM wiring. NuGet release is version/tag driven. Never introduce client secrets or embedded FTP credentials.
- `.terraform.lock.hcl` is intentionally untracked.

Use the repository README and focused docs for human operational detail; keep this file limited to implementation constraints that affect safe changes.
