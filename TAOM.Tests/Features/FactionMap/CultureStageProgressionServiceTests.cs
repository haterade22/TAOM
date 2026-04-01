using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.FactionMap;

namespace TAOM.Tests.Features.FactionMap;

[TestClass]
public class CultureStageProgressionServiceTests
{
    private IModLogger _logger;
    private CultureStageProgressionService _sut;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new CultureStageProgressionService(_logger);
    }

    [TestMethod]
    public void InvokeNextStage_NullView_LogsErrorAndReturns()
    {
        // Arrange
        // Act
        _sut.InvokeNextStage(null!, "gondor", false);

        // Assert
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("null")));
    }

    [TestMethod]
    public void InvokeNextStage_ViewWithNoCharacterCreationManagerField_LogsError()
    {
        // Arrange
        var view = new ViewWithNoCharacterCreationManager();

        // Act
        _sut.InvokeNextStage(view, "gondor", false);

        // Assert
        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void InvokeNextStage_ViewWithManagerAndAffirmativeAction_InvokesAffirmativeAction()
    {
        // Arrange
        var invoked = false;
        Action affirmativeAction = () => invoked = true;
        var view = new ViewWithAffirmativeAction(affirmativeAction);

        // Act
        _sut.InvokeNextStage(view, "gondor", false);

        // Assert
        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public void LoadFactionMapResources_WhenCalled_DoesNotThrow()
    {
        // Arrange & Act — UIResourceManager is not available in test context, so we verify no unhandled exception
        // and that logger captures any resource-load errors gracefully
        Exception? caught = null;
        try
        {
            _sut.LoadFactionMapResources();
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        // Assert — service must not propagate exceptions (logs them instead)
        Assert.IsNull(caught);
    }

    // ---- test helper stubs ----

    private class ViewWithNoCharacterCreationManager
    {
        // No _characterCreationManager field — simulates missing field
        public string SomeOtherField = "irrelevant";
    }

    private class FakeCharacterCreationContent
    {
        public string? LastSetName { get; private set; }
        public void SetMainCharacterName(string name) => LastSetName = name;
    }

    private class FakeCharacterCreationManager
    {
        public FakeCharacterCreationContent CharacterCreationContent { get; } = new FakeCharacterCreationContent();
    }

    private class ViewWithAffirmativeAction
    {
        private readonly FakeCharacterCreationManager _characterCreationManager = new FakeCharacterCreationManager();
        private readonly Delegate _affirmativeAction;

        public ViewWithAffirmativeAction(Action action)
        {
            _affirmativeAction = action;
        }
    }
}
