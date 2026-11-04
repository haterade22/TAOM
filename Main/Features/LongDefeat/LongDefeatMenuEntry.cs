using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.LongDefeat.Diagnostics;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// Puts "The Long Defeat" on the main menu. <see cref="Install"/> is safe to repeat: the option id is the guard, because the engine's
/// first-screen hook can run more than once. The option hides itself while the config holds no fortress, and on a dedicated server,
/// which has no menu to click and no client to load the run.
/// </summary>
internal sealed class LongDefeatMenuEntry
{
    internal const string OptionId = "TaomLongDefeat";

    private const int OrderIndex = 101;
    private const string Label = "{=TAOM_LD_MenuOption}The Long Defeat";
    private const string Hint = "{=TAOM_LD_MenuHint}Hold a fortress against wave after wave. Spend gold on defenders between waves and keep the keep from falling.";

    private readonly ILongDefeatMenuAdapter _menu;
    private readonly LongDefeatConfigCatalog _catalog;
    private readonly LongDefeatSetupPrompt _prompt;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly IOutcomeLog _outcomes;
    private bool _reported;

    public LongDefeatMenuEntry(ILongDefeatMenuAdapter menu, LongDefeatConfigCatalog catalog, LongDefeatSetupPrompt prompt,
        IDedicatedServerProvider dedicated, IOutcomeLog outcomes)
    {
        _menu = menu;
        _catalog = catalog;
        _prompt = prompt;
        _dedicated = dedicated;
        _outcomes = outcomes;
    }

    public void Install()
    {
        if (_menu.HasOption(OptionId))
            return;

        _menu.AddOption(OptionId, Label, Hint, OrderIndex, IsHidden, _prompt.Begin);
    }

    private bool IsHidden()
    {
        var dedicated = _dedicated.IsDedicatedServer;
        var fortresses = _catalog.Fortresses.Count;
        if (!_reported)
        {
            _reported = true;
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.MenuId, "", LongDefeatChecks.Menu(fortresses, dedicated));
        }

        return dedicated || fortresses == 0;
    }
}
