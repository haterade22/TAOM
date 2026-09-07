using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// Engine boundary for <see cref="TAOM.Features.CharacterSkillsRepair.ICharacterSkillsRepairService"/>.
/// Everything crosses as string ids and counts (ADR-007: <c>CharacterObject</c> and
/// <c>MBCharacterSkills</c> are engine types and never leave the adapter).
///
/// The state it looks for is a <c>BasicCharacterObject</c> whose protected
/// <c>DefaultCharacterSkills</c> is null. Vanilla's
/// <c>BasicCharacterObject.GetSkillValue</c> (v1.4.8 <c>:292-295</c>) is
/// <c>return DefaultCharacterSkills.Skills.GetPropertyValue(skill);</c> with no guard at all, so
/// every read of a non-hero character's skill is a hard NRE while that field is null.
/// </summary>
public interface ICharacterSkillsAdapter
{
    /// <summary>
    /// String ids of every registered character carrying no skill set. Empty on a healthy load,
    /// and empty rather than throwing when the object manager is not available yet.
    /// </summary>
    IReadOnlyList<string> FindCharactersWithNoSkillSet();

    /// <summary>
    /// Gives one such character an empty skill set, which is exactly what vanilla's own
    /// <c>Deserialize</c> fallback produces for a troop that declares no <c>skill_template</c>
    /// and no <c>&lt;skills&gt;</c> block. False when the character cannot be resolved or the
    /// field cannot be written.
    /// </summary>
    bool TryGiveEmptySkillSet(string characterId);
}
