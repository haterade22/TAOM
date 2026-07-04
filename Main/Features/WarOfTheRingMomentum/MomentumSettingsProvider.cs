using System;
using TAOM.Core.Validation;
using TAOM.Features.WarOfTheRingMomentum.Models;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// MCM-over-JSON effective values (TaomSettingsProvider precedent). MCM sliders already
/// bound their ranges, but persisted MCM values can predate a range change — so every
/// value re-clamps to the same invariants the config provider validates (dual-surface
/// rule, csharp-architecture.md).
/// </summary>
public class MomentumSettingsProvider : IMomentumSettingsProvider
{
    private readonly IMomentumConfigProvider _configProvider;

    public MomentumSettingsProvider(IMomentumConfigProvider configProvider)
    {
        _configProvider = configProvider;
    }

    private MomentumConfig Config => _configProvider.GetConfig();
    private static TaomSettings Mcm => TaomSettings.Instance;

    public bool MomentumEnabled => Mcm?.MomentumEnabled ?? Config.Enabled;

    public int VictoryThreshold =>
        Mcm != null ? Math.Max(1, Mcm.MomentumVictoryThreshold) : Config.VictoryThreshold;

    public float ParticipationMultiplier =>
        Mcm != null && FiniteFloatValidator.IsFiniteAtLeast(Mcm.MomentumParticipationMultiplier, 1f)
            ? Mcm.MomentumParticipationMultiplier
            : Config.Player.ParticipationMultiplier;

    public bool RequireParticipationForVictory =>
        Mcm?.MomentumRequirePlayerForVictory ?? Config.Player.RequireParticipationForVictory;

    public int MinimumPlayerEventsForVictory =>
        Mcm != null ? Math.Max(0, Mcm.MomentumMinPlayerEvents) : Config.Player.MinimumPlayerEventsForVictory;

    public bool ShowMapMeter => Mcm?.ShowWarOfTheRingMapMeter ?? true;
}
