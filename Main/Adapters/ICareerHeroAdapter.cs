namespace TAOM.Adapters;

public interface ICareerHeroAdapter
{
    string StringId { get; }
    string Name { get; }
    int Level { get; }
    string CultureStringId { get; }
    int ClanTier { get; }
    bool HasAttribute(string attributeName);
    int GetSkillValue(string skillName);
}
