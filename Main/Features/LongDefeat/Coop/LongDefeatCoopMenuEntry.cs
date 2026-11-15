using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.LongDefeat.Coop;

/// <summary>
/// Puts "The Long Defeat (co-op)" on the main menu (#1280). It shows only once TAOM.CoopCompat has attached a Coop session link, never on a
/// dedicated server, and only while the config holds a fortress. Host asks the solo run's questions and hosts the result; Join asks for
/// the host's address. <see cref="Install"/> is safe to repeat: the option id is the guard.
/// </summary>
internal sealed class LongDefeatCoopMenuEntry
{
    internal const string OptionId = "TaomLongDefeatCoop";
    internal const string HostChoice = "host";
    internal const string JoinChoice = "join";

    private const int OrderIndex = 102;
    private const string Label = "{=TAOM_LD_Coop_MenuOption}The Long Defeat (co-op)";
    private const string Hint = "{=TAOM_LD_Coop_MenuHint}Hold a fortress with other players over the Coop mod. One player hosts; the others join by address.";
    private const string Description = "{=TAOM_LD_Coop_Prompt_Desc}Host a run for other players, or join a run another player hosts.";
    private const string HostLabel = "{=TAOM_LD_Coop_Host}Host a run";
    private const string JoinLabel = "{=TAOM_LD_Coop_Join}Join a run";
    private const string JoinText = "{=TAOM_LD_Coop_JoinText}The host's address and port, for example 192.168.1.20:4200.";

    private readonly ILongDefeatMenuAdapter _menu;
    private readonly LongDefeatConfigCatalog _catalog;
    private readonly LongDefeatSetupPrompt _prompt;
    private readonly LongDefeatCoopRuns _runs;
    private readonly IDedicatedServerProvider _dedicated;

    public LongDefeatCoopMenuEntry(ILongDefeatMenuAdapter menu, LongDefeatConfigCatalog catalog, LongDefeatSetupPrompt prompt,
        LongDefeatCoopRuns runs, IDedicatedServerProvider dedicated)
    {
        _menu = menu;
        _catalog = catalog;
        _prompt = prompt;
        _runs = runs;
        _dedicated = dedicated;
    }

    public void Install()
    {
        if (_menu.HasOption(OptionId))
            return;

        _menu.AddOption(OptionId, Label, Hint, OrderIndex, IsHidden, Ask);
    }

    private bool IsHidden() => !_runs.State.IsAvailable || _dedicated.IsDedicatedServer || _catalog.Fortresses.Count == 0;

    private void Ask() =>
        _menu.Choose(Label, Description, new List<LongDefeatChoice> { new(HostChoice, HostLabel), new(JoinChoice, JoinLabel) }, choice =>
        {
            if (choice == HostChoice)
                _prompt.Begin(setup =>
                {
                    _runs.Host(setup);
                    return true;
                });
            else
                _menu.AskText(JoinLabel, JoinText, _runs.Join);
        });
}
