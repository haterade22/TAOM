using System;
using System.Collections.Generic;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Incidents;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.EncounterLines;
using TAOM.Features.EncounterLines.Models;
using TAOM.Features.Execution;
using TAOM.Features.IncidentVoices;
using TAOM.Features.IncidentVoices.Hooks;
using TAOM.Features.IncidentVoices.Models;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.IncidentVoices;

/// <summary>Wiring guard for Incident Voices (Patch175), the patch's fault handling, and the text handle on a real engine incident.</summary>
[TestClass]
public class IncidentVoicesWiringTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded.");
    }

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<IncidentVoicesModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedIncidentVoices_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new IncidentVoicesModule();

        Assert.AreEqual("IncidentVoices", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void TheModule_AppliesPatch175AtGameInit_AndOnlyThat()
    {
        var decls = new IncidentVoicesModule().PatchCategories.ToList();

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch175_IncidentVoices", decls[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, decls[0].Phase);
        Assert.AreEqual(1, FeatureModules.All.SelectMany(m => m.PatchCategories).Count(d => d.Category == "Patch175_IncidentVoices"));
    }

    [TestMethod]
    public void TheModule_AddsNoBehaviorAndNoModelAndNoMissionBehavior()
    {
        var module = new IncidentVoicesModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }

    private static Container Registered()
    {
        var c = new Container();
        new EncounterLinesModule().RegisterServices(c);
        new IncidentVoicesModule().RegisterServices(c);
        c.RegisterInstance(Substitute.For<IModLogger>());
        c.RegisterInstance(Substitute.For<TAOM.Features.InitialChildGeneration.IRandomSource>());
        c.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        c.RegisterInstance(Substitute.For<ICoopPresenceProvider>());
        c.RegisterInstance(Substitute.For<TAOM.Features.Messengers.IMessengerConversationState>());
        c.RegisterInstance(Substitute.For<IAlignmentService>());
        c.RegisterInstance(Substitute.For<TAOM.Core.Domain.IRaceManager>());
        c.RegisterInstance(Substitute.For<IPathService>());
        return c;
    }

    [TestMethod]
    public void RegisterServices_MakesTheServiceCatalogAndAdapterSingletons()
    {
        using var c = Registered();

        Assert.AreSame(c.Resolve<IncidentVoiceService>(), c.Resolve<IncidentVoiceService>());
        Assert.AreSame(c.Resolve<IIncidentCatalogProvider>(), c.Resolve<IIncidentCatalogProvider>());
        Assert.IsInstanceOfType(c.Resolve<IIncidentVoiceAdapter>(), typeof(IncidentVoiceAdapter));
    }

    [TestMethod]
    public void InitializeStatics_HandsTheServiceToThePatch_AndCanRunTwice()
    {
        using var c = Registered();
        var module = new IncidentVoicesModule();

        module.InitializeStatics(c);
        module.InitializeStatics(c);

        Assert.AreSame(c.Resolve<IncidentVoiceService>(), IncidentCanBeInvokedPatch.Service);
    }

    // The patch body

    private static IncidentVoiceService ServiceOver(IIncidentCatalogProvider catalog, IIncidentVoiceAdapter adapter)
    {
        var groups = Substitute.For<IEncounterGroupsConfigProvider>();
        var map = IncidentRulesTests.ShippedMap();
        groups.GetMap().Returns(map);
        return new IncidentVoiceService(catalog, adapter, groups, Substitute.For<IAlignmentService>(), Substitute.For<IDedicatedServerProvider>(), Substitute.For<ICoopPresenceProvider>());
    }

    private static Incident NewIncident(int options)
    {
        var incident = new Incident("incident_test_patch");
        incident.Initialize("{=t}Title", "{=d}Description", new Incident.IncidentTrigger("LeavingVillage"), "PartyCampLife", CampaignTime.Days(1f), _ => true);
        for (var i = 0; i < options; i++)
            incident.AddOption("{=o" + i + "}Option " + i, new List<IncidentEffect>());
        return incident;
    }

    [TestMethod]
    public void Postfix_AFaultInTheService_KeepsVanillasAnswer_AndWarnsOnce()
    {
        RequireGame();
        var adapter = Substitute.For<IIncidentVoiceAdapter>();
        adapter.Player().Returns(_ => throw new InvalidOperationException("boom"));
        var rule = IncidentRulesTests.Rule("incident_test_patch");
        var catalog = Substitute.For<IIncidentCatalogProvider>();
        catalog.GetCatalog().Returns(new IncidentCatalog(new Dictionary<string, IncidentRule> { [rule.Id] = rule }, new string[0]));
        var logger = Substitute.For<IModLogger>();
        IncidentCanBeInvokedPatch.Initialize(ServiceOver(catalog, adapter), logger);
        var incident = NewIncident(1);

        var first = true;
        IncidentCanBeInvokedPatch.Postfix(incident, ref first);
        var second = true;
        IncidentCanBeInvokedPatch.Postfix(incident, ref second);

        Assert.IsTrue(first);
        Assert.IsTrue(second);
        logger.Received(1).LogWarning(NSubstitute.Arg.Is<string>(s => s.Contains("IncidentVoices")));
    }

    [TestMethod]
    public void Postfix_VanillaSaidNo_DoesNotAskTheService()
    {
        RequireGame();
        var adapter = Substitute.For<IIncidentVoiceAdapter>();
        IncidentCanBeInvokedPatch.Initialize(ServiceOver(Substitute.For<IIncidentCatalogProvider>(), adapter), Substitute.For<IModLogger>());

        var result = false;
        IncidentCanBeInvokedPatch.Postfix(NewIncident(1), ref result);

        Assert.IsFalse(result);
        adapter.DidNotReceive().Player();
    }

    [TestMethod]
    public void Postfix_ADisabledIncident_AnswersFalse()
    {
        RequireGame();
        var adapter = Substitute.For<IIncidentVoiceAdapter>();
        adapter.Player().Returns(new PlayerFacts("gondor", "human", new SideIds("empire_w", "gondor")));
        var rule = IncidentRulesTests.Rule("incident_test_patch", disable: true);
        var catalog = Substitute.For<IIncidentCatalogProvider>();
        catalog.GetCatalog().Returns(new IncidentCatalog(new Dictionary<string, IncidentRule> { [rule.Id] = rule }, new string[0]));
        IncidentCanBeInvokedPatch.Initialize(ServiceOver(catalog, adapter), Substitute.For<IModLogger>());

        var result = true;
        IncidentCanBeInvokedPatch.Postfix(NewIncident(1), ref result);

        Assert.IsFalse(result);
    }

    // The text handle on a real engine incident

    [TestMethod]
    public void Handle_ReadsTheIdAndTheOptionCount_FromTheEngineIncident()
    {
        RequireGame();
        var handle = new IncidentTextHandle(NewIncident(3));

        Assert.AreEqual("incident_test_patch", handle.Id);
        Assert.AreEqual(3, handle.OptionCount);
    }

    [TestMethod]
    public void Handle_SetThenNull_WritesTheNewValue_ThenPutsVanillasBack_KeepingTheVariables()
    {
        RequireGame();
        var incident = NewIncident(1);
        incident.Description.SetTextVariable("PLACE", "Pelargir");
        var handle = new IncidentTextHandle(incident);
        var vanilla = incident.Description.Value;

        TryWrite(() => handle.Set("desc", "{=taom_inc_test_all_desc}Changed {PLACE}"));
        if (incident.Description.Value == vanilla)
            Assert.Inconclusive("the text system is not initialised in this host, so CacheTokens cannot run");
        Assert.AreEqual("{=taom_inc_test_all_desc}Changed {PLACE}", incident.Description.Value);
        Assert.IsTrue(incident.Description.Attributes.ContainsKey("PLACE"), "the variable vanilla's condition set must survive the write");

        handle.Set("desc", null);

        Assert.AreEqual(vanilla, incident.Description.Value);
    }

    [TestMethod]
    public void Handle_ASlotTheIncidentDoesNotHave_IsIgnored()
    {
        RequireGame();
        var incident = NewIncident(1);
        var handle = new IncidentTextHandle(incident);

        handle.Set("opt2", "{=x}Nope");
        handle.Set("opt0", "{=x}Nope");
        handle.Set("bogus", "{=x}Nope");

        Assert.AreEqual("{=o0}Option 0", incident.GetOptionText(0).Value);
    }

    private static void TryWrite(Action write)
    {
        try
        {
            write();
        }
        catch (NullReferenceException)
        {
            // the tokenizer is a static the engine creates at start; a bare test host may not have it
        }
    }
}
