namespace TAOM.Features.SignatureStrikes;

/// <summary>
/// MCM live values over the validated JSON defaults. <c>TaomSettings.Instance</c> can be null
/// very early in startup or when MCM fails to load, so every read falls back to JSON.
/// </summary>
public interface ISignatureStrikesSettingsProvider
{
    /// <summary>Folds the Combat Mechanics master toggle: master off means no signature strikes,
    /// the same contract every other mechanic in that MCM group follows.</summary>
    bool IsEnabled { get; }

    /// <summary>Live multiplier on both JSON cooldowns, so the balance can be tuned in a running
    /// game (the JSON needs a full restart). Always finite and positive.</summary>
    float CooldownMultiplier { get; }
}
