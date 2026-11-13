using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The start path of The Long Defeat: <see cref="Begin"/> asks for the fortress, <see cref="Start"/> is the prompt-free entry.
/// Neither throws, because they run inside a menu click, where an exception reaches the engine's UI loop.
/// </summary>
internal sealed class LongDefeatSetupPrompt
{
    private const string NoClientMessage = "{=TAOM_LD_NoClient}The Long Defeat needs the game client and cannot start here.";
    private const string NoContentMessage = "{=TAOM_LD_NoContent}The Long Defeat has no fortress to defend. Check long_defeat/fortresses.json.";
    private const string PromptTitle = "{=TAOM_LD_MenuOption}The Long Defeat";
    private const string PromptDescription = "{=TAOM_LD_Prompt_Desc}Hold a fortress against wave after wave of attackers. Spend gold between waves on more defenders. The run ends when the garrison or the keep falls. If a shader error window appears while the fortress loads, press Cancel.";
    private const string ChoiceWithBest = "{=TAOM_LD_Prompt_ChoiceBest}{NAME}, best run: waves cleared {WAVES}, kills {KILLS}";
    private const string ClassTitle = "{=TAOM_LD_Prompt_ClassTitle}Choose your class";
    private const string ClassDescription = "{=TAOM_LD_Prompt_ClassDesc}Your class sets the weapons you carry into the fight. Cavalry also sets your armour and puts you on a horse; the other classes leave your armour as it is.";
    internal const string StartFailedMessage = "{=TAOM_LD_StartFailed}The Long Defeat could not start. The log has the reason.";

    private readonly ILongDefeatMenuAdapter _menu;
    private readonly ILongDefeatGameManagerFactory _factory;
    private readonly IModLogger _logger;
    private readonly LongDefeatConfigCatalog _catalog;
    private readonly LongDefeatBestRunStore _best;

    public LongDefeatSetupPrompt(ILongDefeatMenuAdapter menu, ILongDefeatGameManagerFactory factory, IModLogger logger,
        LongDefeatConfigCatalog catalog, LongDefeatRunRecords records)
    {
        _menu = menu;
        _factory = factory;
        _logger = logger;
        _catalog = catalog;
        _best = records.Best;
    }

    /// <summary>Asks which fortress, then which class when the fortress offers one, and starts the run. Cancelling either question starts nothing.</summary>
    public void Begin()
    {
        try
        {
            if (_catalog.Fortresses.Count == 0)
            {
                _menu.Notify(NoContentMessage);
                return;
            }

            _menu.Choose(PromptTitle, PromptDescription, _catalog.Fortresses.Select(Choice).ToList(),
                AskClass);
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    /// <summary>Starts a run from a finished setup. False, with the player told why, when no game manager could be built.</summary>
    public bool Start(LongDefeatSetup setup)
    {
        try
        {
            if (!_factory.Start(setup))
            {
                _logger.LogWarning("[LongDefeat] the game manager factory returned false (no TAOM.Client); the run cannot start");
                _menu.Notify(NoClientMessage);
                return false;
            }

            _logger.LogInfo($"[LongDefeat] started fortress {setup.FortressId} ({setup.Trigger})");
            return true;
        }
        catch (Exception ex)
        {
            Fault(ex);
            return false;
        }
    }

    private void AskClass(string fortressId)
    {
        try
        {
            var classes = _catalog.TryGetFortress(fortressId, out var fortress) ? _catalog.ClassesFor(fortress) : Array.Empty<RunClass>();
            if (classes.Count == 0)
            {
                Start(new LongDefeatSetup(fortressId, "menu", RunOptions.Default));
                return;
            }

            _menu.Choose(ClassTitle, ClassDescription, classes.Select(ClassChoice).ToList(),
                classId => Start(new LongDefeatSetup(fortressId, "menu", RunOptions.Default with { ClassId = classId })));
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    private static LongDefeatChoice ClassChoice(RunClass cls) => new(cls.Id, cls.Name, Hint: cls.Text);

    private LongDefeatChoice Choice(FortressEntry fortress)
    {
        var best = _best.Load(fortress.Id);
        if (best == null)
            return new LongDefeatChoice(fortress.Id, fortress.Name);

        return new LongDefeatChoice(fortress.Id, ChoiceWithBest, new Dictionary<string, string>
        {
            ["NAME"] = fortress.Name,
            ["WAVES"] = best.Waves.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["KILLS"] = best.Kills.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
    }

    private void Fault(Exception ex)
    {
        _logger.LogError($"[LongDefeat] the start flow failed: {ex}");
        ShowStartFailed(_menu, _logger);
    }

    /// <summary>
    /// Shows the start-failed notice and never throws. The client's game manager calls it after a load that opened no mission, so
    /// the player sees the same notice the main menu shows when the start flow fails.
    /// </summary>
    internal static void ShowStartFailed(ILongDefeatMenuAdapter menu, IModLogger logger)
    {
        try
        {
            menu.Notify(StartFailedMessage);
        }
        catch (Exception ex)
        {
            logger.LogWarning($"[LongDefeat] the start-failed notice could not be shown: {ex.Message}");
        }
    }
}
