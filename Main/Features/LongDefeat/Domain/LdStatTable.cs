using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// What the director hands the effects adapter to publish into the engine's hooks at the start of a wave: the attackers' wave damage
/// factor (read by the damage model), their stat record (written onto driven properties by the stat model's post-pass) and the percent their
/// four armour values are raised by at spawn. The record is null when no stat power is in play. It is built fresh for each publish and
/// never changed afterwards, because the engine's threads read it without a lock; the defenders' and the player's records join it with the boons.
/// </summary>
internal sealed record LdStatTable(float AttackerDamage, RaceAbilityEffects? Attackers = null, float AttackerArmourPercent = 0f);
