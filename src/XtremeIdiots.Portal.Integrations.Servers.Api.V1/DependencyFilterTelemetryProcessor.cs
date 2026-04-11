using System;
using System.Linq;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1;

/// <summary>
/// Filters out successful, fast dependency calls for configured dependency types
/// to reduce telemetry volume. Failed calls and calls exceeding the duration
/// threshold are always retained.
/// </summary>
public sealed class DependencyFilterTelemetryProcessor : ITelemetryProcessor
{
    private readonly ITelemetryProcessor _next;
    private readonly IConfiguration _configuration;

    public DependencyFilterTelemetryProcessor(ITelemetryProcessor next, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(configuration);

        _next = next;
        _configuration = configuration;
    }

    public void Process(ITelemetry item)
    {
        if (item is DependencyTelemetry dependency && ShouldFilter(dependency))
            return;

        _next.Process(item);
    }

    private bool ShouldFilter(DependencyTelemetry dependency)
    {
        if (string.IsNullOrEmpty(dependency.Type))
            return false;

        var excludedTypes = _configuration["ApplicationInsights:DependencyFilter:ExcludedTypes"]?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var excludedPrefixes = _configuration["ApplicationInsights:DependencyFilter:ExcludedTypePrefixes"]?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        bool typeMatches =
            (excludedTypes?.Any(t => string.Equals(dependency.Type, t, StringComparison.OrdinalIgnoreCase)) == true) ||
            (excludedPrefixes?.Any(p => dependency.Type.StartsWith(p, StringComparison.OrdinalIgnoreCase)) == true);

        if (!typeMatches)
            return false;

        if (dependency.Success != true)
            return false;

        var thresholdMs = double.TryParse(
            _configuration["ApplicationInsights:DependencyFilter:DurationThresholdMs"], out var t) ? t : 1000;
        if (dependency.Duration.TotalMilliseconds > thresholdMs)
            return false;

        return true;
    }
}
