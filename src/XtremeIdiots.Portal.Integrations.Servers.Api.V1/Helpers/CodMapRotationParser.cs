using System.Text;
using System.Text.RegularExpressions;
using XtremeIdiots.Portal.Integrations.Servers.Api.Models.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal static partial class CodMapRotationParser
{
    [GeneratedRegex("\\A\\s*\"sv_mapRotation\"\\s+is:\\s*\"(?<value>.*?)\"(?:[ \\t]+default:[ \\t]*\"(?<default>.*?)\"(?:[ \\t]+info:[ \\t]*\"(?<info>.*?)\")?)?(?:\\r?\\n[ \\t]*latched:[ \\t]*\"(?<latched>.*?)\")?(?:\\r?\\n[ \\t]*Domain is any text)?[ \\t\\r\\n]*\\z",
        RegexOptions.IgnoreCase | RegexOptions.Singleline, 1000)]
    private static partial Regex DvarResponseRegex();

    [GeneratedRegex("\"sv_mapRotation\"\\s+is:", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex DvarHeaderRegex();

    public static List<Quake3QueryMap> ParsePackets(IEnumerable<byte[]> packets, GameType game)
    {
        var response = new StringBuilder();
        foreach (var packet in packets)
        {
            if (packet.Length < 10 ||
                !packet.AsSpan(0, 4).SequenceEqual(new byte[] { 255, 255, 255, 255 }) ||
                !packet.AsSpan(4, 6).SequenceEqual("print\n"u8))
            {
                throw new FormatException("Invalid CoD map rotation RCON packet.");
            }

            if (Array.FindIndex(packet, 10, static value => value > 127) >= 0)
            {
                throw new FormatException("Non-ASCII CoD map rotation response cannot be decoded losslessly.");
            }
            response.Append(Encoding.ASCII.GetString(packet, 10, packet.Length - 10).TrimEnd('\0'));
        }

        return Parse(response.ToString(), game);
    }

    private static List<Quake3QueryMap> Parse(string response, GameType game)
    {
        var match = DvarResponseRegex().Match(response);
        if (!match.Success || DvarHeaderRegex().Count(response) != 1 ||
            HasUnbalancedQuotes(match.Groups["default"].Value) ||
            HasUnbalancedQuotes(match.Groups["info"].Value) ||
            HasUnbalancedQuotes(match.Groups["latched"].Value))
        {
            throw new FormatException("Missing, malformed or incomplete sv_mapRotation dvar response.");
        }

        // CoD prints a white-color reset inside the closing dvar quote.
        var value = match.Groups["value"].Value;
        if (value.EndsWith("^7", StringComparison.Ordinal))
        {
            value = value[..^2];
        }

        var tokens = game == GameType.CallOfDuty4x ? TokenizeCoD4x(value) : Tokenize(value);
        var maps = new List<Quake3QueryMap>();
        var gameType = string.Empty;
        for (var i = 0; i < tokens.Count; i += 2)
        {
            if (i + 1 == tokens.Count || tokens[i + 1].Length == 0)
            {
                throw new FormatException("Missing sv_mapRotation directive argument.");
            }

            if (tokens[i].Equals("gametype", StringComparison.OrdinalIgnoreCase))
            {
                gameType = tokens[i + 1];
            }
            else if (tokens[i].Equals("map", StringComparison.OrdinalIgnoreCase))
            {
                maps.Add(new Quake3QueryMap { GameType = gameType, MapName = tokens[i + 1] });
            }
            else
            {
                throw new FormatException("Unrecognized sv_mapRotation directive.");
            }
        }

        if (tokens.Count > 0 && maps.Count == 0)
        {
            throw new FormatException("Non-empty sv_mapRotation contains no maps.");
        }

        return maps;
    }

    // Dvar output does not escape embedded quotes; an unmatched quote may be a truncated field.
    private static bool HasUnbalancedQuotes(string value) => value.Count(character => character == '"') % 2 != 0;

    private static List<string> TokenizeCoD4x(string value)
    {
        // CoD4x's rotation lexer is not the stock Com_Parse: it stops at a newline
        // and recognizes directives only when followed by a literal space.
        var newline = value.IndexOf('\n');
        if (newline >= 0)
        {
            if (!string.IsNullOrWhiteSpace(value[(newline + 1)..]))
            {
                throw new FormatException("CoD4x stops reading sv_mapRotation at the first newline.");
            }
            value = value[..newline];
        }

        var tokens = new List<string>();
        var position = 0;
        while (position < value.Length)
        {
            if (value[position] is ' ' or ';')
            {
                position++;
                continue;
            }

            if (tokens.Count % 2 == 0 &&
                !value.AsSpan(position).StartsWith("map ", StringComparison.OrdinalIgnoreCase) &&
                !value.AsSpan(position).StartsWith("gametype ", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException("Invalid CoD4x sv_mapRotation directive.");
            }

            var quoted = value[position] == '"';
            if (quoted)
            {
                position++;
            }
            var start = position;
            while (position < value.Length && value[position] != ';' &&
                (quoted ? value[position] != '"' : value[position] != ' '))
            {
                if (char.IsControl(value[position]) || (!quoted && value[position] == '"'))
                {
                    throw new FormatException("Invalid CoD4x sv_mapRotation token.");
                }
                position++;
            }
            tokens.Add(value[start..position]);
            if (quoted)
            {
                if (position == value.Length || value[position] != '"')
                {
                    throw new FormatException("Unterminated CoD4x sv_mapRotation token.");
                }
                position++;
            }

            // When advancing from an argument CoD4x skips to the next space,
            // not the next semicolon; reject text that would be skipped.
            while (position < value.Length && value[position] == ';')
            {
                position++;
            }
            if (position < value.Length && value[position] != ' ')
            {
                throw new FormatException("Invalid CoD4x sv_mapRotation token boundary.");
            }
        }

        return tokens;
    }

    private static List<string> Tokenize(string value)
    {
        var tokens = new List<string>();
        var position = 0;
        while (position < value.Length)
        {
            if (char.IsWhiteSpace(value[position]))
            {
                position++;
                continue;
            }

            if (char.IsControl(value[position]))
            {
                throw new FormatException("Invalid control character in sv_mapRotation.");
            }

            if (value.AsSpan(position).StartsWith("//"))
            {
                while (position < value.Length && value[position] != '\n')
                {
                    position++;
                }
                continue;
            }

            if (value.AsSpan(position).StartsWith("/*"))
            {
                var end = value.IndexOf("*/", position + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    throw new FormatException("Unterminated sv_mapRotation comment.");
                }
                position = end + 2;
                continue;
            }

            var quoted = value[position] == '"';
            if (quoted)
            {
                position++;
            }
            var start = position;
            while (position < value.Length && (quoted ? value[position] != '"' : !char.IsWhiteSpace(value[position])))
            {
                if (char.IsControl(value[position]) || (!quoted && value[position] == '"'))
                {
                    throw new FormatException("Invalid control character in sv_mapRotation token.");
                }
                position++;
            }

            if (quoted && position == value.Length)
            {
                throw new FormatException("Unterminated sv_mapRotation token.");
            }
            tokens.Add(value[start..position]);
            if (quoted)
            {
                position++;
                if (position < value.Length && !char.IsWhiteSpace(value[position]))
                {
                    throw new FormatException("Invalid quoted sv_mapRotation token boundary.");
                }
            }
        }

        return tokens;
    }
}
