using TaleWorlds.CampaignSystem;

namespace TAOM.Features.FactionMap;

public interface ICultureSettingService
{
    void SetCultureOnCharacterCreation(CultureObject culture, object viewInstance, object? originalDataSource);
}
