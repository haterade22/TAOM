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
    /// handover must not take them: the same eligibility as the list, and a clan to take over. Unlike
    /// the list it is not limited to rulers, their family and clan leaders, since the handover makes any
    /// taken-over clan member the clan's leader.
    /// </summary>
    HeroPickRow FindTakeover(string heroId, string cultureId, PlayerSwitchPolicy policy);
}
