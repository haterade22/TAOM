using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.FiefManagement;
using TAOM.Features.FiefManagement.Hooks;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.FiefManagement;

// Closes #177 (P1 audit-tests). Pre-this, FiefHubCampaignBehavior had ZERO test coverage on its
// 5 callbacks (RegisterEvents, OnSessionLaunched, OnNewGameCreated, OnGameLoaded, SyncData) —
// ADR-008's 80% behavior-hook target was entirely unmet for this feature even though FiefHubService
// itself had strong coverage (22 tests on 8 methods).
//
// Three callbacks delegate cleanly to mockable interfaces (OnNewGameCreated, OnGameLoaded,
// SyncData) so they are directly tested below. The remaining two (RegisterEvents and
// OnSessionLaunched) touch sealed engine classes (`CampaignEvents`, `CampaignGameStarter`) and are
// covered by the source-content pattern established in #191 — assert the production source contains
// the required wiring lines. Reverting either line in production turns this test red.
[TestClass]
public class FiefHubCampaignBehaviorTests
{
    private IFiefHubMenuPresenter _presenter = null!;
    private IFiefManagementSettingsProvider _settings = null!;
    private FiefHubCampaignBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _presenter = Substitute.For<IFiefHubMenuPresenter>();
        _settings = Substitute.For<IFiefManagementSettingsProvider>();
        _sut = new FiefHubCampaignBehavior(_presenter, _settings);
    }

    // --- Direct callback delegation tests ---

    [TestMethod]
    public void OnNewGameCreated_CallsPresenterReset()
    {
        // Reflection invoke because OnNewGameCreated is private. CampaignEvents call sites
        // dispatch via delegate so this models the production call shape.
        var method = typeof(FiefHubCampaignBehavior)
            .GetMethod("OnNewGameCreated", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(method, "OnNewGameCreated must be a private instance method on FiefHubCampaignBehavior");

        method.Invoke(_sut, new object[] { null! });

        _presenter.Received(1).Reset();
    }

    [TestMethod]
    public void OnGameLoaded_CallsPresenterReset()
    {
        var method = typeof(FiefHubCampaignBehavior)
            .GetMethod("OnGameLoaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(method, "OnGameLoaded must be a private instance method on FiefHubCampaignBehavior");

        method.Invoke(_sut, new object[] { null! });

        _presenter.Received(1).Reset();
    }

    [TestMethod]
    public void SyncData_DoesNotTouchDataStore()
    {
        // Per the behavior comment: "Selected-index cursor is transient — not persisted across
        // save/load." This test pins the no-op contract so a future refactor that adds persistence
        // is forced to update the test along with the behavior.
        var dataStore = Substitute.For<IDataStore>();

        _sut.SyncData(dataStore);

        // Bare invocation: no SyncData<T>(string, ref T) calls, regardless of overload.
        Assert.AreEqual(0, dataStore.ReceivedCalls().Count(),
            "FiefHubCampaignBehavior.SyncData must remain a no-op (selected-index is transient state).");
    }

    [TestMethod]
    public void Behavior_IsCampaignBehaviorBase()
    {
        // Without this, campaignStarter.AddBehavior(...) wouldn't accept the behavior at runtime.
        Assert.IsInstanceOfType(_sut, typeof(CampaignBehaviorBase));
    }

    // --- Source-content wiring tests (engine-coupled callback coverage) ---

    [TestMethod]
    public void RegisterEvents_SubscribesAllExpectedCampaignEvents()
    {
        // RegisterEvents itself touches sealed CampaignEvents — can't be invoked in unit tests.
        // Read the source and assert all three expected subscriptions are present. A future
        // refactor that drops a subscription goes red here.
        var source = ReadProjectSource("Main", "Features", "FiefManagement", "Hooks",
            "FiefHubCampaignBehavior.cs");
        if (source == null)
            Assert.Inconclusive("FiefHubCampaignBehavior.cs not found — run from repo root");

        StringAssert.Contains(source, "CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched)",
            "Must subscribe to OnSessionLaunchedEvent to register the fief_hub game menu.");
        StringAssert.Contains(source, "CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated)",
            "Must subscribe to OnNewGameCreatedEvent so presenter resets at campaign start.");
        StringAssert.Contains(source, "CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded)",
            "Must subscribe to OnGameLoadedEvent so presenter resets on save-load.");
    }

    [TestMethod]
    public void OnSessionLaunched_RegistersFiefHubMenuAndOptions()
    {
        // OnSessionLaunched calls starter.AddGameMenu / AddGameMenuOption on the sealed
        // CampaignGameStarter. Source-content assertion that the menu + 4 options are registered.
        var source = ReadProjectSource("Main", "Features", "FiefManagement", "Hooks",
            "FiefHubCampaignBehavior.cs");
        if (source == null)
            Assert.Inconclusive("FiefHubCampaignBehavior.cs not found — run from repo root");

        var normalizedSource = source.Replace("\r\n", "\n");

        StringAssert.Contains(normalizedSource, "starter.AddGameMenu(\n            \"fief_hub\"",
            "Must add the 'fief_hub' game menu via starter.AddGameMenu.");
        StringAssert.Contains(source, "\"fief_hub_prev\"",
            "Must register the 'previous fief' menu option.");
        StringAssert.Contains(source, "\"fief_hub_next\"",
            "Must register the 'next fief' menu option.");
        StringAssert.Contains(source, "\"fief_hub_manage\"",
            "Must register the 'manage this fief' menu option (the GameState push site Codex #36 caught).");
        StringAssert.Contains(source, "\"fief_hub_leave\"",
            "Must register the 'leave' menu option.");
    }

    [TestMethod]
    public void MainSubModule_AddsFiefHubCampaignBehavior()
    {
        var source = ReadProjectSource("Main", "SubModule.cs");
        if (source == null)
            Assert.Inconclusive("Main/SubModule.cs not found");

        StringAssert.Contains(source, "new FiefHubCampaignBehavior(",
            "Main/SubModule.cs::OnGameStart must add a FiefHubCampaignBehavior via " +
            "campaignStarter.AddBehavior. Without it, the F6 fief-hub menu never registers.");
    }

    // --- Helpers ---

    private static string ReadProjectSource(params string[] relativeParts)
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(new[] { dir }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }
}
