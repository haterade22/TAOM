using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

public class CareerHeroAdapter : ICareerHeroAdapter
{
    private readonly Hero _hero;

    public CareerHeroAdapter(Hero hero)
    {
        _hero = hero;
    }

    public string StringId => _hero?.StringId;
    public string Name => _hero?.Name?.ToString();
    public int Level => _hero?.Level ?? 0;
    public string CultureStringId => _hero?.Culture?.StringId;
    public int ClanTier => _hero?.Clan?.Tier ?? 0;

    public bool HasAttribute(string attributeName)
    {
        if (_hero == null || string.IsNullOrEmpty(attributeName)) return false;

        var attr = MBObjectManager.Instance?.GetObject<CharacterAttribute>(attributeName);
        if (attr == null) return false;

        return _hero.GetAttributeValue(attr) > 0;
    }

    public int GetSkillValue(string skillName)
    {
        if (_hero == null || string.IsNullOrEmpty(skillName)) return 0;

        var skill = MBObjectManager.Instance?.GetObject<SkillObject>(skillName);
        if (skill == null) return 0;

        return _hero.GetSkillValue(skill);
    }
}
