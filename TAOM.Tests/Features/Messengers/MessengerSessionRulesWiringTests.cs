using System.Linq;
using DryIoc;
using TAOM.Tests.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using NSubstitute;
using TAOM.Features.Messengers;
using TAOM.Features.Messengers.Hooks;

namespace TAOM.Tests.Features.Messengers;

/// <summary>Wiring guard for the messenger session rules (#898): drop the module line and the lines come back.</summary>
[TestClass]
public class MessengerSessionRulesWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<MessengerSessionRulesModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedMessengerSessionRules_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new MessengerSessionRulesModule();

        Assert.AreEqual("MessengerSessionRules", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void TheModule_AppliesItsPatchAtGameInit_AndOnlyThat()
    {
        var decls = new MessengerSessionRulesModule().PatchCategories.ToList();

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch117_MessengerSessionRules", decls[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, decls[0].Phase);
    }

    [TestMethod]
    public void TheModule_AddsNoBehaviorAndNoModel()
    {
        var module = new MessengerSessionRulesModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }

    [TestMethod]
    public void InitializeStatics_HandsTheConversationStateToThePostfix()
    {
        var open = Substitute.For<IMessengerConversationState>();
        open.IsOpen.Returns(true);
        var closed = Substitute.For<IMessengerConversationState>();
        closed.IsOpen.Returns(false);
        try
        {
            using var c = new Container();
            c.RegisterInstance(open);
            new MessengerSessionRulesModule().InitializeStatics(c);
            var r = true;
            MessengerSessionRulesPatch.Postfix(ref r);
            Assert.IsFalse(r, "an open conversation clears a true result");
        }
        finally
        {
            using var c = new Container();
            c.RegisterInstance(closed);
            new MessengerSessionRulesModule().InitializeStatics(c);
        }

        var kept = true;
        MessengerSessionRulesPatch.Postfix(ref kept);
        Assert.IsTrue(kept, "a closed conversation leaves it alone");
    }

    [TestMethod]
    public void TheCategory_IsNotRegisteredByHandInIoC()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        Assert.IsFalse(src.Contains("Patch117") || src.Contains("MessengerSessionRules"), "Main/IoC.cs must not name the module");
    }
}
