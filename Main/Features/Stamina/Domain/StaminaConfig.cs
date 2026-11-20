using System.Collections.Generic;
using Newtonsoft.Json;

namespace TAOM.Features.Stamina.Domain;

/// <summary>
/// The shape of ModuleData/configs/stamina.json as written. Every value is optional: a missing one takes the compiled
/// default (<see cref="StaminaTuning"/>). StaminaConfigProvider validates it and turns it into a <see cref="StaminaTuning"/>.
/// </summary>
public sealed class StaminaConfig
{
    // The economy sections are name to value maps, so the provider can warn about a name it does not know.
    [JsonProperty("pool")]
    public Dictionary<string, float?>? Pool { get; set; }

    [JsonProperty("costs")]
    public Dictionary<string, float?>? Costs { get; set; }

    [JsonProperty("run")]
    public Dictionary<string, float?>? Run { get; set; }

    [JsonProperty("regen")]
    public Dictionary<string, float?>? Regen { get; set; }

    [JsonProperty("bands")]
    public Dictionary<string, float?>? Bands { get; set; }

    [JsonProperty("armour")]
    public Dictionary<string, float?>? Armour { get; set; }

    [JsonProperty("peoples")]
    public StaminaPeoplesConfig? Peoples { get; set; }

    [JsonProperty("effects")]
    public StaminaEffectsConfig? Effects { get; set; }

    [JsonProperty("tick")]
    public StaminaTickConfig? Tick { get; set; }
}

public sealed class StaminaPeoplesConfig
{
    /// <summary>Keyed by race name, as the game registers it (human, elf, dwarf, orc, ...).</summary>
    [JsonProperty("races")]
    public Dictionary<string, StaminaPeopleConfig?>? Races { get; set; }

    /// <summary>Keyed by culture id (gondor, arthedain, ...).</summary>
    [JsonProperty("cultures")]
    public Dictionary<string, StaminaPeopleConfig?>? Cultures { get; set; }

    [JsonProperty("multiplierMin")]
    public float? MultiplierMin { get; set; }

    [JsonProperty("multiplierMax")]
    public float? MultiplierMax { get; set; }
}

public sealed class StaminaPeopleConfig
{
    [JsonProperty("pool")]
    public float? Pool { get; set; }

    [JsonProperty("regen")]
    public float? Regen { get; set; }

    [JsonProperty("exempt")]
    public bool? Exempt { get; set; }
}

public sealed class StaminaEffectsConfig
{
    [JsonProperty("winded")]
    public StaminaBandConfig? Winded { get; set; }

    [JsonProperty("tired")]
    public StaminaBandConfig? Tired { get; set; }

    [JsonProperty("exhausted")]
    public StaminaBandConfig? Exhausted { get; set; }
}

public sealed class StaminaBandConfig
{
    [JsonProperty("swing")]
    public float? Swing { get; set; }

    [JsonProperty("handling")]
    public float? Handling { get; set; }

    [JsonProperty("move")]
    public float? Move { get; set; }

    [JsonProperty("aiAttack")]
    public float? AiAttack { get; set; }
}

public sealed class StaminaTickConfig
{
    [JsonProperty("sweepSeconds")]
    public float? SweepSeconds { get; set; }

    [JsonProperty("maxRefreshesPerFrame")]
    public int? MaxRefreshesPerFrame { get; set; }

    [JsonProperty("minRefreshAgeSeconds")]
    public float? MinRefreshAgeSeconds { get; set; }
}
