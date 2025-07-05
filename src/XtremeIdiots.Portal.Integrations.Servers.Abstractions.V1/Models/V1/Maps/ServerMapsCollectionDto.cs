﻿using MX.Api.Abstractions;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps
{
    public record ServerMapsCollectionDto : CollectionModel<ServerMapDto>
    {
        public ServerMapsCollectionDto(IEnumerable<ServerMapDto> items, int totalCount, int filteredCount) : base(items, totalCount, filteredCount)
        {
        }
    }
}
