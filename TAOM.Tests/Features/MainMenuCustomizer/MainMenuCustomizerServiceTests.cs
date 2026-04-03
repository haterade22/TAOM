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
    public void CustomizeMenu_HidesSavedGamesOption()
    {
        _sut.CustomizeMenu();

        _moduleMenuAdapter.Received(1).HideOption("CampaignResumeGame");
    }

    [TestMethod]
    public void CustomizeMenu_HidesContinueCampaignOption()
    {
        _sut.CustomizeMenu();

        _moduleMenuAdapter.Received(1).HideOption("ContinueCampaign");
    }

    [TestMethod]
    public void CustomizeMenu_HidesNewCampaignOption()
    {
        _sut.CustomizeMenu();

        _moduleMenuAdapter.Received(1).HideOption("StoryModeNewGame");
    }

    [TestMethod]
    public void CustomizeMenu_RenamesSandboxToEnterTheAgeOfMen()
    {
        _sut.CustomizeMenu();

        _moduleMenuAdapter.Received(1).RenameOption("SandBoxNewGame", "Enter The Age Of Men");
    }

    [TestMethod]
    public void CustomizeMenu_LogsApplied()
    {
        _sut.CustomizeMenu();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("MainMenuCustomizer")));
    }
}
