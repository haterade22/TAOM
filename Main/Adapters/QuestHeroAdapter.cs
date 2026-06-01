using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

/// <summary>
/// Wraps a <see cref="Hero"/> for the career-quest system. All TaleWorlds APIs here were verified
/// against installed Bannerlord 1.4.5 (see the career-quest 1.4.5 verification pass): renown via
/// <c>GainRenownAction.Apply(Hero, float, bool=false)</c>, influence via
/// <c>ChangeClanInfluenceAction.Apply(Clan, float)</c>, item grant via
/// <c>ItemRoster.AddToCounts(ItemObject, int)</c>, reads via <c>Hero.Gold</c> / <c>Clan.Renown</c> /
/// <c>Hero.GetSkillValue(SkillObject)</c>.
/// </summary>
public class QuestHeroAdapter : IQuestHeroAdapter
{
    private readonly Hero _hero;

    public QuestHeroAdapter(Hero hero)
    {
        _hero = hero;
    }

    public string StringId => _hero?.StringId;

    public bool IsValid => _hero != null && _hero.IsAlive;

    public int GetSkillValue(string skillName)
    {
        if (_hero == null || string.IsNullOrEmpty(skillName)) return 0;
        var skill = MBObjectManager.Instance?.GetObject<SkillObject>(skillName);
        return skill == null ? 0 : _hero.GetSkillValue(skill);
    }

    // Clan.Renown is a float; quest thresholds are integers — floor to int.
    public int ClanRenown => (int)(_hero?.Clan?.Renown ?? 0f);

    public int Gold => _hero?.Gold ?? 0;

    public void AddItemToInventory(string itemId, int count)
    {
        if (_hero == null || string.IsNullOrEmpty(itemId) || count <= 0) return;
        var item = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
        if (item == null) return;
        // Grant to the hero's party (the player's MainParty in the v1 self-quest case).
        var roster = (_hero.PartyBelongedTo ?? MobileParty.MainParty)?.ItemRoster;
        roster?.AddToCounts(item, count);
    }

    public void AddRenown(int amount)
    {
        if (_hero == null || amount == 0) return;
        GainRenownAction.Apply(_hero, amount);
    }

    public void AddInfluence(int amount)
    {
        if (_hero?.Clan == null || amount == 0) return;
        ChangeClanInfluenceAction.Apply(_hero.Clan, amount);
    }
}
