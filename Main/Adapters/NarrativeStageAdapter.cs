using System.Linq;
using TaleWorlds.CampaignSystem.CharacterCreationContent;

namespace TAOM.Adapters;

/// <summary>
/// The only file that touches CharacterCreationManager's narrative menu chain.
/// </summary>
/// <remarks>
/// Constructed from the patch's live instance rather than resolved from IoC, because the manager
/// exists only for the duration of one character creation. Same shape as BodyGeneratorPreviewSink,
/// which wraps a live BodyGeneratorView the same way.
///
/// Every member is null-tolerant: CurrentMenu is null until StartNarrativeStage has run, and a
/// TaleWorlds computed getter can fault before a plain null check would catch it.
/// </remarks>
public class NarrativeStageAdapter : INarrativeStageAdapter
{
    private readonly CharacterCreationManager _manager;

    public NarrativeStageAdapter(CharacterCreationManager manager) => _manager = manager;

    public string CurrentMenuId => _manager?.CurrentMenu?.StringId ?? string.Empty;

    public bool SelectFirstSuitableOption()
    {
        var option = _manager?.GetSuitableNarrativeMenuOptions()?.FirstOrDefault();
        if (option == null)
            return false;

        _manager!.OnNarrativeMenuOptionSelected(option);
        return true;
    }

    public bool TryAdvance() => _manager?.TrySwitchToNextMenu() ?? false;
}
