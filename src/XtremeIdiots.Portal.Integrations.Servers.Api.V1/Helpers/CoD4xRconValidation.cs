using System.Text.RegularExpressions;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal static partial class CoD4xRconValidation
{
    public static bool IsAacpMapList(string? variable, string? value) =>
        variable != null && value != null && AacpVariable().IsMatch(variable.Trim()) && MapList().IsMatch(value);

    [GeneratedRegex(@"\Ascr_aacp_maps_[1-9][0-9]*\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AacpVariable();

    [GeneratedRegex(@"\A[a-zA-Z0-9_-]+(?:;[a-zA-Z0-9_-]+)+\z")]
    private static partial Regex MapList();
}
