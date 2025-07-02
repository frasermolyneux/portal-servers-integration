using Newtonsoft.Json;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps
{
    public record ServerMapDto
    {
        public ServerMapDto(string name, string fullName, DateTime modified)
        {
            Name = name;
            FullName = fullName;
            Modified = modified;
        }

        [JsonProperty]
        public string Name { get; internal set; }

        [JsonProperty]
        public string FullName { get; internal set; }

        [JsonProperty]
        public DateTime Modified { get; internal set; }
    }
}
