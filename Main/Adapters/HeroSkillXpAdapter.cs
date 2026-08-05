using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public sealed class HeroSkillXpAdapter : IHeroSkillXpAdapter
{
    private readonly IModLogger _logger;

    public HeroSkillXpAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public bool AddSkillXp(string heroId, string skillId, float xp)
    {
        if (float.IsNaN(xp) || float.IsInfinity(xp) || xp <= 0f)
            return false;
        try
        {
            var hero = FindHero(heroId);
            var skill = FindSkill(skillId);
            if (hero?.HeroDeveloper == null || skill == null)
            {
                _logger?.LogWarning($"[Enlistment] AddSkillXp: unresolvable hero '{heroId}' or skill '{skillId}'");
                return false;
            }
            hero.HeroDeveloper.AddSkillXp(skill, xp);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] AddSkillXp('{heroId}', '{skillId}', {xp}) failed: {ex.Message}");
            return false;
        }
    }

    public int GetSkillValue(string heroId, string skillId)
    {
        try
        {
            var hero = FindHero(heroId);
            var skill = FindSkill(skillId);
            if (hero == null || skill == null)
                return 0;
            return hero.GetSkillValue(skill);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] GetSkillValue('{heroId}', '{skillId}') failed: {ex.Message}");
            return 0;
        }
    }

    private static Hero FindHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;
        return Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);
    }

    private static SkillObject FindSkill(string skillId)
    {
        if (string.IsNullOrEmpty(skillId))
            return null;
        return MBObjectManager.Instance?.GetObject<SkillObject>(skillId);
    }
}
