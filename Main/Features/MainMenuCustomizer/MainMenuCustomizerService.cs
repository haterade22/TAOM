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

    public void CustomizeMenu()
    {
        _moduleMenuAdapter.HideOption("StoryModeNewGame");
        _moduleMenuAdapter.RenameOption("SandBoxNewGame", "{=taom_main_menu_new_game}Enter The Age Of Men");
        _logger.LogInfo("MainMenuCustomizer: menu customization applied");
    }
}
