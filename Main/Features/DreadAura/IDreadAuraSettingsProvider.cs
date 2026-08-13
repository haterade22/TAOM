namespace TAOM.Features.DreadAura;

/// <summary>
/// Merges MCM live values over the validated JSON defaults. <c>TaomSettings.Instance</c> can be
/// null very early in startup or when MCM fails to load, so every read falls back to JSON.
///
/// Every geometry value is read LIVE, once per pulse, never snapshotted onto a tracked source.
/// Snapshotting them at registration would mean the MCM sliders only affected wraiths that spawned
/// after the change, which is not what the hint text promises.
/// </summary>
public interface IDreadAuraSettingsProvider
{
    bool IsEnabled { get; }

    /// <summary>Outer radius in metres. Drain falls to zero here. MCM over JSON.</summary>
    float Radius { get; }

    /// <summary>Radius within which drain is at full rate. JSON only, no MCM knob.</summary>
    float InnerRadius { get; }

    /// <summary>Morale per second at full rate, before the target's tier/hero and racial
    /// resistance. MCM over JSON.</summary>
    float MoralePerSecond { get; }

    /// <summary>When false, agents on the player's own team are never affected. The PLAYER agent
    /// is immune regardless (it has no <c>CommonAIComponent</c>); this governs his troops.</summary>
    bool AffectsPlayerTroops { get; }
}
