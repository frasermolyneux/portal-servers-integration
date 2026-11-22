using System.Text.RegularExpressions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal static class RconRosterParser
{
    private static readonly Regex Separator = new(@"^-+(?:\s+-+)+\s*$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex BotRow = new(@"^\s*\d+\s+-?\d+\s+(?:\d+|CNCT|ZMBI|PRIM)\s+\S+\s+.+\s+\d+\s+bot\s+-?\d+\s+\d+\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    private static readonly Regex MissingIdentityRow = new(@"^\s*\d+\s+-?\d+\s+(?:\d+|CNCT|ZMBI|PRIM)\s+(?:0+|\[U:\d+:0+\]|STEAM_\d:0:0+)(?:\s|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    public static RconRosterObservationDto Parse(string response, Regex playerRegex, string identityGroup)
    {
        var lines = response.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var headerIndex = Array.FindIndex(lines, line =>
            line.StartsWith("num ", StringComparison.OrdinalIgnoreCase) &&
            line.Contains("score", StringComparison.OrdinalIgnoreCase) &&
            line.Contains("ping", StringComparison.OrdinalIgnoreCase) &&
            line.Contains("name", StringComparison.OrdinalIgnoreCase) &&
            line.Contains("address", StringComparison.OrdinalIgnoreCase));

        if (headerIndex < 0 || headerIndex + 1 >= lines.Length || !Separator.IsMatch(lines[headerIndex + 1]) ||
            !lines.Take(headerIndex).Any(line => line.StartsWith("map:", StringComparison.OrdinalIgnoreCase)))
        {
            return new RconRosterObservationDto { State = RconRosterState.Failed };
        }

        var players = new List<RconRosterPlayerDto>();
        var unparsed = 0;
        var missingIdentity = 0;
        var bots = 0;
        foreach (var line in lines.Skip(headerIndex + 2))
        {
            if (BotRow.IsMatch(line))
            {
                bots++;
                continue;
            }

            var match = playerRegex.Match(line);
            if (MissingIdentityRow.IsMatch(line))
            {
                missingIdentity++;
                if (!match.Success)
                {
                    unparsed++;
                }
                continue;
            }

            if (!match.Success || !int.TryParse(match.Groups["num"].Value, out var slot))
            {
                unparsed++;
                continue;
            }

            var identity = match.Groups[identityGroup].Value;
            if (string.IsNullOrWhiteSpace(identity) || identity.All(c => c == '0'))
            {
                missingIdentity++;
                continue;
            }

            players.Add(new RconRosterPlayerDto { Slot = slot, PlayerIdentifier = identity });
        }

        return new RconRosterObservationDto
        {
            State = RconRosterState.Partial,
            Players = players,
            UnparsedRowCount = unparsed,
            MissingIdentityCount = missingIdentity,
            KnownBotCount = bots
        };
    }
}
