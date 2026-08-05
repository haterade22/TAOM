namespace TAOM.Adapters;

/// <summary>Skill XP + skill reads for the enlistment reward spine. Skill ids are engine StringIds (note: smithing = "Crafting").</summary>
public interface IHeroSkillXpAdapter
{
    bool AddSkillXp(string heroId, string skillId, float xp);

    /// <summary>Current skill VALUE (not xp), or 0 when unresolvable.</summary>
    int GetSkillValue(string heroId, string skillId);
}
