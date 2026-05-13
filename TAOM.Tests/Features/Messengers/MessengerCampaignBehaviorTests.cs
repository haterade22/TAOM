using System.IO;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Messengers;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.Messengers;

// Wiring-regression test for the audit-motivating crash (#121, closed in b4b4de1).
//
// Original failure: commit 03a41b6 shipped Main/Features/Messengers/* (service + tests + docs +
// localization) but the diff did NOT include Main/IoC.cs::Configure adding
// MessengerIoC.RegisterMessengerFeature(container), and Main/SubModule.cs::OnGameStart never added
// MessengerCampaignBehavior to the campaign starter. Build was clean, all 1903 unit tests passed,
// and the encyclopedia hero-click NRE was the first signal in-game.
//
// All existing Messenger tests (MessengerService, MessengerStateStore, MessengerConfigProvider)
// mocked the adapter chain — none asserted the feature was actually plugged into the global IoC
// catalog. This test class closes that gap with two regression-grade source-file assertions
// (catch the exact #121 class) plus DryIoc + lifecycle checks for the behavior itself.
[TestClass]
public class MessengerCampaignBehaviorTests
{
    // --- Wiring catalog regression tests ---

    [TestMethod]
    public void MainIoCConfigure_IncludesMessengerFeatureRegistration()
    {
        var iocSource = ReadProjectSource("Main", "IoC.cs");
        if (iocSource == null)
            Assert.Inconclusive("Main/IoC.cs not found — run from repo root or check working directory");

        StringAssert.Contains(iocSource, "MessengerIoC.RegisterMessengerFeature(container);",
            "Main/IoC.cs::Configure must call MessengerIoC.RegisterMessengerFeature(container). " +
            "Audit-motivating regression: commit 03a41b6 shipped the feature WITHOUT this line and " +
            "crashed in-game on encyclopedia hero click. See GitHub issue #121.");
    }

    [TestMethod]
    public void MainSubModule_AddsMessengerCampaignBehavior()
    {
        var subModuleSource = ReadProjectSource("Main", "SubModule.cs");
        if (subModuleSource == null)
            Assert.Inconclusive("Main/SubModule.cs not found — run from repo root or check working directory");

        // Both `AddBehavior(...IoC.Resolve<MessengerCampaignBehavior>...)` and the namespace-qualified
        // form `Features.Messengers.MessengerCampaignBehavior` are accepted; the key invariant is that
        // the behavior reaches the campaign starter's behavior list via some AddBehavior call.
        bool hasResolveForm =
            subModuleSource.Contains("IoC.Resolve<TAOM.Features.Messengers.MessengerCampaignBehavior>()")
            || subModuleSource.Contains("IoC.Resolve<MessengerCampaignBehavior>()");
        Assert.IsTrue(hasResolveForm,
            "Main/SubModule.cs::OnGameStart must register MessengerCampaignBehavior via " +
            "campaignStarter.AddBehavior(IoC.Resolve<MessengerCampaignBehavior>()). " +
            "Audit-motivating regression: commit 03a41b6 shipped the feature WITHOUT this line. " +
            "See GitHub issue #121.");
    }

    // --- DryIoc registration smoke test ---

    [TestMethod]
    public void RegisterMessengerFeature_RegistersBehavior_WithAllDependencies()
    {
        var container = new Container();

        // MessengerCampaignBehavior pulls IMessengerService -> IMessengerConfigProvider, which in turn
        // takes IPathService + IModLogger. Mirror the core deps that Main/IoC.cs registers before
        // any feature module runs.
        container.RegisterInstance<IModLogger>(Substitute.For<IModLogger>());
        container.RegisterInstance<IPathService>(Substitute.For<IPathService>());
        MessengerIoC.RegisterMessengerFeature(container);

        var behavior = container.Resolve<MessengerCampaignBehavior>();

        Assert.IsNotNull(behavior,
            "MessengerIoC.RegisterMessengerFeature must register MessengerCampaignBehavior such " +
            "that the DryIoc container can resolve it. If this fails, the feature module's ctor " +
            "deps changed but the IoC registration did not.");
    }

    [TestMethod]
    public void RegisterMessengerFeature_RegistersService()
    {
        var container = new Container();
        container.RegisterInstance<IModLogger>(Substitute.For<IModLogger>());
        container.RegisterInstance<IPathService>(Substitute.For<IPathService>());
        MessengerIoC.RegisterMessengerFeature(container);

        Assert.IsNotNull(container.Resolve<IMessengerService>(),
            "IMessengerService must be resolvable after MessengerIoC.RegisterMessengerFeature.");
        Assert.IsNotNull(container.Resolve<IMessengerStateStore>(),
            "IMessengerStateStore must be resolvable after MessengerIoC.RegisterMessengerFeature.");
        Assert.IsNotNull(container.Resolve<IMessengerSettingsProvider>(),
            "IMessengerSettingsProvider must be resolvable after MessengerIoC.RegisterMessengerFeature.");
    }

    // --- Type sanity ---

    [TestMethod]
    public void Behavior_IsCampaignBehaviorBase()
    {
        // Without this, campaignStarter.AddBehavior(...) wouldn't accept the behavior at all.
        // Mirror of RacePersistenceBehaviorTests.Behavior_IsCampaignBehaviorBase.
        var behavior = new MessengerCampaignBehavior(
            Substitute.For<IMessengerService>(),
            Substitute.For<IMessengerStateStore>(),
            Substitute.For<IMessengerSettingsProvider>(),
            Substitute.For<IModLogger>());

        Assert.IsInstanceOfType(behavior, typeof(CampaignBehaviorBase));
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
