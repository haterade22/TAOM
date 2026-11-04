using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Autorun;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LongDefeat.Autorun;

/// <summary>The engine-facing halves of the autorun are not loadable in a test, so their wiring is pinned from the source.</summary>
[TestClass]
public class LongDefeatAutorunWiringTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void TheHudView_RefusesTheFocusMenu_WhenTheAutorunArmedThisProcess()
    {
        var source = RepoPaths.ReadSource("Client/Features/LongDefeat/LongDefeatHudView.cs", stripComments: true);

        StringAssert.Contains(source, "LongDefeatAutorunPlan.ArmedInThisProcess");
        StringAssert.Contains(source, "IsOpeningEscapeMenuOnFocusChangeAllowed");
    }

    [TestMethod]
    public void TheClientSubModule_TicksTheLongDefeatAutorunDriver_FromTheApplicationTick()
    {
        var source = RepoPaths.ReadSource("Client/ClientSubModule.cs", stripComments: true);

        StringAssert.Contains(source, "LongDefeatAutorunStart.Driver(");
        StringAssert.Contains(source, "_longDefeatAutorun?.Tick(dt)");
    }

    [TestMethod]
    public void TheClientComposition_ReplacesTheNullAutorunHost()
    {
        var source = RepoPaths.ReadSource("Client/TaomClientComposition.cs", stripComments: true);

        StringAssert.Contains(source, "Register<ILongDefeatAutorunHost, LongDefeatAutorunHost>");
    }

    [TestMethod]
    public void TheLauncher_WritesAnArmedRunsRecordsUnderThePlansFolder()
    {
        var source = RepoPaths.ReadSource("Main/Features/LongDefeat/Hooks/LongDefeatMissionLauncher.cs", stripComments: true);

        StringAssert.Contains(source, "setup.RecordsFolder");
        StringAssert.Contains(source, "LongDefeatRunRecords.ForFolder(");
        // The chosen records must reach the mission logic: without the argument CreateBehaviors falls back to the default folder.
        StringAssert.Contains(source, "CreateBehaviors(plan, defender, attacker, records)");
    }

    [TestMethod]
    public void TheModule_RegistersThePlanTheNullHostAndTheDriver_AndNothingBuildsUnarmed()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IOutcomeLog>());
        new LongDefeatModule().RegisterServices(container);

        Assert.IsInstanceOfType(container.Resolve<ILongDefeatAutorunHost>(), typeof(NullLongDefeatAutorunHost));
        Assert.IsFalse(container.Resolve<LongDefeatAutorunPlan>().Armed || LongDefeatAutorunPlan.ArmedInThisProcess);
        Assert.AreSame(container.Resolve<LongDefeatAutorunDriver>(), container.Resolve<LongDefeatAutorunDriver>());
    }

    [TestMethod]
    public void AnUnarmedPlan_BuildsNoDriver_AndAProblemIsLoggedOnce()
    {
        var logger = Substitute.For<IModLogger>();
        var built = 0;

        var unarmed = LongDefeatAutorunStart.Driver(new LongDefeatAutorunPlan(), () => { built++; return null!; }, logger);
        var broken = LongDefeatAutorunStart.Driver(LongDefeatAutorunPlan.Resolve(_ => "x", _ => false, _ => ""), () => { built++; return null!; }, logger);

        Assert.IsNull(unarmed);
        Assert.IsNull(broken);
        Assert.AreEqual(0, built);
        logger.Received(1).LogWarning(NSubstitute.Arg.Is<string>(s => s.Contains("not armed")));
    }
}
