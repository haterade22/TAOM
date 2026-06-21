using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

/// <summary>
/// Wraps the <see cref="Hero"/> receiving a LOTR-issue completion reward (the player). APIs verified
/// against installed 1.4.6: gold via <c>GiveGoldAction.ApplyBetweenCharacters(null, hero, amount)</c>,
/// renown via <c>GainRenownAction.Apply(hero, float)</c>, item via <c>ItemRoster.AddToCounts</c>.
/// </summary>
public class LotrIssueRewardAdapter : ILotrIssueRewardAdapter
{
    private readonly Hero _hero;

    public LotrIssueRewardAdapter(Hero hero)
    {
        _hero = hero;
    }

    public bool IsValid => _hero != null && _hero.IsAlive;

    public void AddGold(int amount)
    {
        if (_hero == null || amount <= 0) return;
        GiveGoldAction.ApplyBetweenCharacters(null, _hero, amount);
    }

    public void AddRenown(int amount)
    {
        if (_hero == null || amount == 0) return;
        GainRenownAction.Apply(_hero, amount);
    }

    public void AddItemToInventory(string itemId, int count)
    {
        if (_hero == null || string.IsNullOrEmpty(itemId) || count <= 0) return;
        var item = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
        if (item == null) return;
        var roster = (_hero.PartyBelongedTo ?? MobileParty.MainParty)?.ItemRoster;
        roster?.AddToCounts(item, count);
    }
}
