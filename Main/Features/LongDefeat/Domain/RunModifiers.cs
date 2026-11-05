using System.Collections.Generic;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>Folds a wave's affixes into the table the engine's hooks read (phase2-plan.md section 1): the stat powers into a <see cref="RaceAbilityEffects"/> record and <c>hide</c> into an armour percent. Pure.</summary>
internal static class RunModifiers
{
    /// <summary>The table for a wave with these affixes and this attacker damage factor, a new record on every call so a table the engine's threads already hold is never mutated.</summary>
    public static LdStatTable StatTable(AffixPlan plan, IReadOnlyList<string> affixes, float attackerDamage)
    {
        var fx = new RaceAbilityEffects();
        var armour = 0f;
        var stats = false;
        foreach (var id in affixes)
        {
            switch (id)
            {
                case AffixPlan.Lash:
                    fx.SwingSpeedPercent += plan.Value(id, "swingSpeedPercent");
                    fx.AttackEagernessPercent += plan.Value(id, "attackEagernessPercent");
                    stats = true;
                    break;
                case AffixPlan.Drums:
                    fx.MoveSpeedPercent += plan.Value(id, "moveSpeedPercent");
                    fx.AccelerationPercent += plan.Value(id, "accelerationPercent");
                    stats = true;
                    break;
                case AffixPlan.Eye:
                    fx.AimErrorPercent += plan.Value(id, "aimErrorPercent");
                    fx.DrawSpeedPercent += plan.Value(id, "drawSpeedPercent");
                    fx.ReloadSpeedPercent += plan.Value(id, "reloadSpeedPercent");
                    fx.MissileSpeedPercent += plan.Value(id, "missileSpeedPercent");
                    stats = true;
                    break;
                case AffixPlan.Discipline:
                    fx.BlockAbilityPercent += plan.Value(id, "blockAbilityPercent");
                    fx.ParryAbilityPercent += plan.Value(id, "parryAbilityPercent");
                    stats = true;
                    break;
                case AffixPlan.Hide:
                    armour += plan.Value(id, "armourPercent");
                    break;
            }
        }

        fx.MountSpeedPercent = fx.MoveSpeedPercent;
        return new LdStatTable(attackerDamage, stats ? fx : null, armour);
    }
}
