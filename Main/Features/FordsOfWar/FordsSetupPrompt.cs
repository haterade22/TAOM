using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// The main-menu flow of Fords of War: your faction, the enemy faction, the difficulty and, when <c>maps.json</c> lists more than
/// one scene, the map; then the client's game manager loads the match. Cancelling any prompt stops the flow and starts nothing.
/// <see cref="Start"/> is the prompt-free path the console command uses. Neither entry point throws: they run inside a menu
/// click, where an exception reaches the engine's UI loop. Every text is a <c>{=TAOM_FOW_...}</c> key; the menu adapter resolves it.
/// </summary>
internal sealed class FordsSetupPrompt
{
    private const string NoContentMessage = "{=TAOM_FOW_NoContent}Fords of War has no factions or maps to play on. Check fords_of_war/factions.json and maps.json.";
    private const string NoClientMessage = "{=TAOM_FOW_NoClient}Fords of War needs the game client and cannot start here.";
    internal const string StartFailedMessage = "{=TAOM_FOW_StartFailed}Fords of War could not start. The log has the reason.";

    private readonly FordsConfigCatalog _catalog;
    private readonly IFordsMenuAdapter _menu;
    private readonly IFordsOfWarGameManagerFactory _factory;
    private readonly IModLogger _logger;

    public FordsSetupPrompt(FordsConfigCatalog catalog, IFordsMenuAdapter menu, IFordsOfWarGameManagerFactory factory, IModLogger logger)
    {
        _catalog = catalog;
        _menu = menu;
        _factory = factory;
        _logger = logger;
    }

    /// <summary>Asks the questions in order and starts the match with the answers.</summary>
    public void Begin()
    {
        try
        {
            if (!FordsSetupChoices.HasContent(_catalog))
            {
                _menu.Notify(NoContentMessage);
                return;
            }

            _menu.Choose("{=TAOM_FOW_Prompt_YourFaction_Title}Choose Your Side",
                "{=TAOM_FOW_Prompt_YourFaction_Desc}Pick the faction whose army you will command. You give orders and spend gold; you do not fight.",
                FordsSetupChoices.Factions(_catalog), west => AskEnemy(west));
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    /// <summary>Starts a match from a finished setup. False, with the player told why, when no game manager could be built.</summary>
    public bool Start(FordsMatchSetup setup)
    {
        try
        {
            var manager = _factory.Create(setup);
            if (manager == null)
            {
                _logger.LogWarning("[FordsOfWar] the game manager factory returned null (no TAOM.Client); the match cannot start");
                _menu.Notify(NoClientMessage);
                return false;
            }

            _menu.StartGame(manager);
            return true;
        }
        catch (Exception ex)
        {
            Fault(ex);
            return false;
        }
    }

    /// <summary>
    /// Shows the start-failed notice and never throws. The mission exit after a failed start and the client's game manager after a
    /// failed load come here, so the player sees the same notice the main menu shows when the prompts fail.
    /// </summary>
    internal static void ShowStartFailed(IFordsMenuAdapter menu, IModLogger logger)
    {
        try
        {
            menu.Notify(StartFailedMessage);
        }
        catch (Exception ex)
        {
            logger.LogWarning($"[FordsOfWar] the start-failed notice could not be shown: {ex.Message}");
        }
    }

    private void AskEnemy(string west) =>
        Guarded(() => _menu.Choose("{=TAOM_FOW_Prompt_EnemyFaction_Title}Choose Your Enemy",
            "{=TAOM_FOW_Prompt_EnemyFaction_Desc}Pick the faction the computer commands against you.",
            FordsSetupChoices.Factions(_catalog), east => AskDifficulty(west, east)));

    private void AskDifficulty(string west, string east) =>
        Guarded(() => _menu.Choose("{=TAOM_FOW_Prompt_Difficulty_Title}Choose the Difficulty",
            "{=TAOM_FOW_Prompt_Difficulty_Desc}A harder enemy commander decides faster, makes fewer mistakes and earns more gold.",
            FordsSetupChoices.Difficulties(_catalog.Match.AiProfiles), profile => AskMap(west, east, profile)));

    private void AskMap(string west, string east, string profile) =>
        Guarded(() =>
        {
            if (_catalog.Maps.Count > 1)
            {
                _menu.Choose("{=TAOM_FOW_Prompt_Map_Title}Choose the Battlefield",
                    "{=TAOM_FOW_Prompt_Map_Desc}Pick the ground the three lanes run across.",
                    FordsSetupChoices.Maps(_catalog), scene => Finish(west, east, profile, scene));
                return;
            }

            Finish(west, east, profile, _catalog.Maps[0].SceneId);
        });

    private void Finish(string west, string east, string profile, string scene) =>
        Guarded(() =>
        {
            if (!_catalog.TryGetMap(scene, out var map))
            {
                _logger.LogError($"[FordsOfWar] scene '{scene}' vanished from maps.json between the prompt and the start");
                _menu.Notify(StartFailedMessage);
                return;
            }

            Start(FordsSetupChoices.Compose(map, west, east, profile));
        });

    private void Guarded(Action step)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    private void Fault(Exception ex)
    {
        _logger.LogError($"[FordsOfWar] the start flow failed: {ex}");
        try
        {
            _menu.Notify(StartFailedMessage);
        }
        catch (Exception)
        {
            // The notice is the last resort; a menu that cannot show it has nothing left to tell.
        }
    }
}
