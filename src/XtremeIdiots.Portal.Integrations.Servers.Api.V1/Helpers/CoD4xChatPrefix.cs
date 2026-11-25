namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal static class CoD4xChatPrefix
{
    public static bool TryGetByteCount(string variable, string response, out int byteCount)
    {
        byteCount = 0;
        if (!TryGetValue(variable, response, out var value))
        {
            return false;
        }

        // RCON responses are decoded as ASCII, one character per received byte.
        byteCount = value.Length;
        return true;
    }

    public static bool TryGetValue(string variable, string response, out string value)
    {
        value = string.Empty;
        var prefix = $"\"{variable}\" is: \"";
        const string suffix = "^7\" default: \"";
        var found = false;

        foreach (var line in response.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var valueStart = prefix.Length;
            var valueEnd = line.IndexOf(suffix, valueStart, StringComparison.Ordinal);
            if (found || valueEnd < 0 ||
                line.IndexOf(suffix, valueEnd + suffix.Length, StringComparison.Ordinal) >= 0 ||
                line.AsSpan(valueStart, valueEnd - valueStart).Contains('"'))
            {
                return false;
            }

            value = line[valueStart..valueEnd];
            found = true;
        }

        return found;
    }
}
