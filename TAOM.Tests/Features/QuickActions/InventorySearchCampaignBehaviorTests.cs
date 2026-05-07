using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.QuickActions;
using TAOM.Features.QuickActions.Hooks;

namespace TAOM.Tests.Features.QuickActions;

[TestClass]
public class InventorySearchCampaignBehaviorTests
{
    private IQuickActionsSettingsProvider _settings = null!;
    private IModLogger _logger = null!;
    private InventorySearchCampaignBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IQuickActionsSettingsProvider>();
        _logger = Substitute.For<IModLogger>();
        _sut = new InventorySearchCampaignBehavior(_settings, _logger);
    }

    [TestMethod]
    public void IsSearchAvailable_DefaultsToTrue_BeforeAnyEvent()
    {
        Assert.IsTrue(_sut.IsSearchAvailable);
    }

    // --- TickEvent reconciliation ---

    [TestMethod]
    public void OnTick_McmChangedToFalse_FlipsInternalFlag()
    {
        _settings.EnableInventorySearch.Returns(false);

        // Simulate event invocation by calling the private handler via reflection alternative:
        // Since the handler is private, we exercise it via the public surface — invoke
        // SyncData with a fake datastore to confirm field is tracked, then trigger reconciliation.
        // Simpler: directly test that after RegisterEvents would fire, internal state aligns.
        // We bypass RegisterEvents (requires Campaign.Current) and call OnTick via reflection-free path.

        var method = typeof(InventorySearchCampaignBehavior).GetMethod(
            "OnTick",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        method.Invoke(_sut, new object[] { 0.016f });

        Assert.IsFalse(_sut.IsSearchAvailable);
    }

    [TestMethod]
    public void OnTick_McmStillTrue_NoChange()
    {
        _settings.EnableInventorySearch.Returns(true);

        var method = typeof(InventorySearchCampaignBehavior).GetMethod(
            "OnTick",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        method.Invoke(_sut, new object[] { 0.016f });

        Assert.IsTrue(_sut.IsSearchAvailable);
    }

    [TestMethod]
    public void OnTick_McmFlipsBackToTrue_ReconcilesFromFalse()
    {
        // Start at false:
        _settings.EnableInventorySearch.Returns(false);
        var tick = typeof(InventorySearchCampaignBehavior).GetMethod(
            "OnTick",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        tick.Invoke(_sut, new object[] { 0.016f });
        Assert.IsFalse(_sut.IsSearchAvailable);

        // User flips MCM back on:
        _settings.EnableInventorySearch.Returns(true);
        tick.Invoke(_sut, new object[] { 0.016f });
        Assert.IsTrue(_sut.IsSearchAvailable);
    }

    // --- OnNewGameCreated seed ---

    [TestMethod]
    public void OnNewGameCreated_SeedsFromMcm_True()
    {
        _settings.EnableInventorySearch.Returns(true);
        var method = typeof(InventorySearchCampaignBehavior).GetMethod(
            "OnNewGameCreated",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        method.Invoke(_sut, new object?[] { null });

        Assert.IsTrue(_sut.IsSearchAvailable);
    }

    [TestMethod]
    public void OnNewGameCreated_SeedsFromMcm_False()
    {
        _settings.EnableInventorySearch.Returns(false);
        var method = typeof(InventorySearchCampaignBehavior).GetMethod(
            "OnNewGameCreated",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        method.Invoke(_sut, new object?[] { null });

        Assert.IsFalse(_sut.IsSearchAvailable);
    }

    // --- OnGameLoaded reconciliation ---

    [TestMethod]
    public void OnGameLoaded_McmDisagrees_ReconcilesToMcm()
    {
        _settings.EnableInventorySearch.Returns(false);
        var method = typeof(InventorySearchCampaignBehavior).GetMethod(
            "OnGameLoaded",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        method.Invoke(_sut, new object?[] { null });

        Assert.IsFalse(_sut.IsSearchAvailable);
    }
}
