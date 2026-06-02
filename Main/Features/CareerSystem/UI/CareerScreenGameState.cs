using TaleWorlds.Core;

namespace TAOM.Features.CareerSystem.UI;

public class CareerScreenGameState : GameState
{
    public override bool IsMenuState => true;

    // Set by GauntletCareerScreen.OpenCareerScreen(switchMode) before PushState. The screen ctor
    // reads this to decide between normal mode (perk tree) and switch mode (career picker invoked
    // from the "I wish to discuss my career path" dialogue).
    public bool IsSwitchMode { get; set; }
}
