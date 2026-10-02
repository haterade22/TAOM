using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;

namespace TAOM.Features.FactionUI.UI.Widgets;

/// <summary>A themed character-creation header button that jumps straight to stage
/// <see cref="StageIndex"/> (#704, Kysaro's), through the engine's own
/// <c>CharacterCreationManager.GoToStage</c>.</summary>
public class StageJumpButton : ButtonWidget
{
    public StageJumpButton(UIContext context)
        : base(context)
    {
    }

    [Editor(false)]
    public int StageIndex { get; set; }

    protected override void HandleClick()
    {
        base.HandleClick();
        if (GameStateManager.Current?.ActiveState is CharacterCreationState state)
            state.CharacterCreationManager?.GoToStage(StageIndex);
    }
}
