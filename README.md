# XtremeIdiots Portal - Servers Integration

[![Code Quality](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/codequality.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/codequality.yml)
[![PR Verify](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/pr-verify.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/pr-verify.yml)
[![Deploy Dev](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/deploy-dev.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/deploy-dev.yml)
[![Deploy PRD](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/deploy-prd.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/deploy-prd.yml)
[![Release - Version and Tag](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-version-and-tag.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-version-and-tag.yml)
[![Release - Publish NuGet](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-publish-nuget.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-publish-nuget.yml)

---

## Overview

This repository contains the servers integration API for the XtremeIdiots Portal solution.

## Documentation

* [Development Workflows](docs/development-workflows.md) - Branch strategy, CI/CD triggers, NuGet publishing, and development flows
* [Manual Steps](docs/manual-steps.md) - Post-deployment configuration steps

### .NET Support

The NuGet packages published by this repository target .NET 9 and .NET 10. See [DOTNET_SUPPORT.md](DOTNET_SUPPORT.md) for details on the multi-targeting strategy and dependency management.

---

## Related Projects

* [frasermolyneux/azure-landing-zones](https://github.com/frasermolyneux/azure-landing-zones) - The deploy service principal is managed by this project, as is the workload subscription.
* [frasermolyneux/platform-connectivity](https://github.com/frasermolyneux/platform-connectivity) - The platform connectivity project provides DNS and Azure Front Door shared resources.
* [frasermolyneux/platform-strategic-services](https://github.com/frasermolyneux/platform-strategic-services) - The platform strategic services project provides a shared services such as API Management and App Service Plans.

---

## Contributing

Please read the [contributing](CONTRIBUTING.md) guidance; this is a learning and development project.

---

## Security

Please read the [security](SECURITY.md) guidance; I am always open to security feedback through email or opening an issue.
