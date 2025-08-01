﻿namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1
{
    public record ServerRconPlayerDto
    {
        public int Num { get; set; }
        public string? Guid { get; set; }
        public string? Name { get; set; }
        public string? IpAddress { get; set; }
        public int Rate { get; set; }
        public int Ping { get; set; }
    }
}
