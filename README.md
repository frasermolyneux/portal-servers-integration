# XtremeIdiots Portal - Servers Integration
[![Build and Test](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/build-and-test.yml)
[![Code Quality](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/codequality.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/codequality.yml)
[![Copilot Setup Steps](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/copilot-setup-steps.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/copilot-setup-steps.yml)
[![Dependabot Auto-Merge](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/dependabot-automerge.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/dependabot-automerge.yml)
[![Deploy Dev](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/deploy-dev.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/deploy-dev.yml)
[![Deploy Prd](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/deploy-prd.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/deploy-prd.yml)
[![Destroy Development](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/destroy-development.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/destroy-development.yml)
[![Destroy Environment](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/destroy-environment.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/destroy-environment.yml)
[![PR Verify](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/pr-verify.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/pr-verify.yml)
[![Release - Publish NuGet](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-publish-nuget.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-publish-nuget.yml)
[![Release - Version and Tag](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-version-and-tag.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-version-and-tag.yml)

## Documentation
* [Development Workflows](/docs/development-workflows.md) - Branch strategy, CI/CD triggers, NuGet publishing, and development flows
* [Manual Steps](/docs/manual-steps.md) - Post-deployment configuration steps
* [Platform Settings Contracts](/docs/platform-settings-contracts.md) - Resolver architecture, migration notes, and troubleshooting runbook
* [Testing](/docs/testing.md) - Test strategy and package testing helpers for API client consumers

## Overview
Versioned REST API that bridges the XtremeIdiots Portal with game servers: querying live status, running RCON operations, and syncing maps over FTP. Uses ASP.NET Core 9 with Microsoft.Identity.Web for Entra-protected controllers, Application Insights telemetry with sampling, optional Azure App Configuration, and a generated Repository API client for portal data. Packages abstractions and API clients for reuse across the ecosystem and deploys to Azure App Service via Terraform with OIDC authentication.

## NuGet Packages

| Package                                                                                                                                                     | Latest                                                                                                                                                                                                  | Description                                               |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------- |
| [`XtremeIdiots.Portal.Integrations.Servers.Abstractions.V1`](https://www.nuget.org/packages/XtremeIdiots.Portal.Integrations.Servers.Abstractions.V1)       | [![NuGet](https://img.shields.io/nuget/v/XtremeIdiots.Portal.Integrations.Servers.Abstractions.V1.svg)](https://www.nuget.org/packages/XtremeIdiots.Portal.Integrations.Servers.Abstractions.V1/)       | Shared contracts and DTOs for the Servers Integration API |
| [`XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1`](https://www.nuget.org/packages/XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1)           | [![NuGet](https://img.shields.io/nuget/v/XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1.svg)](https://www.nuget.org/packages/XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1/)           | Typed client for Servers Integration API V1               |
| [`XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing`](https://www.nuget.org/packages/XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing) | [![NuGet](https://img.shields.io/nuget/v/XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing.svg)](https://www.nuget.org/packages/XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing/) | In-memory fakes and factory helpers for consumer tests    |

## Contributing
Please read the [contributing](CONTRIBUTING.md) guidance; this is a learning and development project.

## Security
Please read the [security](SECURITY.md) guidance; I am always open to security feedback through email or opening an issue.
