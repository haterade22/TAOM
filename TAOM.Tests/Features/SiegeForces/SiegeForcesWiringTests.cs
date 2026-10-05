using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DryIoc;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.SiegeForces;
using TAOM.Features.SiegeForces.Hooks;
using TAOM.Features.SiegeForces.Models;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SiegeForces;

/// <summary>
/// Wiring regression guard for the siege troop picker (#734). Every piece fails silently when unwired: drop the module
/// line and no picker ever opens; drop the category and the prefixes never apply; drop the model and the left-out troops
/// still spawn; give the totals prefix an engine read before its pending check and every Custom Battle throws in
/// <c>AfterStart</c>. Methods that touch engine types carry <c>RequiresGame</c> (hosted CI builds against reference
/// assemblies whose bodies throw); the rest run there.
/// </summary>
[TestClass]
public class SiegeForcesWiringTests
{
    private static readonly string[] ConditionalBranches =
    {
        "beq", "bge", "bgt", "ble", "blt", "bne.un", "bge.un", "bgt.un", "ble.un", "blt.un", "brtrue", "brfalse",
    };

    private static bool _gameLoaded;

    // GetCustomAttributes over every TAOM type reaches attributes that name SandBox.View types, which only
    // GameAssemblies loads into the test AppDomain (the CreatureBandits wiring tests do the same).
    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static IEnumerable<Type> PatchClasses() =>
        AccessTools.GetTypesFromAssembly(typeof(SiegeForcesModule).Assembly)
            .Where(t => t.GetCustomAttributes<HarmonyPatchCategory>().Any(c => c.info.category == "Patch102_SiegeForces"));

    // --- the module -------------------------------------------------------------------------------------------

