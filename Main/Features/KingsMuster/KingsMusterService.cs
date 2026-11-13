using TAOM.Core.Validation;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.KingsMuster;

/// <summary>
/// The King's Muster (#874, docs/features/kings-muster.md): <see cref="ApplyRulerDiscount"/> scales the influence cost of one
/// called party when the army leader is an AI kingdom ruler. It is decided from two facts and a number (ADR-007), so nothing
/// here touches a TaleWorlds type, and it truncates with a plain <c>(int)</c> cast, as vanilla's own
/// <c>CalculatePartyInfluenceCost</c> does. A cost of 0 (a call inside the leader's own clan) and any negative number are left
/// alone.
/// <para>
/// The MCM switch and the co-op gate are read live on each call (the gate is the same as <c>RedArrowService.Applies</c>:
/// every co-op case and a dedicated server get the unscaled cost). The factor is read and sanitized once, in the constructor,
/// on the main thread when the army-management model is built.
/// </para>
/// </summary>
public sealed class KingsMusterService
{
    private readonly IKingsMusterSettingsProvider _settings;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicatedServer;
    private readonly float _factor;

    public KingsMusterService(
        IKingsMusterSettingsProvider settings,
        IArmyTargetingConfigProvider config,
        ICoopSessionProvider coop,
        IDedicatedServerProvider dedicatedServer)
    {
        _settings = settings;
        _coop = coop;
        _dedicatedServer = dedicatedServer;
        // The provider already validates; a factor outside [0.25, 1] (a NaN included) reaching here means no discount, never a surcharge.
        var factor = config.GetConfig().RulerCallCostFactor;
        _factor = FiniteFloatValidator.IsFiniteInRange(factor, ArmyTargetingConfig.MinRulerCallCostFactor, 1f) ? factor : 1f;
    }

    /// <summary>
    /// <paramref name="cost"/> (the culture feats' result) scaled by the factor when the leader rules a kingdom and is not the
    /// player, the switch is on and this is singleplayer; the cost itself otherwise.
    /// </summary>
    public int ApplyRulerDiscount(bool isKingdomRuler, bool isLedByMainHero, int cost)
    {
        if (!isKingdomRuler || isLedByMainHero || cost <= 0 || _factor >= 1f || !Applies)
            return cost;
        return (int)(cost * _factor);
    }

    /// <summary>
    /// The factor <see cref="ApplyRulerDiscount"/> applies to a call by this leader (1 when it applies none). The Realms liege
    /// discount reads it so the two do not stack (<c>LiegeArmyDiscountRule</c>).
    /// </summary>
    public float AppliedFactor(bool isKingdomRuler, bool isLedByMainHero)
        => !isKingdomRuler || isLedByMainHero || _factor >= 1f || !Applies ? 1f : _factor;

    private bool Applies =>
        _settings.DiscountEnabled
        && !_coop.IsSessionActive
        && !_coop.ShouldDeferToHost
        && !_dedicatedServer.IsDedicatedServer;
}
