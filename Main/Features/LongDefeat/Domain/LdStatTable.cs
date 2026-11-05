using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// What the boons owned give one group of the player's side: the stat record the stat hook writes onto its driven properties (null when no stat boon aims at the group, so the
/// hook stays inert), and the factors the damage model applies to a blow the group deals and to a blow it takes. Built fresh and never changed afterwards.
/// </summary>
internal sealed record LdTargetStats(RaceAbilityEffects? Stats, float Damage, float DamageTaken)
{
    public static LdTargetStats None { get; } = new(null, 1f, 1f);
}

/// <summary>
/// What the director hands the effects adapter to publish into the engine's hooks: the attackers' wave damage
/// factor (read by the damage model), their stat record (written onto driven properties by the stat model's post-pass) and the percent their
/// four armour values are raised by at spawn. The record is null when no stat power is in play. The defenders' three groups and the lord carry what the boons owned give
/// them (<see cref="For"/>). It is built fresh for each publish and never changed afterwards, because the engine's threads read it without a lock.
/// </summary>
internal sealed record LdStatTable(float AttackerDamage, RaceAbilityEffects? Attackers = null, float AttackerArmourPercent = 0f)
{
    public LdTargetStats Infantry { get; init; } = LdTargetStats.None;

    public LdTargetStats Archers { get; init; } = LdTargetStats.None;

    public LdTargetStats Cavalry { get; init; } = LdTargetStats.None;

    public LdTargetStats Player { get; init; } = LdTargetStats.None;

    public LdTargetStats For(BoonTarget target) => target switch
    {
        BoonTarget.Infantry => Infantry,
        BoonTarget.Archers => Archers,
        BoonTarget.Cavalry => Cavalry,
        _ => Player,
    };
}
