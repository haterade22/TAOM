using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace TAOM.Features.FiefManagement;

public interface IFiefHubMenuPresenter
{
    int Count { get; }

    void Reset();
    void Refresh();
    TextObject BuildTitle();
    bool ManageOptionEnabled(out TextObject disabledHint);

    void StepNext();
    void StepPrevious();

    /// <summary>Resolve the currently-selected fief to a sealed Settlement instance, or null if
    /// the player has lost ownership since the menu was last refreshed. Called only at the
    /// boundary (consequence callback) so the rest of the presenter stays ADR-007 compliant.</summary>
    Settlement ResolveCurrentSettlement();
}
