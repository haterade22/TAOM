using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.EconomyDiagnostics;

namespace TAOM.Tests.Features.EconomyDiagnostics;

/// <summary>
/// Resolves the feature through a REAL DryIoc container.
///
/// Why this exists: `TownGoldLedger` shipped with two public constructors (`()` and `(int)`), and
/// DryIoc refuses to guess — it threw `UnableToSelectSinglePublicConstructorFromMultiple` from
/// `IoC.Configure()`, inside `SubModule.OnSubModuleLoad`, which is a hard crash to desktop **before
/// the main menu**. Every unit test passed and the build was clean, because the existing tests all
/// did `new TownGoldLedger(3)` directly and never touched the container. The sibling `*WiringTests`
/// in this repo assert on SubModule/IoC *source text*, which cannot catch a constructor-selection
/// failure either.
///
/// So the guard has to be an actual `Register` + `Resolve` round-trip. Registration is the half that
/// crashed (DryIoc validates constructor selection at `Register` time, not first resolve), and
/// resolution proves the object graph is satisfiable.
/// </summary>
[TestClass]
public class EconomyDiagnosticsWiringTests
{
    private static IContainer NewContainer()
    {
        var container = new Container();
        EconomyDiagnosticsIoC.RegisterEconomyDiagnosticsFeature(container);
        return container;
    }

    /// <summary>
    /// The exact call that crashed the game. It fails at Register, before any Resolve.
    /// </summary>
    [TestMethod]
    public void RegisterFeature_AgainstARealContainer_DoesNotThrow()
    {
        NewContainer();
    }

    [TestMethod]
    public void Resolve_TownGoldLedger_SucceedsAndUsesTheDefaultRetention()
    {
        var ledger = NewContainer().Resolve<ITownGoldLedger>();

        Assert.IsNotNull(ledger);

        // The parameterless ctor must be the one DryIoc picks, and it must give a usable window —
        // a zero-day window would make every report empty and read as "no drain".
        ledger.Record("town_G3", TownGoldFlow.DailyMint, 100);
        Assert.AreEqual(1, ledger.GetHistory("town_G3").Count);
    }

    /// <summary>
    /// The recorder patch and the console command both resolve the ledger independently; if the
    /// registration were transient they would write to and read from different instances, and the
    /// report would always be empty while the ledger silently filled.
    /// </summary>
    [TestMethod]
    public void Resolve_TownGoldLedger_IsASingleton()
    {
        var container = NewContainer();

        var first = container.Resolve<ITownGoldLedger>();
        first.Record("town_G3", TownGoldFlow.VillagerDelivery, -500);

        var second = container.Resolve<ITownGoldLedger>();

        Assert.AreSame(first, second, "the recorder and the console command must share one ledger");
        Assert.AreEqual(-500, second.GetHistory("town_G3")[0].AmountFor(TownGoldFlow.VillagerDelivery));
    }

    [TestMethod]
    public void Resolve_EconomyDiagnosticsBehavior_GetsItsLedgerInjected()
    {
        var container = NewContainer();

        var behavior = container.Resolve<EconomyDiagnosticsBehavior>();

        Assert.IsNotNull(behavior, "SubModule.cs resolves this to add it as a campaign behavior");
    }

    /// <summary>
    /// Guards the root cause rather than the symptom: any future second public constructor on a
    /// registered type reintroduces the same startup crash.
    /// </summary>
    [TestMethod]
    public void RegisteredImplementations_HaveExactlyOnePublicConstructor()
    {
        foreach (var type in new[] { typeof(TownGoldLedger), typeof(EconomyDiagnosticsBehavior) })
        {
            Assert.AreEqual(1, type.GetConstructors().Length,
                $"{type.Name} must expose exactly one PUBLIC constructor. DryIoc throws "
                + "UnableToSelectSinglePublicConstructorFromMultiple at registration time, which crashes "
                + "OnSubModuleLoad before the main menu. Make extra overloads internal.");
        }
    }
}
