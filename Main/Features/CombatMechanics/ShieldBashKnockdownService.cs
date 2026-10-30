using TAOM.Core.Validation;
using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Features.CombatMechanics;

// Hammerhand (#895): vanilla never knocks a man down with a bash (a shield lacks CanKnockDown), so this adds a true on
// the chance the attacker's Athletics lead earns, in percent: clamp(lead, min, max), divided by the victim race's
// knockdown resistance (dwarf 2.5, trolls 4; a row of 0 means none, so the man always falls while the clamped chance is above
// 0, as in ChargeKnockdownService)
// and by a live race ability's resistance, scaled by the kick factor for an empty hand and by the player factor when
// the victim is the player. Per-hit hot path (only a bash or kick reaches it): config cached at construction, the
// toggle read per call, no allocation. The config is trusted as CombatMechanicsConfigProvider validated it.
public class ShieldBashKnockdownService : IShieldBashKnockdownService
{
    private readonly ICombatMechanicsSettingsProvider _settings;
    private readonly IRaceCombatModifiersResolver _raceModifiers;
    private readonly ShieldBashConfig _config;

    public ShieldBashKnockdownService(
        ICombatMechanicsConfigProvider configProvider,
        ICombatMechanicsSettingsProvider settings,
        IRaceCombatModifiersResolver raceModifiers)
    {
        _settings = settings;
        _raceModifiers = raceModifiers;
        _config = configProvider.GetConfig().ShieldBash ?? new ShieldBashConfig();
    }

    public bool? Decide(in ShieldBashContext context)
    {
        if (!_settings.ShieldBashKnockdownEnabled)
            return null;
        if (!context.IsAlternativeAttack || context.IsHorseCharge)
            return null;

        // Only men bash: a creature's alternative attack is not a shield bash.
        if (!context.AttackerIsHuman)
            return null;

        // A Bash with a weapon that is not a shield (a lance butt, a mace in the off-hand) is not Hammerhand's.
        if (!context.AttackerWeaponIsNull && !context.AttackerWeaponIsShield)
            return null;

        // The ShrugOff blow flag is no gate (vanilla sets it on nearly every bash and kick): the deliberate immunities are.
        if (context.VictimIsImmune)
            return null;

        float chance = Clamp(context.AttackerAthletics - context.VictimAthletics, _config.MinChancePercent, _config.MaxChancePercent);
        chance /= _raceModifiers.Resolve(context.VictimRaceId).KnockdownResistanceMultiplier;

        float abilityResistance = context.VictimAbilityResistanceFactor;
        if (context.VictimCareerResistanceScale != null)
            abilityResistance = context.VictimCareerResistanceScale(abilityResistance);
        if (FiniteFloatValidator.IsFinite(abilityResistance) && abilityResistance > 0f)
            chance /= abilityResistance;

        if (context.AttackerWeaponIsNull)
            chance *= _config.KickChanceFactor;
        if (context.VictimIsPlayer)
            chance *= _config.PlayerVictimChanceFactor;

        // Strict < : a roll equal to the chance does not knock down, and a NaN roll never does. Null (never false) on a miss.
        return context.RandomRoll * 100f < chance ? true : (bool?)null;
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
            return min;
        return value > max ? max : value;
    }
}
