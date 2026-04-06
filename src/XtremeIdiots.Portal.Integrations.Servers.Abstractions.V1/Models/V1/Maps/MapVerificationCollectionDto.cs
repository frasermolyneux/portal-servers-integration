using MX.Api.Abstractions;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps;

public record MapVerificationCollectionDto : CollectionModel<MapVerificationResultDto>
{
    public MapVerificationCollectionDto(IEnumerable<MapVerificationResultDto> items) : base(items) { }
}
