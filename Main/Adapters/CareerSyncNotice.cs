using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Adapters;

/// <inheritdoc />
public sealed class CareerSyncNotice : ICareerSyncNotice
{
    public void ShowRefused() => InformationManager.DisplayMessage(new InformationMessage(
        new TextObject("{=taom_career_sync_refused}The co-op server did not accept that career change and keeps the career it has saved for you.").ToString()));
}
