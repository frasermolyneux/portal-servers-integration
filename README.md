# XtremeIdiots Portal - Servers Integration
[![Build and Test](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/build-and-test.yml)
[![Code Quality](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/codequality.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/codequality.yml)
[![Copilot Setup Steps](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/copilot-setup-steps.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/copilot-setup-steps.yml)
[![Dependabot Automerge](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/dependabot-automerge.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/dependabot-automerge.yml)
[![Deploy Dev](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/deploy-dev.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/deploy-dev.yml)
[![Deploy Prd](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/deploy-prd.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/deploy-prd.yml)
[![Destroy Development](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/destroy-development.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/destroy-development.yml)
[![Destroy Environment](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/destroy-environment.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/destroy-environment.yml)
[![PR Verify](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/pr-verify.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/pr-verify.yml)
[![Release Publish NuGet](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-publish-nuget.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-publish-nuget.yml)
[![Release Version and Tag](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-version-and-tag.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-version-and-tag.yml)

## Documentation
* [Development Workflows](/docs/development-workflows.md) - Branch strategy, CI/CD triggers, NuGet publishing, and development flows
* [Manual Steps](/docs/manual-steps.md) - Post-deployment configuration steps
* [Platform Settings Contracts](/docs/platform-settings-contracts.md) - Resolver architecture, migration notes, and troubleshooting runbook

## Overview
Versioned REST API that bridges the XtremeIdiots Portal with game servers: querying live status, running RCON operations, and syncing maps over FTP. Uses ASP.NET Core 9 with Microsoft.Identity.Web for Entra-protected controllers, Application Insights telemetry with sampling, optional Azure App Configuration, and a generated Repository API client for portal data. Packages abstractions and API clients for reuse across the ecosystem and deploys to Azure App Service via Terraform with OIDC authentication.

## Contributing
Please read the [contributing](CONTRIBUTING.md) guidance; this is a learning and development project.

## Security
Please read the [security](SECURITY.md) guidance; I am always open to security feedback through email or opening an issue.

## Local dev: MCP wire-up
This repo is wired to the shared `frasermolyneux-copilot` MCP server (pinned to tag `v0.1.0` of [`frasermolyneux/.github-copilot`](https://github.com/frasermolyneux/.github-copilot)). The cloud-runner Copilot setup (`.github/workflows/copilot-setup-steps.yml`) checks out the catalog, builds the MCP server, and `.github/copilot/mcp_config.json` registers it for the GitHub Copilot coding agent. For local clients (VS Code, Claude Desktop, Copilot CLI) and tool-surface details, see [`.github-copilot/mcp-server/README.md`](https://github.com/frasermolyneux/.github-copilot/blob/main/mcp-server/README.md).