    [TestMethod]
    public void FeatureModules_ListTheSiegeForcesModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<SiegeForcesModule>().Count());
    }

    [TestMethod]
    public void TheModule_IsNamedSiegeForces_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new SiegeForcesModule();

        Assert.AreEqual("SiegeForces", module.Id);
        Assert.IsNull(module.ParkedReason, "the picker ships on");
        Assert.IsFalse(module.OwnsSaveData, "nothing is persisted: the pending fit names its battle by reference");
    }

    [TestMethod]
    public void TheModule_AppliesPatch102AtGameInit_AndOnlyThat()
    {
        // PlayerSiege and the spawn logic are campaign and core types, like the other campaign-menu patches.
        var decls = new SiegeForcesModule().PatchCategories.ToList();

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch102_SiegeForces", decls[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, decls[0].Phase);
    }

    [TestMethod]
    public void TheModule_AddsNoBehaviorOfAnyKind()
    {
        // No mission behavior, no campaign behavior: nothing here needs a lifecycle, and nothing is saved.
        var module = new SiegeForcesModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Patch102_HasExactlyTheTwoPatchesInItsCategory()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        CollectionAssert.AreEquivalent(
            new[] { nameof(Patch102_StartSiegeMissionPicker), nameof(Patch102_SpawnTotalsFit) },
            PatchClasses().Select(t => t.Name).ToList());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Patch102_TheFitClassIsAppliedBeforeTheEntryClass_InTheOrderPatchCategoryIndexApplies()
    {
        // PatchCategoryIndex.Apply patches a category's classes in the order Build indexed them (assembly order) and a
        // class that fails stops the rest. The fit must come first, so a failed fit leaves no picker without its fit.
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var index = PatchCategoryIndex.Build(typeof(SiegeForcesModule).Assembly);
        var classesByCategory = (Dictionary<string, List<Type>>)typeof(PatchCategoryIndex)
            .GetField("_classesByCategory", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(index)!;
        var order = classesByCategory["Patch102_SiegeForces"];

        CollectionAssert.AreEqual(
            new[] { typeof(Patch102_SpawnTotalsFit), typeof(Patch102_StartSiegeMissionPicker) }, order,
            "declare Patch102_SpawnTotalsFit above Patch102_StartSiegeMissionPicker in Patch102_SiegeForces.cs");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModule_DeclaresOneCampaignModel_ExtendingTheDefaultTroopSupplierModel()
    {
        var decl = new SiegeForcesModule().GameModels.Single();

        Assert.AreEqual(ModelTarget.Campaign, decl.Target, "a wall battle's picker exists only in a campaign");
        Assert.AreEqual(typeof(TroopSupplierProbabilityModel), decl.SlotType);
        Assert.AreEqual(typeof(TaomTroopSupplierProbabilityModel), decl.ModelType);
        Assert.AreEqual(typeof(DefaultTroopSupplierProbabilityModel), decl.ModelType.BaseType,
            "extend the model SandBox installs, so every other rule stays the engine's");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModule_RegistersTheServiceAsOneInstance_WithItsAdapterAndProviders()
    {
        using var container = new Container();
        RegisterExternals(container);

        new SiegeForcesModule().RegisterServices(container);

        var service = container.Resolve<SiegeForcesService>();
        Assert.AreSame(service, container.Resolve<SiegeForcesService>());
        Assert.IsInstanceOfType(container.Resolve<ISiegeForcesAdapter>(), typeof(SiegeForcesAdapter));
        Assert.IsInstanceOfType(container.Resolve<ISiegeForcesConfigProvider>(), typeof(SiegeForcesConfigProvider));
        Assert.IsInstanceOfType(container.Resolve<ISiegeForcesSettingsProvider>(), typeof(SiegeForcesSettingsProvider));
        Assert.AreSame(container.Resolve<ISiegeForcesAdapter>(), container.Resolve<ISiegeForcesAdapter>());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModel_ReachesTheSameServiceInstance_ThePatchesResolve()
    {
        using var container = new Container();
        RegisterExternals(container);
        var module = new SiegeForcesModule();
        module.RegisterServices(container);

        var model = (TaomTroopSupplierProbabilityModel)module.GameModels.Single().Create(container);

        var service = typeof(TaomTroopSupplierProbabilityModel).GetField("_service", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(model);
        Assert.AreSame(container.Resolve<SiegeForcesService>(), service,
            "the model and the prefixes must share one service, or the model filters for a plan the prefix never fits");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void AtCampaignStart_TheModelIsAddedToACampaignStarter_AndNothingIsAddedToACustomBattleStarter()
    {
        using var container = new Container();
        RegisterExternals(container);
        var logger = Substitute.For<IModLogger>();
        var module = new SiegeForcesModule();
        module.RegisterServices(container);
        var runner = new ModuleRunner(new TaomFeatureModule[] { module }, () => logger);

        var campaign = new CampaignGameStarter(null, null);
        FeatureModuleHooks.AddGameStartContent(runner, container, campaign);
        Assert.AreEqual(1, campaign.Models.OfType<TaomTroopSupplierProbabilityModel>().Count());

        var basic = new BasicGameStarter();
        FeatureModuleHooks.AddGameStartContent(runner, container, basic);
        Assert.AreEqual(0, ((TaleWorlds.Core.IGameStarter)basic).Models.OfType<TaomTroopSupplierProbabilityModel>().Count(),
            "Custom Battle builds its own troop supplier and has no wall-battle picker");
    }

    private static void RegisterExternals(IContainer container)
    {
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance<IRaceManager>(FakeRaceManager.WithTrolls());
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
    }

    // --- the model's call order -------------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModel_NotesTheListLengthThenCallsTheBaseModelThenHandsTheServiceTheWindow()
    {
        // The window starts at the length BEFORE the base call and the filter must run AFTER it: reversed, the window is
        // empty or the base appends over what was removed. The override is straight-line (no conditional branch), so
        // the order of calls in its IL is the order they run in; this pins that order and the absence of a branch.
        // It proves presence and order, not the branch relationship of a body that has branches (lessons/harmony-il.md).
        var method = typeof(TaomTroopSupplierProbabilityModel).GetMethod(
            "EnqueueTroopSpawnProbabilitiesAccordingToUnitSpawnPrioritization",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        var il = method.GetMethodBody()!.GetILAsByteArray()!;

        var calls = IlCallScanner.ExtractCalledMethods(method, il).ToList();
        var listLength = calls.FindIndex(c => c.Name == "get_Count"
            && c.DeclaringType == typeof(List<(FlattenedTroopRosterElement, MapEventParty, float)>));
        var baseCall = calls.FindIndex(c => c.DeclaringType == typeof(DefaultTroopSupplierProbabilityModel)
            && c.Name == "EnqueueTroopSpawnProbabilitiesAccordingToUnitSpawnPrioritization");
        var filter = calls.FindIndex(c => c.DeclaringType == typeof(SiegeForcesService) && c.Name == "FilterAppended");

        Assert.IsTrue(listLength >= 0, "the list length is no longer read");
        Assert.IsTrue(baseCall > listLength, "the length must be read before the base model appends");
        Assert.IsTrue(filter > baseCall, "the filter must run after the base model has appended");
        Assert.AreEqual(1, calls.Count(c => c.DeclaringType == typeof(DefaultTroopSupplierProbabilityModel)), "exactly one base call");

        var branches = IlCallScanner.Fingerprint(method, il).Where(t => ConditionalBranches.Contains(t)).ToList();
        Assert.AreEqual(0, branches.Count, "the model body must stay straight-line (gamemodels.md rule 4): " + string.Join(", ", branches));
    }

    [TestMethod]
    public void TheModel_PassesIncludePlayerStraightToTheService()
    {
        // A simulation (includePlayer false) must reach the service as false so it is never filtered. The model may not
        // decide that itself: it has no branch to do it with, and the service tests pin the false case.
        var source = RepoPaths.ReadSource("Main/Features/SiegeForces/Models/TaomTroopSupplierProbabilityModel.cs", stripComments: true);

        StringAssert.Contains(source, "_service.FilterAppended(includePlayer, () => new ReadyListWindow(battleParty, priorityList, from));");
    }

    // --- the prefixes' guard order ----------------------------------------------------------------------------

    private static bool IsEngineAssembly(Type? type)
    {
        var name = type?.Assembly.GetName().Name ?? "";
        return !(name == "TAOM" || name == "mscorlib" || name.StartsWith("System", StringComparison.Ordinal));
    }

    private static IReadOnlyList<MethodBase> CalleesOf(MethodBase method) =>
        IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()!).ToList();

    [DataTestMethod]
    [TestCategory("RequiresGame")]
    [DataRow(typeof(Patch102_StartSiegeMissionPicker))]
    [DataRow(typeof(Patch102_SpawnTotalsFit))]
    public void ThePrefixBody_NamesNoEngineMember_OnlyTaomHelpersAndTheBaseLibrary(Type patch)
    {
        // R2: a member that stops resolving fails when the body is compiled, before its try runs, and PatchShield
        // then swallows it at the target and skips the original. The engine work lives in NoInlining helpers.
        var prefix = patch.GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static)!;
        var callees = CalleesOf(prefix);

        Assert.IsTrue(callees.Count >= 2, "the scan found no calls: it cannot vouch for the body");
        foreach (var callee in callees)
            Assert.IsFalse(IsEngineAssembly(callee.DeclaringType), $"{patch.Name}.Prefix calls {callee.DeclaringType?.Name}.{callee.Name}");
        foreach (var local in prefix.GetMethodBody()!.LocalVariables)
            Assert.IsFalse(IsEngineAssembly(local.LocalType), $"{patch.Name}.Prefix declares a local of {local.LocalType.Name}");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheScanThatBacksThePrefixCheck_RejectsABodyThatDoesNameAnEngineMember()
    {
        // The control: a body that reads an engine static must fail the same predicate, or the check above is vacuous.
        var offender = typeof(SiegeForcesWiringTests).GetMethod(nameof(ABodyThatReadsAnEngineStatic), BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.IsTrue(CalleesOf(offender).Any(c => IsEngineAssembly(c.DeclaringType)));
    }

    private static bool ABodyThatReadsAnEngineStatic() => Campaign.Current == null;

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheTotalsPrefix_AsksTheServiceWhetherARecordIsPendingBeforeAnyEngineRead()
    {
        // R1(a): the prefix runs for every battle, Custom Battle included, where MapEvent.PlayerMapEvent throws. The
        // first thing it calls is the pure pending read, and that helper names no engine member.
        var prefix = typeof(Patch102_SpawnTotalsFit).GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static)!;
        var callees = CalleesOf(prefix).Where(c => c.DeclaringType == typeof(Patch102_SpawnTotalsFit)).Select(c => c.Name).ToList();

        Assert.AreEqual("HasPending", callees.FirstOrDefault(), "the pending check must come first");
        Assert.IsTrue(callees.IndexOf("Fit") > callees.IndexOf("HasPending"), "the engine-touching Fit must come after it");

        var hasPending = typeof(Patch102_SpawnTotalsFit).GetMethod("HasPending", BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (var callee in CalleesOf(hasPending))
            Assert.IsFalse(IsEngineAssembly(callee.DeclaringType), $"HasPending calls {callee.DeclaringType?.Name}.{callee.Name}");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheEngineTouchingHelpers_AreNotInlinedIntoThePrefixes()
    {
        // NoInlining is what keeps their engine references out of the prefix's own compilation.
        foreach (var (type, name) in new[]
                 {
                     (typeof(Patch102_StartSiegeMissionPicker), "OfferPicker"), (typeof(Patch102_StartSiegeMissionPicker), "Resume"),
                     (typeof(Patch102_SpawnTotalsFit), "HasPending"), (typeof(Patch102_SpawnTotalsFit), "Fit"),
                 })
        {
            var helper = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(helper, $"{type.Name}.{name}");
            Assert.IsTrue(helper!.MethodImplementationFlags.HasFlag(MethodImplAttributes.NoInlining), $"{type.Name}.{name} lost NoInlining");
        }
    }

    // --- the setting ------------------------------------------------------------------------------------------

    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    [TestMethod]
    public void TheSetting_IsOnByDefault_AppliesWithoutARestart_AndSitsInItsOwnBattleTacticsGroup()
    {
        // MCM reads these attributes by name; MCMv5 is runtime-only here, so the test does too.
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableSiegeTroopPicker))!;
        Assert.IsTrue(new TaomSettings().EnableSiegeTroopPicker);

        var group = Attribute(property, "SettingPropertyGroupAttribute");
        Assert.AreEqual("Battle Tactics/Siege Forces", group.GetType().GetProperty("GroupName")!.GetValue(group));
        Assert.AreEqual(25, (int)group.GetType().GetProperty("GroupOrder")!.GetValue(group)!,
            "MCM creates a group from its first property and ignores a GroupOrder on any later one");

        var value = Attribute(property, "SettingPropertyBoolAttribute");
        Assert.AreEqual(false, value.GetType().GetProperty("RequireRestart")!.GetValue(value),
            "the toggle gates only the offer and is read at each assault");
        StringAssert.Contains((string)value.GetType().GetProperty("HintText")!.GetValue(value)!, "next assault");
        StringAssert.Contains((string)value.GetType().GetProperty("HintText")!.GetValue(value)!, "reserves included");
    }

    [TestMethod]
    public void TheGroupOrder_IsFreeAmongTheOtherBattleTacticsGroups()
    {
        var orders = typeof(TaomSettings).GetProperties()
            .Where(p => p.Name != nameof(TaomSettings.EnableSiegeTroopPicker))
            .Select(p => p.GetCustomAttributes(inherit: false).FirstOrDefault(a => a.GetType().Name == "SettingPropertyGroupAttribute"))
            .Where(a => a != null)
            .Select(a => (Name: (string)a!.GetType().GetProperty("GroupName")!.GetValue(a)!, Order: (int)a.GetType().GetProperty("GroupOrder")!.GetValue(a)!))
            .Where(g => g.Name.StartsWith("Battle Tactics/", StringComparison.Ordinal) && g.Order != 0)
            .ToList();

        Assert.IsTrue(orders.Count >= 10, "expected the other Battle Tactics groups, found " + orders.Count);
        CollectionAssert.DoesNotContain(orders.Select(g => g.Order).ToList(), 25,
            "another Battle Tactics group already uses GroupOrder 25: " + string.Join(", ", orders.Where(g => g.Order == 25).Select(g => g.Name)));
    }
}
