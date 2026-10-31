using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.ReevesLedger;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.ReevesLedger;

/// <summary>
/// Wiring guard for the Reeve's Ledger (#857): the module line, its empty save footprint, the container graph, and the
/// shape rules the two mixins must keep (ADR-002 size, a latch behind every engine-called member, no settings read per frame).
/// The mixins need a running campaign, so those rules are pinned on the comment-stripped sources.
/// </summary>
[TestClass]
public class ReevesLedgerWiringTests
{
    private static readonly string[] MixinFiles = { "TownManagementLedgerMixin.cs", "ReserveStepMixin.cs" };

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<ReevesLedgerModule>().Count(),
            "ReevesLedgerModule must be listed exactly once in Main/Composition/FeatureModules.cs");
    }

    [TestMethod]
    public void TheModule_IsNamedReevesLedger_NotParked_OwnsNoSaveData_DeclaresNothingElse()
    {
        var module = new ReevesLedgerModule();

        Assert.AreEqual("ReevesLedger", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "the ledger saves nothing: it reads the fief and moves gold through the engine's helper");
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.PatchCategories.Count, "no Harmony: the feature is two mixins and two prefab insertions");
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
    }

    [TestMethod]
    public void TheModulesServices_ResolveFromARealContainer()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        new ReevesLedgerModule().RegisterServices(container);

        Assert.IsNotNull(container.Resolve<ReevesLedgerService>());
        Assert.AreSame(container.Resolve<ReevesLedgerService>(), container.Resolve<ReevesLedgerService>());
        Assert.IsInstanceOfType(container.Resolve<IFiefLedgerAdapter>(), typeof(FiefLedgerAdapter));
        Assert.IsNotNull(container.Resolve<ReevesLedgerSettingsProvider>());
    }

    [TestMethod]
    public void IoC_DoesNotRegisterTheFeatureByHand()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true) + RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("ReevesLedger"), "the module registers everything; UIExtenderEx finds the mixins and prefabs by assembly scan");
    }

    [TestMethod]
    public void EveryUiFile_StaysUnder150Lines()
    {
        var files = Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "ReevesLedger", "UI"), "*.cs")
            .Concat(Directory.GetFiles(RepoPaths.RepoPath("Client", "Features", "ReevesLedger", "UI"), "*.cs")).ToArray();

        Assert.IsTrue(files.Length >= 5, "two mixins, the tooltip builder and two prefab patches");
        foreach (var file in files)
            Assert.IsTrue(File.ReadAllLines(file).Length < 150, "ADR-002: " + Path.GetFileName(file) + " is 150 lines or more");
    }

    [TestMethod]
    public void TheMixins_HookTheVanillaMethodsTheBriefNames()
    {
        StringAssert.Contains(Mixin("TownManagementLedgerMixin.cs"), "[ViewModelMixin(\"RefreshTownManagementStats\")]");
        StringAssert.Contains(Mixin("ReserveStepMixin.cs"), "[ViewModelMixin(\"ExecuteConfirm\")]");
    }

    [TestMethod]
    public void EveryEngineCalledMember_IsGuardedAndLogsThroughALatch()
    {
        foreach (var file in MixinFiles)
        {
            var src = Mixin(file);

            Assert.IsTrue(Regex.IsMatch(src, @"catch \(Exception ex\)"), file + " has no guard");
            Assert.IsFalse(Regex.IsMatch(src, @"catch\s*(\{|\(Exception\s*\))"), file + ": a catch without a variable cannot log the cause");
            Assert.IsFalse(Regex.IsMatch(src, @"catch[^{]*\{\s*\}"), file + ": an empty catch");
            StringAssert.Contains(src, "LogOnce(", file + " logs through the latch");
            StringAssert.Contains(src, "_faults!.Run(", file + ": the refresh and click entry points run inside the guard");
            Assert.IsFalse(Regex.IsMatch(src, @"_logger\??\.LogError"), file + " must log through LogOnce only, never straight from a callback");
        }
    }

    [TestMethod]
    public void TheTwoMixins_NameTheirFaultSitesApart()
    {
        // Both fault logs report as "ReevesLedger", and the session summary counts swallowed throws per (feature, site)
        // for the process (#1071): a site name both mixins use would merge their counts and their one WARNING line.
        var sites = MixinFiles.Select(file => Regex.Matches(Mixin(file), @"_faults[!?]?\.(?:Run|Lines|Text|Condition|LogOnce)\(""([^""]+)""")
            .Cast<Match>().Select(m => m.Groups[1].Value).ToArray()).ToArray();

        Assert.IsTrue(sites.All(s => s.Length > 0), "each mixin names at least one literal site");
        CollectionAssert.AreEqual(Array.Empty<string>(), sites[0].Intersect(sites[1]).ToArray(),
            "a site name shared by the two mixins");
    }

    [TestMethod]
    public void ThePatrolRow_IsGatedByTheServiceRule_NotByIsTown()
    {
        // #992: a castle with a patrol, or one the patrol model allows, shows the row too.
        var src = Mixin("TownManagementLedgerMixin.cs");

        StringAssert.Contains(src, "_service!.ShowsPatrolRow(");
        Assert.IsFalse(src.Contains("!_settlement!.IsTown"), "the gate is the service rule, not a town-only check");
    }

    [TestMethod]
    public void TheHoverFunctions_RunInsideTheGuard_SoTheyReturnEmptyInsteadOfThrowing()
    {
        var src = Mixin("TownManagementLedgerMixin.cs");

        Assert.AreEqual(3, Regex.Matches(src, @"_faults!\.Lines\(""").Count, "profit, stash and prison tooltips");
        Assert.AreEqual(1, Regex.Matches(src, @"_faults!\.Text\(""").Count, "the patrol tooltip");
    }

    [TestMethod]
    public void TheMixins_ReadTheSettingsOnlyInTheirConstructor_NeverPerFrame()
    {
        foreach (var file in MixinFiles)
        {
            var src = Mixin(file);

            Assert.IsFalse(src.Contains("TaomSettings"), file + " must go through the provider");
            Assert.AreEqual(1, Regex.Matches(src, @"\.(LedgerEnabled|StepsEnabled)\b").Count,
                file + " reads its switch once, at construction: 'applied the next time the window opens'");
        }
    }

    [TestMethod]
    public void TheMixins_NeverCallTheGoldHelperThemselves_TheEnginesConfirmDoesTheMoving()
    {
        foreach (var file in MixinFiles)
        {
            var src = Mixin(file);

            Assert.IsFalse(src.Contains("GiveGoldAction") || src.Contains("BoostBuildingProcess") || src.Contains("ChangeHeroGold"), file);
        }
    }

    [TestMethod]
    public void TheStepButtons_NeverDriveTheSliderNegative()
    {
        // A negative CurrentGivenAmount makes the discrete slider floor to hundreds and write back: gold from nothing.
        var src = Mixin("ReserveStepMixin.cs");

        StringAssert.Contains(src, "CurrentGivenAmount = 0;");
        Assert.AreEqual(1, Regex.Matches(src, @"CurrentGivenAmount\s*=").Count, "the only write is the zero that makes the confirm a no-move refresh");
        StringAssert.Contains(src, "ExecuteConfirm()");
    }

    [TestMethod]
    public void TheConfirm_RunsInTheLoadBearingOrder_ZeroTheGivenAmount_SetTheTarget_ThenConfirm()
    {
        // The confirm charges a rise and refunds a fall for CurrentReserveAmount + CurrentGivenAmount, so the zero must land first.
        var src = Mixin("ReserveStepMixin.cs");

        Assert.IsTrue(Regex.IsMatch(src, @"vm\.CurrentGivenAmount = 0;\s*vm\.CurrentReserveAmount = step\.Target;\s*vm\.ExecuteConfirm\(\);"),
            "CurrentGivenAmount = 0, then CurrentReserveAmount = step.Target, then ExecuteConfirm()");
    }

    [TestMethod]
    public void TheStepMixin_PlansFromTheReserveViewModelsOwnFigures_AndNamesNoReserveAdapter()
    {
        var src = Mixin("ReserveStepMixin.cs");

        Assert.IsFalse(src.Contains("IReserveAdapter") || src.Contains("_reserve"), "the view model already holds the reserve and the capped purse");
        Assert.AreEqual(3, Regex.Matches(src, @"PlanReserveStep\(vm\.CurrentReserveAmount, vm\.MaxReserveAmount, ").Count,
            "Refresh plans an add and a withdraw, the step plans the pressed one");
    }

    [TestMethod]
    public void TheStepMixin_ReadsMayWriteWorldTwice_OnceToShowTheButtons_OnceBeforeTheClickMovesGold()
    {
        var src = Mixin("ReserveStepMixin.cs");

        Assert.AreEqual(2, Regex.Matches(src, @"\.MayWriteWorld\b").Count);
        Assert.IsTrue(Regex.IsMatch(src, @"var visible = _enabled && _service!\.MayWriteWorld;"), "Refresh hides the buttons");
        Assert.IsTrue(Regex.IsMatch(src, @"vm == null \|\| !_service!\.MayWriteWorld\)\s*return;"), "the click refuses");
    }

    [TestMethod]
    public void TheMixins_BuildTheFaultLogBeforeAnyOtherResolve_SoAConstructorFaultIsLogged()
    {
        foreach (var file in MixinFiles)
        {
            var src = Mixin(file);
            var firstResolve = src.IndexOf("IoC.Resolve<", StringComparison.Ordinal);
            var logLine = src.IndexOf("_faults = new ScreenFaultLog(IoC.Resolve<IModLogger>(), \"ReevesLedger\");", StringComparison.Ordinal);

            Assert.IsTrue(logLine >= 0, file);
            Assert.AreEqual(logLine + "_faults = new ScreenFaultLog(".Length, firstResolve, file + ": the fault log is the first resolve");
        }
    }

    [TestMethod]
    public void TheStepsMismatchLog_UsesItsOwnLatchKey_NotTheExceptionKey()
    {
        var src = Mixin("ReserveStepMixin.cs");

        StringAssert.Contains(src, "LogOnce(site + \" check\"");
    }

    [TestMethod]
    public void TheMixins_ResolveTheSettingsClassItself_NoInterface()
    {
        foreach (var file in MixinFiles)
            Assert.IsFalse(Mixin(file).Contains("IReevesLedgerSettingsProvider"), file);
    }

    [TestMethod]
    public void TheTooltipAndHintViewModels_AreCreatedOnce_AndUpdatedInPlace()
    {
        const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
        var checkedMembers = 0;
        foreach (var type in new[] { typeof(TAOM.Features.ReevesLedger.UI.TownManagementLedgerMixin), typeof(TAOM.Features.ReevesLedger.UI.ReserveStepMixin) })
        {
            foreach (var field in type.GetFields(all).Where(f => IsViewModelType(f.FieldType)))
            {
                Assert.IsTrue(field.IsInitOnly, type.Name + "." + field.Name + ": a tooltip or hint field is readonly, so it is created once");
                checkedMembers++;
            }

            foreach (var prop in type.GetProperties(all).Where(p => IsViewModelType(p.PropertyType)))
            {
                Assert.IsFalse(prop.CanWrite, type.Name + "." + prop.Name + ": a tooltip or hint property is never reassigned");
                checkedMembers++;
            }
        }

        Assert.IsTrue(checkedMembers >= 8, "the reflection found the tooltip and hint members");
    }

    private static bool IsViewModelType(Type t) =>
        t == typeof(TaleWorlds.Core.ViewModelCollection.Information.BasicTooltipViewModel)
        || t == typeof(TaleWorlds.Core.ViewModelCollection.Information.HintViewModel);

    [TestMethod]
    public void TheProfitAndProjectsAndTotalLabels_ReuseVanillasKeys()
    {
        var tips = CoreUi("LedgerTooltips.cs");

        StringAssert.Contains(tips, "{=L1Q4AysI}Profit");
        StringAssert.Contains(tips, "{=LpsoPtOo}Projects");
        StringAssert.Contains(tips, "{=kWVbHPtT}Total");
        StringAssert.Contains(Mixin("TownManagementLedgerMixin.cs"), "GameTexts.FindText(\"str_profit\")");
        foreach (var key in new[] { "taom_reeve_profit", "taom_reeve_projects", "taom_reeve_total" })
            Assert.IsFalse(tips.Contains(key) || Mixin("TownManagementLedgerMixin.cs").Contains(key), key + " is registered nowhere now");
    }

    private static string Mixin(string file) => RepoPaths.ReadSource("Client/Features/ReevesLedger/UI/" + file, stripComments: true);

    // LedgerTooltips stays in core and the fault log is Main/Core/Logging/ScreenFaultLog (both load headless); the mixins and prefabs live in TAOM.Client.
    private static string CoreUi(string file) => RepoPaths.ReadSource("Main/Features/ReevesLedger/UI/" + file, stripComments: true);
}
