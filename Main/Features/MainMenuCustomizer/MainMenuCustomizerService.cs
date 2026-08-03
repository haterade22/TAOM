using System.Collections.Generic;
using TAOM.Core.Logging;

namespace TAOM.Features.MainMenuCustomizer;

public class MainMenuCustomizerService : IMainMenuCustomizerService
{
    private readonly IModuleMenuAdapter _moduleMenuAdapter;
    private readonly IModLogger _logger;

    public MainMenuCustomizerService(IModuleMenuAdapter moduleMenuAdapter, IModLogger logger)
    {
        _moduleMenuAdapter = moduleMenuAdapter;
        _logger = logger;
    }

    // Field report 2026-08-03 §9.8 — one log line per outcome, not per call. CustomizeMenu runs on
    // every screen-root set; a headless dedicated server does that thousands of times per boot with
    // StoryMode/SandBox absent, so the unconditional logging produced 4,803 lines in one server log.
    // The customization still runs every call — only the logging is deduped, because the engine can
    // rebuild the initial-state options between sets and skipping the work would silently drop it.
    private readonly HashSet<string> _reportedMisses = new();
    private bool _appliedLogged;

    public void CustomizeMenu()
    {
        var hidden = _moduleMenuAdapter.HideOption("StoryModeNewGame");
        if (!hidden) ReportMissOnce("StoryModeNewGame", "hide");

        var renamed = _moduleMenuAdapter.RenameOption("SandBoxNewGame", "{=taom_main_menu_new_game}Enter The Age Of Men");
        if (!renamed) ReportMissOnce("SandBoxNewGame", "rename");

        if (hidden && renamed && !_appliedLogged)
        {
            _appliedLogged = true;
            _logger.LogInfo("MainMenuCustomizer: menu customization applied");
        }
    }

    private void ReportMissOnce(string id, string operation)
    {
        if (!_reportedMisses.Add(id)) return;
        _logger.LogWarning(
            $"MainMenuCustomizer: option '{id}' not found, skipping {operation}. " +
            "Expected on a headless/dedicated server, where StoryMode and SandBox are not loaded. " +
            "Further misses for this option are not logged.");
    }
}
