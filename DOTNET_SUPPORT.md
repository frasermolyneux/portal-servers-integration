# .NET Support Strategy

## Target Framework Policy

### NuGet Packages (Libraries)
- **Target Frameworks:** `net9.0;net10.0`
- **Projects:**
  - `XtremeIdiots.Portal.Integrations.Servers.Abstractions.V1`
  - `XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1`

Both .NET 9 and .NET 10 are targeted to maximize compatibility for consumers of these packages.

### Web Applications
- **Target Framework:** `net9.0`
- **Projects:**
  - `XtremeIdiots.Portal.Integrations.Servers.Api.V1`

Web applications remain on .NET 9 as they are deployment artifacts, not distributed packages.

### Test Projects
- **Target Frameworks:** `net9.0;net10.0`

All test projects multi-target to verify library behavior on both runtime versions.

## Dependency Management

### Package Update Policy
- **Minor/Patch versions:** Automatically updated via Dependabot
- **Major versions:** Manual review required
- **Compatibility:** All dependencies must support both .NET 9 and .NET 10

### Current Approach
- No conditional package references based on target framework
- Single dependency version for all target frameworks
- Packages use versions compatible with both .NET 9 and .NET 10

## CI/CD Integration

### Build & Test
GitHub Actions workflows build and test against both .NET 9 and .NET 10 SDKs:
```yaml
dotnet-version: |
  9.0.x
  10.0.x
```

This ensures package compatibility and runtime behavior verification across both versions.

### Dependabot Configuration
```yaml
ignore:
  - dependency-name: "*"
    update-types: ["version-update:semver-major"]
```

Major version updates require manual review to assess breaking changes and compatibility impact.

## Package Versions

Current strategy maintains packages at the highest version supporting both target frameworks:
- Microsoft.Extensions.* packages: 10.0.2
- Azure.Identity: 1.17.1
- MX.Api.* packages: 2.2.31

Web application-specific packages (e.g., `Microsoft.AspNetCore.Mvc.NewtonsoftJson`) remain at .NET 9 compatible versions.
