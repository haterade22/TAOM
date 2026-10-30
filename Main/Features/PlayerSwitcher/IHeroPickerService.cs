using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Every eligibility and grouping rule for the picker. Engine-free by construction: it consumes
/// PickableHeroInfo from the adapter, so all of it is unit testable with no running campaign.
/// </summary>
public interface IHeroPickerService
{
    HeroPickList BuildPickList(string cultureId, PlayerSwitchPolicy policy);

    /// <summary>
    /// The row to hand over for one hero picked on the faction screen (#704), or an empty row when the
    /// handover must not take them. Eligibility is the list's, judged against the hero's own culture
    /// (the screen's <paramref name="cultureId"/> only when that lookup finds none, #1067). Unlike the
    /// list it is not limited to rulers, their family and clan leaders, since the handover makes any
    /// taken-over clan member the clan's leader, and a clanless wanderer comes back with no clan, to be
    /// adopted into the player's new one.
    /// </summary>
    HeroPickRow FindTakeover(string heroId, string cultureId, PlayerSwitchPolicy policy);
}
