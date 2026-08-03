using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.MainMenuCustomizer;

namespace TAOM.Tests.Features.MainMenuCustomizer;

[TestClass]
public class MainMenuCustomizerServiceTests
{
    private IModuleMenuAdapter _moduleMenuAdapter;
    private IModLogger _logger;
    private MainMenuCustomizerService _sut;

    [TestInitialize]
    public void Setup()
    {
        _moduleMenuAdapter = Substitute.For<IModuleMenuAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new MainMenuCustomizerService(_moduleMenuAdapter, _logger);
    }

    [TestMethod]
    public void CustomizeMenu_HidesNewCampaignOption()
    {
        _sut.CustomizeMenu();

        _moduleMenuAdapter.Received(1).HideOption("StoryModeNewGame");
    }

    [TestMethod]
    public void CustomizeMenu_DoesNotHideSavedGames()
    {
        _sut.CustomizeMenu();

        _moduleMenuAdapter.DidNotReceive().HideOption("CampaignResumeGame");
    }

    [TestMethod]
    public void CustomizeMenu_DoesNotHideContinueCampaign()
    {
        _sut.CustomizeMenu();

        _moduleMenuAdapter.DidNotReceive().HideOption("ContinueCampaign");
    }

    [TestMethod]
    public void CustomizeMenu_RenamesSandboxToEnterTheAgeOfMen()
    {
        _sut.CustomizeMenu();

        _moduleMenuAdapter.Received(1).RenameOption("SandBoxNewGame", "{=taom_main_menu_new_game}Enter The Age Of Men");
    }

    [TestMethod]
    public void CustomizeMenu_LogsApplied()
    {
        _moduleMenuAdapter.HideOption(Arg.Any<string>()).Returns(true);
        _moduleMenuAdapter.RenameOption(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        _sut.CustomizeMenu();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("MainMenuCustomizer")));
    }

    // --- Multiplayer field report 2026-08-03 §9.8 — headless log flood ---
    //
    // OnBeforeInitialModuleScreenSetAsRoot re-fires per screen-root-set, and a headless dedicated
    // server sets it thousands of times per boot. With StoryMode/SandBox absent there, both options
    // miss on every single call: one reported server log carried 4,803 MainMenuCustomizer lines.
    // The customization itself must still run each time (the engine can rebuild the option list),
    // so the fix is to dedupe the LOGGING, not to skip the work.

    [TestMethod]
    public void CustomizeMenu_OptionsMissing_WarnsOncePerOptionAcrossManyCalls()
    {
        _moduleMenuAdapter.HideOption(Arg.Any<string>()).Returns(false);
        _moduleMenuAdapter.RenameOption(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        for (var i = 0; i < 50; i++)
            _sut.CustomizeMenu();

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("StoryModeNewGame")));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("SandBoxNewGame")));
    }

    [TestMethod]
    public void CustomizeMenu_ManyCalls_StillAppliesEveryTime()
    {
        // Dedupe the log, never the work: the engine can rebuild the initial-state options between
        // screen-root sets, and skipping would silently lose the customization on a real client.
        for (var i = 0; i < 5; i++)
            _sut.CustomizeMenu();

        _moduleMenuAdapter.Received(5).HideOption("StoryModeNewGame");
        _moduleMenuAdapter.Received(5).RenameOption("SandBoxNewGame", Arg.Any<string>());
    }

    [TestMethod]
    public void CustomizeMenu_ManyCalls_LogsAppliedOnce()
    {
        _moduleMenuAdapter.HideOption(Arg.Any<string>()).Returns(true);
        _moduleMenuAdapter.RenameOption(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        for (var i = 0; i < 50; i++)
            _sut.CustomizeMenu();

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("menu customization applied")));
    }

    [TestMethod]
    public void CustomizeMenu_OptionAppearsLater_WarnsOnceThenLogsApplied()
    {
        // A client that opens the main menu before StoryMode finishes registering: the first pass
        // misses and warns, a later pass succeeds. The success must still be reported.
        _moduleMenuAdapter.HideOption(Arg.Any<string>()).Returns(false);
        _moduleMenuAdapter.RenameOption(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        _sut.CustomizeMenu();

        _moduleMenuAdapter.HideOption(Arg.Any<string>()).Returns(true);
        _moduleMenuAdapter.RenameOption(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _sut.CustomizeMenu();

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("StoryModeNewGame")));
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("menu customization applied")));
    }
}
