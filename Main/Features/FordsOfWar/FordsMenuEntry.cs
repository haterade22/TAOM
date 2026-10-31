using TAOM.Adapters;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// Puts "Fords of War" on the main menu. <see cref="Install"/> is safe to repeat: the option id is the guard, because the
/// engine's first-screen hook can run more than once. The option hides itself while there is no faction or map to play on.
/// </summary>
internal sealed class FordsMenuEntry
{
    internal const string OptionId = "TaomFordsOfWar";

    // After the content options and well before Multiplayer (9997); 100 is where the parked shader option sat.
    private const int OrderIndex = 100;
    private const string Label = "{=TAOM_FOW_MenuOption}Fords of War";
    private const string Hint = "{=TAOM_FOW_MenuHint}Command an army across three lanes. Spend gold on troops and heroes, break the towers and destroy the enemy keep.";

    private readonly IFordsMenuAdapter _menu;
    private readonly FordsConfigCatalog _catalog;
    private readonly FordsSetupPrompt _prompt;

    public FordsMenuEntry(IFordsMenuAdapter menu, FordsConfigCatalog catalog, FordsSetupPrompt prompt)
    {
        _menu = menu;
        _catalog = catalog;
        _prompt = prompt;
    }

    public void Install()
    {
        if (_menu.HasOption(OptionId))
            return;

        _menu.AddOption(OptionId, Label, Hint, OrderIndex, () => !FordsSetupChoices.HasContent(_catalog), _prompt.Begin);
    }
}
