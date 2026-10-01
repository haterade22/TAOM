using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.BattleCorpses;
using TAOM.Features.BattleCorpses.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BattleCorpses;

/// <summary>
/// Wiring regression guard for battle corpse cleanup (#701). Nothing reaches the feature through a patch
/// or a model: the module list adds the mission behavior, and the MCM button calls the notifier. Drop any
/// link and battles silently keep native's hour-long corpse timer.
/// </summary>
[TestClass]
public class BattleCorpsesWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheBattleCorpsesModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<BattleCorpsesModule>().Count(),
            "BattleCorpsesModule must be listed exactly once in Main/Composition/FeatureModules.cs.");
    }

    [TestMethod]
    public void Module_DeclaresTheMissionBehavior()
    {
        var decls = new BattleCorpsesModule().MissionBehaviors;

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual(typeof(BattleCorpseMissionBehavior), decls[0].BehaviorType);
    }

    [TestMethod]
    public void Module_RegistersEverythingTheBehaviorAndAdvisorNeed()
    {
        // Resolving builds the objects without touching the engine: the adapter reads options only when asked.
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        new BattleCorpsesModule().RegisterServices(container);

        Assert.IsNotNull(container.Resolve<BattleCorpsePolicy>());
        Assert.IsNotNull(container.Resolve<BattleSettingsAdvisor>());
        Assert.IsNotNull(container.Resolve<IGraphicsOptionsAdapter>());
        Assert.AreSame(container.Resolve<BattleCorpsePolicy>(), container.Resolve<BattleCorpsePolicy>(),
            "the policy's warn-once latches only work as a singleton");
    }

    [TestMethod]
    public void McmButton_AppliesThroughTheNotifier()
    {
        var src = RepoPaths.ReadSource("Main/Features/TaomSettings.cs", stripComments: true);

        StringAssert.Contains(src, "BattleSettingsAdviceNotifier.Apply(");
    }

    [TestMethod]
    public void Behavior_NotesOffThreadNativeWrites()
    {
        // csharp-architecture.md: a new native write carries the MissionThreadGuard tripwire.
        var src = RepoPaths.ReadSource("Main/Features/BattleCorpses/Hooks/BattleCorpseMissionBehavior.cs", stripComments: true);

        StringAssert.Contains(src, "MissionThreadGuard.NoteCall(");
    }
}
