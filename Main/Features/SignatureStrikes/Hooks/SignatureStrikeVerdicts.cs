using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SignatureStrikes.Hooks;

/// <summary>
/// The primary-victim knockdown and knock-back verdicts the campaign damage model asks for a signature hero's hit (#605):
/// <c>TaomCombatMechanicsModel.DecideAgentKnockedDownByBlow</c> and <c>DecideAgentKnockedBackByBlow</c>, moved out of the
/// model by #737. The strike context comes from <see cref="StrikeContextFactory"/>, the boundary the mission logic's ring
/// also uses, so both paths describe one hit the same way. Null when the feature is absent, for a horse charge, and for an
/// attacker off the roster, so the model asks its next seam.
/// </summary>
public static class SignatureStrikeVerdicts
{
    // Null service or roster = feature absent (the optional-param contract). The roster probe is
    // one dictionary lookup, so the non-signature 99.9% of hits pay nothing further. A horse charge
    // is never a signature verdict (the service declines it too), so it skips even the probe.
    // IsHorseCharge is ChargeVelocity > 0f, so a NaN velocity counts as a melee hit, deliberately:
    // the model routed it so before #737.
    public static bool? Decide(ISignatureStrikeService? signatureStrikes, ISignatureAgentRoster? signatureRoster, Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, in Blow blow, bool knockdown)
    {
        if (signatureStrikes == null || signatureRoster == null || collisionData.IsHorseCharge
            || !signatureRoster.TryGet(attackerAgent, out var entry))
            return null;

        var context = StrikeContextFactory.FromMeleeCollision(
            attackerAgent, victimAgent, in collisionData, isCanceled: false, blow.BlowFlag, entry,
            Mission.Current?.CurrentTime ?? float.NaN);
        return knockdown ? signatureStrikes.DecideKnockdown(in context) : signatureStrikes.DecideKnockback(in context);
    }
}
