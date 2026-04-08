using Newtonsoft.Json;

namespace TAOM.Features.NamedCompanions.Domain;

public sealed class NamedCompanionDefinition
{
    [JsonProperty("character_id")]
    public string CharacterId { get; set; }

    [JsonProperty("spawn_settlement")]
    public string SpawnSettlement { get; set; }

    [JsonProperty("race")]
    public string Race { get; set; }

    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;
}
