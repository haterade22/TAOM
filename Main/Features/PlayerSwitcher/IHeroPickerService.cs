using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Every eligibility and grouping rule for the picker. Engine-free by construction: it consumes
/// PickableHeroInfo from the adapter, so all of it is unit testable with no running campaign.
/// </summary>
public interface IHeroPickerService
{
    HeroPickList BuildPickList(string cultureId, PlayerSwitchPolicy policy);
}
