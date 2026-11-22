using System.Text.Json.Serialization;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

[JsonConverter(typeof(JsonStringEnumConverter<RconRosterState>))]
public enum RconRosterState
{
    Unknown = 0,
    Partial = 1,
    Complete = 2,
    Failed = 3
}

public record RconRosterPlayerDto
{
    public int Slot { get; init; }
    public string PlayerIdentifier { get; init; } = string.Empty;
}

public record RconRosterObservationDto
{
    public RconRosterState State { get; init; }
    public List<RconRosterPlayerDto> Players { get; init; } = [];
    public int UnparsedRowCount { get; init; }
    public int MissingIdentityCount { get; init; }
    public int KnownBotCount { get; init; }
}
