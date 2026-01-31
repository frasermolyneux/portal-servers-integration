using MX.Api.Abstractions;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record RconMapCollectionDto : CollectionModel<RconMapDto>
{
    public RconMapCollectionDto(IEnumerable<RconMapDto> items) : base(items)
    {
    }
}
