namespace TAOM.Features.CombatMechanics;

/// <summary>
/// Per-culture cavalry charge damage (#610). The engine builds <c>MountChargeDamage</c> from the
/// horse item's <c>charge_damage</c> and the harness <c>charge_bonus</c>; TAOM's cavalry mostly
/// ride shared vanilla horses, so the kingdom feel is a multiplier on that value, applied in
/// <c>TaomAgentStatCalculateModel.UpdateAgentStats</c> after base and the career passives.
/// </summary>
public interface IChargeDamageService
{
    /// <summary>The factor for a mount whose rider belongs to <paramref name="cultureId"/>.
    /// 1.0 for null, unlisted, non-finite, or when the mechanic is off.</summary>
    float Multiplier(string? cultureId);
}
