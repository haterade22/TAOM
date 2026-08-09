using System;
using System.IO;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AutoResolveDiagnostics;

namespace TAOM.Tests.Features.AutoResolveDiagnostics;

/// <summary>
/// Wiring is verified two ways, because each catches what the other cannot.
///
/// (1) Against a REAL DryIoc container — a type with two public constructors registers fine in
///     source but throws UnableToSelectSinglePublicConstructorFromMultiple inside OnSubModuleLoad,
///     a CTD before the main menu, with every other unit test green.
/// (2) Against the source text of IoC.cs and SubModule.cs — drop either line and the diagnostic
///     silently never runs, which for a diagnostic is the worst failure available: an empty log
///     reads as "no battles happened" rather than "the logger was never wired".
/// </summary>
[TestClass]
public class AutoResolveDiagnosticsWiringTests
{
    private static IContainer BuildContainer()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        AutoResolveDiagnosticsIoC.RegisterAutoResolveDiagnosticsFeature(container);
        return container;
    }

    [TestMethod]
    public void RegisterFeature_AgainstARealContainer_DoesNotThrow()
    {
        using var container = BuildContainer();
        Assert.IsNotNull(container);
    }

    [TestMethod]
    public void Resolve_Behavior_Succeeds()
    {
        using var container = BuildContainer();
        Assert.IsNotNull(container.Resolve<AutoResolveDiagnosticsBehavior>());
    }

    [TestMethod]
    public void Resolve_Adapter_Succeeds()
    {
        using var container = BuildContainer();
        Assert.IsNotNull(container.Resolve<IMapEventBattleLogAdapter>());
    }

    [TestMethod]
    public void Resolve_CensusAdapter_Succeeds()
    {
        using var container = BuildContainer();
        Assert.IsNotNull(container.Resolve<ITroopCensusAdapter>());
    }

    [TestMethod]
    public void Resolve_Behavior_IsASingleton()
    {
        using var container = BuildContainer();
        Assert.AreSame(container.Resolve<AutoResolveDiagnosticsBehavior>(),
                       container.Resolve<AutoResolveDiagnosticsBehavior>());
    }

    [TestMethod]
    public void RegisteredImplementations_HaveExactlyOnePublicConstructor()
    {
        Type[] implementations =
        {
            typeof(AutoResolveDiagnosticsSettingsProvider),
            typeof(MapEventBattleLogAdapter),
            typeof(TroopCensusAdapter),
            typeof(AutoResolveLogWriter),
            typeof(AutoResolveDiagnosticsBehavior),
        };

        foreach (var type in implementations)
        {
            Assert.AreEqual(1, type.GetConstructors().Length,
                $"{type.Name} must have exactly one public constructor — DryIoc throws at Register " +
                "time otherwise, which is a CTD before the main menu.");
        }
    }

    [TestMethod]
    public void IoC_RegistersTheFeature()
    {
        var source = ReadProjectSource(Path.Combine("Main", "IoC.cs"));
        if (source == null)
        {
            Assert.Inconclusive("Main/IoC.cs not found from the test working directory.");
            return;
        }

        StringAssert.Contains(source,
            "AutoResolveDiagnosticsIoC.RegisterAutoResolveDiagnosticsFeature(container)",
            "the feature must be registered in IoC.cs or nothing resolves at runtime");
    }

    [TestMethod]
    public void SubModule_AddsTheBehaviorToTheCampaign()
    {
        var source = ReadProjectSource(Path.Combine("Main", "SubModule.cs"));
        if (source == null)
        {
            Assert.Inconclusive("Main/SubModule.cs not found from the test working directory.");
            return;
        }

        StringAssert.Contains(source, "AutoResolveDiagnosticsBehavior>()",
            "the behavior must be added via campaignStarter.AddBehavior or it never subscribes " +
            "to MapEventEnded, and the log stays empty with no error");
    }

    private static string? ReadProjectSource(string relativePath)
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = dir.Parent;
        }
        return null;
    }
}
