namespace TAOM.Features.CharacterSkillsRepair;

/// <summary>
/// Repairs the one precondition that makes vanilla's <c>BasicCharacterObject.GetSkillValue</c>
/// throw: a registered character whose <c>DefaultCharacterSkills</c> is null. See
/// <see cref="CharacterSkillsRepairService"/> for the full failure chain and why the repair has to
/// run where it does.
/// </summary>
public interface ICharacterSkillsRepairService
{
    /// <summary>
    /// Gives an empty skill set to every character carrying none, and returns how many were
    /// repaired. Zero on every healthy load, which is the normal case; a non-zero result is
    /// logged with the ids so the underlying data problem stays findable.
    /// </summary>
    int RepairMissingSkillSets();
}
