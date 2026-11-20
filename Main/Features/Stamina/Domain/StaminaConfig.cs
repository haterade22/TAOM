using Newtonsoft.Json;

namespace TAOM.Features.Stamina.Domain;

/// <summary>
/// The shape of ModuleData/configs/stamina.json as written. Every value is optional: a missing one takes the compiled
/// default (<see cref="StaminaTuning"/>). StaminaConfigProvider validates it and turns it into a <see cref="StaminaTuning"/>.
/// </summary>
public sealed class StaminaConfig
{
    [JsonProperty("effects")]
    public StaminaEffectsConfig? Effects { get; set; }

    [JsonProperty("tick")]
    public StaminaTickConfig? Tick { get; set; }
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
