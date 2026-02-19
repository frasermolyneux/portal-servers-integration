using System.Text.Json.Serialization;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps;

public record ServerMapDto
{
    public ServerMapDto(string name, string fullName, DateTime modified)
    {
        Name = name;
        FullName = fullName;
        Modified = modified;
    }

    [JsonInclude]
    public string Name { get; internal set; }

    [JsonInclude]
    public string FullName { get; internal set; }

    [JsonInclude]
    public DateTime Modified { get; internal set; }
}
