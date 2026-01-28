# XtremeIdiots Portal - Servers Integration

| Stage                   | Status                                                                                                                                                                                                                                                         |
| ----------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| DevOps Secure Scanning  | [![DevOps Secure Scanning](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/devops-secure-scanning.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/devops-secure-scanning.yml)    |
| Code Quality            | [![Code Quality](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/codequality.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/codequality.yml)                                    |
| Feature Development     | [![Feature Development](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/feature-development.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/feature-development.yml)             |
| Pull Request Validation | [![Pull Request Validation](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/pull-request-validation.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/pull-request-validation.yml) |
| Release to Production   | [![Release to Production](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-to-production.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-to-production.yml)       |

---

## Overview

This repository contains the servers integration API for the XtremeIdiots Portal solution.

### .NET Support

The NuGet packages published by this repository target .NET 9 and .NET 10. See [DOTNET_SUPPORT.md](DOTNET_SUPPORT.md) for details on the multi-targeting strategy and dependency management.

### OpenAPI Specification

The OpenAPI specification is automatically generated and kept up to date. 

#### Generating Locally

To generate the OpenAPI specification locally:

```bash
./generate-openapi.sh
```

This script will:
1. Build the API project
2. Start the application in `OpenApiGeneration` mode
3. Fetch the OpenAPI spec from the running application
4. Save it to `openapi/openapi-v1.json`

#### Automated Updates

The OpenAPI specification is automatically updated via GitHub Actions:
- **Weekly**: Runs every Monday at 9 AM UTC
- **Manual**: Can be triggered via workflow dispatch

When changes are detected, a pull request is automatically created for review.

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
