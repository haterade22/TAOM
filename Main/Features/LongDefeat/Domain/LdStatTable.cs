namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// What the director hands the effects adapter to publish into the engine's hooks at the start of a wave. Slice 2 holds the one number the
/// damage model reads, the attackers' wave damage factor; the stat and armour records of the affixes and boons join it in later slices.
/// </summary>
internal sealed record LdStatTable(float AttackerDamage);
