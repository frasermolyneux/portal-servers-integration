﻿namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1
{
    public record ServerQueryStatusResponseDto
    {
        public string? ServerName { get; set; }
        public string? Map { get; set; }
        public string? Mod { get; set; }
        public int MaxPlayers { get; set; }
        public int PlayerCount { get; set; }

        public IDictionary<string, string> ServerParams { get; set; } = new Dictionary<string, string>();
        public IList<ServerQueryPlayerDto> Players { get; set; } = new List<ServerQueryPlayerDto>();
    }
}
