using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>What one line of upgrades changes for a side's whole army.</summary>
internal enum UpgradeEffect
{
    MeleeDamage,
    MissileDamage,
    MeleeArmour,
    MissileArmour,
    SiegeDamage,
    Fortify,
}

/// <summary>One line of upgrades. Each level adds <paramref name="StepPerLevel"/> to the effect; <paramref name="Prices"/> has one price per level. Name is a <c>{=KEY}English</c> string.</summary>
internal sealed record UpgradeTrack(string Id, string Name, UpgradeEffect Effect, float StepPerLevel, IReadOnlyList<int> Prices);

/// <summary>What the upgrades do to one side's blows and the blows it takes, as one immutable value that a blow can read from any thread.</summary>
internal sealed record CombatUpgradeFactors(float MeleeDamage, float MissileDamage, float MeleeArmour, float MissileArmour)
{
    public static CombatUpgradeFactors None { get; } = new(1f, 1f, 1f, 1f);

    public float Attack(bool missile) => missile ? MissileDamage : MeleeDamage;

    public float Defence(bool missile) => missile ? MissileArmour : MeleeArmour;
}
