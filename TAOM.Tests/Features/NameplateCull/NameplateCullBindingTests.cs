using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Dependencies.Foundation;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.NameplateCull;
using TAOM.Features.NameplateCull.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.NameplateCull;

/// <summary>
/// Pins what Patch104 and its adapter depend on in the installed engine (v1.5.4 at the time of writing): the target, every
/// private member the cull reads, every public member it calls, the shape of the vanilla update it re-implements, and the
/// engine facts the skip rule's correctness rests on (what a plate update does, the constants of the visibility test, and
/// where a plate registers its event listeners). A member the game build lacks fails when a method is compiled, and
/// UpdateRange runs on engine worker threads with no TAOM catch on the stack, so each one is named here and the adapter
/// compiles its three methods at install. The SandBox types are resolved by name:
/// the test project references the TaleWorlds assemblies only.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class NameplateCullBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Ns = "SandBox.ViewModelCollection.Nameplate.";

    private static Type Named(string fullName)
    {
        var type = AccessTools.TypeByName(fullName);
        Assert.IsNotNull(type, fullName + " did not resolve.");
        return type;
    }

    private static Type VmType => Named(Ns + "SettlementNameplatesVM");

    private static Type PlateType => Named(Ns + "SettlementNameplateVM");

    private static MethodInfo Update() => AccessTools.Method(VmType, "Update", Type.EmptyTypes);

    private static List<string> Calls(MethodBase method) =>
        PatchProcessor.GetOriginalInstructions(method)
            .Where(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Newobj) && ci.operand is MethodBase)
            .Select(ci => ((MethodBase)ci.operand).DeclaringType!.Name + "." + ((MethodBase)ci.operand).Name)
            .ToList();

    private static void AssertField(Type owner, string name, Type fieldType)
    {
        var field = owner.GetField(name, AllInstance);
        Assert.IsNotNull(field, $"{owner.Name}.{name} did not resolve.");
        Assert.AreEqual(fieldType, field.FieldType, $"{owner.Name}.{name}");
    }

    private static void AssertProperty(Type owner, string name, Type propertyType)
    {
        var property = owner.GetProperty(name, AllInstance);
        Assert.IsNotNull(property, $"{owner.Name}.{name} did not resolve.");
        Assert.AreEqual(propertyType, property.PropertyType, $"{owner.Name}.{name}");
        Assert.IsNotNull(property.GetGetMethod(), $"{owner.Name}.{name} has no public getter");
    }

    // ---- The target ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Target_SettlementNameplatesVMUpdate_IsAPublicNonVirtualVoidInstanceMethodWithNoParameters()
    {
        RequireGame();

        var target = Update();

        Assert.IsNotNull(target, "SettlementNameplatesVM.Update() did not resolve.");
        Assert.IsTrue(target.IsPublic);
        Assert.IsFalse(target.IsStatic);
        Assert.IsFalse(target.IsVirtual, "a patch on a virtual never reaches an override");
        Assert.AreEqual(typeof(void), target.ReturnType);
        Assert.AreEqual(0, target.GetParameters().Length);
    }

    // ---- The private members the adapter binds ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PrivateFields_TheAdapterBindsByName_ExistWithTheirTypes()
    {
        RequireGame();

        AssertField(VmType, "_mapCamera", typeof(Camera));
        AssertField(PlateType, "_bindIsVisibleOnMap", typeof(bool));
        AssertField(PlateType, "_worldPos", typeof(Vec3));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PublicMembers_TheCullCallsOrReads_ExistWithTheirTypes()
    {
        RequireGame();
        var plate = PlateType;

        // the list the update walks is public, and a List<T>: the adapter indexes it without a cast per plate
        var listType = typeof(MBReadOnlyList<>).MakeGenericType(plate);
        AssertProperty(VmType, "AllNameplates", listType);
        Assert.IsTrue(typeof(List<>).MakeGenericType(plate).IsAssignableFrom(listType));
        AssertProperty(typeof(Camera), "Position", typeof(Vec3));

        AssertProperty(plate, "IsVisibleOnMap", typeof(bool));
        AssertProperty(plate, "IsTracked", typeof(bool));
        AssertProperty(plate, "IsInRange", typeof(bool));
        AssertProperty(plate, "IsTargetedByTutorial", typeof(bool));
        AssertProperty(plate, "Position", typeof(Vec2));
        AssertProperty(plate, "Settlement", typeof(Settlement));

        var update = plate.GetMethod("UpdateNameplateMT", new[] { typeof(Vec3) });
        Assert.IsNotNull(update, "SettlementNameplateVM.UpdateNameplateMT(Vec3) did not resolve.");
        Assert.IsTrue(update.IsPublic);
        Assert.AreEqual(typeof(void), update.ReturnType);
        var refresh = plate.GetMethod("RefreshBindValues", Type.EmptyTypes);
        Assert.IsNotNull(refresh, "SettlementNameplateVM.RefreshBindValues() did not resolve.");
        Assert.IsTrue(refresh.IsPublic);
        Assert.AreEqual(typeof(void), refresh.ReturnType);

        foreach (var name in new[] { "IsTown", "IsFortification", "IsHideout", "IsVisible", "IsInspected" })
            AssertProperty(typeof(Settlement), name, typeof(bool));
        AssertProperty(typeof(Settlement), "Party", typeof(PartyBase));
        AssertProperty(typeof(PartyBase), "IsVisualDirty", typeof(bool));
        AssertProperty(typeof(Campaign), "VisualTrackerManager", typeof(VisualTrackerManager));
        var check = typeof(VisualTrackerManager).GetMethod("CheckTracked", new[] { typeof(ITrackableBase) });
        Assert.IsNotNull(check, "VisualTrackerManager.CheckTracked(ITrackableBase) did not resolve.");
        Assert.AreEqual(typeof(bool), check.ReturnType);
        Assert.IsTrue(typeof(ITrackableBase).IsAssignableFrom(typeof(Settlement)), "the adapter passes a Settlement to CheckTracked");

        Assert.AreEqual(typeof(float), typeof(Vec3).GetField("z")?.FieldType);
        var distance = typeof(Vec3).GetMethod("Distance", new[] { typeof(Vec3) });
        Assert.IsNotNull(distance, "Vec3.Distance(Vec3) did not resolve.");
        Assert.AreEqual(typeof(float), distance.ReturnType);
        Assert.AreEqual(typeof(float), typeof(Vec2).GetField("x")?.FieldType);
        Assert.AreEqual(typeof(float), typeof(Vec2).GetField("y")?.FieldType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TWParallelFor_TakesTheVanillaDefaultGrainSize_AndTheDelegateShapeTheAdapterBuilds()
    {
        RequireGame();

        var f = typeof(TWParallel).GetMethod("For", new[] { typeof(int), typeof(int), typeof(TWParallel.ParallelForAuxPredicate), typeof(int) });

        Assert.IsNotNull(f, "TWParallel.For(int, int, ParallelForAuxPredicate, int) did not resolve.");
        Assert.IsTrue(f.IsStatic && f.IsPublic);
        var grain = f.GetParameters()[3];
        Assert.IsTrue(grain.HasDefaultValue);
        Assert.AreEqual(16, grain.DefaultValue, "vanilla calls For(0, n, predicate) and so gets this grain; the cull passes the same");
        var invoke = typeof(TWParallel.ParallelForAuxPredicate).GetMethod("Invoke")!;
        Assert.AreEqual(typeof(void), invoke.ReturnType);
        CollectionAssert.AreEqual(new[] { typeof(int), typeof(int) }, invoke.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    // ---- The vanilla update the adapter re-implements ----

    private static IEnumerable<MethodBase> AllBodies(Type type) =>
        type.GetMembers(AllInstance | BindingFlags.Static | BindingFlags.DeclaredOnly).OfType<MethodBase>()
            .Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).SelectMany(AllBodies))
            .Where(m => m.GetMethodBody() != null);

    private static List<string> BodiesTouching(Type type, string fieldName, params OpCode[] opcodes) =>
        AllBodies(type)
            .Where(m => PatchProcessor.GetOriginalInstructions(m)
                .Any(ci => opcodes.Contains(ci.opcode) && ci.operand is FieldInfo f && f.Name == fieldName))
            .Select(m => m.Name)
            .Distinct()
            .ToList();

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void CachedCameraPosition_IsReadOnlyInUpdateNameplateAuxMT_WhichOnlyUpdatesParallelLoopReaches()
    {
        RequireGame();

        // The adapter does not store _cachedCameraPosition: it hands the camera to the loop itself. That is safe only while
        // nothing else reads the field, and the only reader is reached only from vanilla Update() (which stores it first).
        CollectionAssert.AreEqual(new[] { "UpdateNameplateAuxMT" },
            BodiesTouching(VmType, "_cachedCameraPosition", OpCodes.Ldfld, OpCodes.Ldflda), "readers of _cachedCameraPosition");
        CollectionAssert.AreEqual(new[] { "Update" },
            BodiesTouching(VmType, "_cachedCameraPosition", OpCodes.Stfld, OpCodes.Stobj), "writers of _cachedCameraPosition");

        // The predicate that wraps UpdateNameplateAuxMT: built once in the constructor, handed to TWParallel.For in Update only,
        // and the method itself is never called directly.
        CollectionAssert.AreEqual(new[] { "Update" },
            BodiesTouching(VmType, "UpdateNameplateAuxMTPredicate", OpCodes.Ldfld, OpCodes.Ldflda), "users of the predicate");
        CollectionAssert.AreEqual(new[] { ".ctor" },
            AllBodies(VmType).Where(m => PatchProcessor.GetOriginalInstructions(m)
                    .Any(ci => ci.opcode == OpCodes.Ldftn && ci.operand is MethodBase t && t.Name == "UpdateNameplateAuxMT"))
                .Select(m => m.Name).Distinct().ToList(), "where the predicate is built");
        Assert.AreEqual(0, AllBodies(VmType).Count(m => PatchProcessor.GetOriginalInstructions(m)
                .Any(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && ci.operand is MethodBase t && t.Name == "UpdateNameplateAuxMT")),
            "UpdateNameplateAuxMT is never called directly");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void VanillaUpdate_CallsExactlyTheCameraThePlateListTheParallelLoopAndRefreshBindValues()
    {
        RequireGame();

        var calls = Calls(Update());

        // Update() = read the camera, store it, TWParallel.For over every plate, then RefreshBindValues over every plate.
        // A new call here means the engine added a step the cull does not copy.
        CollectionAssert.AreEquivalent(
            new[] { "Camera.get_Position", "List`1.get_Count", "TWParallel.For", "List`1.get_Item", "SettlementNameplateVM.RefreshBindValues" },
            calls.Distinct().ToArray(), "calls: " + string.Join(", ", calls));
        Assert.AreEqual(1, calls.Count(c => c == "TWParallel.For"));
        Assert.AreEqual(1, calls.Count(c => c == "SettlementNameplateVM.RefreshBindValues"));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void VanillaPlateUpdate_IsPositionWindowVisibilityPushPositionAndDynamicProperties_AndNothingElse()
    {
        RequireGame();

        var calls = Calls(PlateType.GetMethod("UpdateNameplateMT", new[] { typeof(Vec3) })!);

        // What a skipped plate does not do. Everything it touches is either hidden-plate bookkeeping or read again when
        // the plate next updates; a new call here has to be read before the skip is trusted.
        CollectionAssert.AreEqual(
            new[]
            {
                "SettlementNameplateVM.CalculatePosition", "SettlementNameplateVM.DetermineIsInsideWindow",
                "SettlementNameplateVM.DetermineIsVisibleOnMap", "NameplateVM.RefreshPosition",
                "NameplateVM.RefreshDynamicProperties",
            }, calls, "calls: " + string.Join(", ", calls));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void VanillaVisibilityTest_HoldsTheThreeConstantsTheRuleCopies()
    {
        RequireGame();
        var isVisible = PlateType.GetMethod("IsVisible", AllInstance);
        Assert.IsNotNull(isVisible, "SettlementNameplateVM.IsVisible did not resolve.");

        var constants = PatchProcessor.GetOriginalInstructions(isVisible)
            .Where(ci => ci.opcode == OpCodes.Ldc_R4).Select(ci => (float)ci.operand).ToList();

        Assert.IsTrue(constants.Contains(NameplateCullRule.TownCameraHeight), "400");
        Assert.IsTrue(constants.Contains(NameplateCullRule.FortificationCameraHeight), "200");
        Assert.IsTrue(constants.Contains(NameplateCullRule.NearMargin), "100");
        Assert.AreEqual(400f, NameplateCullRule.TownCameraHeight);
        Assert.AreEqual(200f, NameplateCullRule.FortificationCameraHeight);
        Assert.AreEqual(100f, NameplateCullRule.NearMargin);
        var calls = Calls(isVisible);
        CollectionAssert.IsSubsetOf(new[] { "Settlement.get_IsTown", "Settlement.get_IsFortification" }, calls.Distinct().ToArray());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void VanillaRefreshPosition_ParksAHiddenPlateAtTheCoordinateTheSkipTestReads()
    {
        RequireGame();

        // The skip condition "parked off screen" compares the pushed Position with this point, so a changed park point would
        // leave every plate unskippable (or, worse, match a visible one) without any other test noticing.
        var constants = PatchProcessor.GetOriginalInstructions(PlateType.GetMethod("RefreshPosition", AllInstance | BindingFlags.DeclaredOnly)!)
            .Where(ci => ci.opcode == OpCodes.Ldc_R4).Select(ci => (float)ci.operand).ToList();

        CollectionAssert.AreEqual(new[] { NameplateCullAdapter.ParkedCoordinate, NameplateCullAdapter.ParkedCoordinate }, constants,
            "the two operands of new Vec2(x, y) in the parked branch");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void VanillaDynamicProperties_CallExactlyTheSettlementFactsTheRuleMirrors()
    {
        RequireGame();

        var calls = Calls(PlateType.GetMethod("RefreshDynamicProperties", new[] { typeof(bool) })!);

        // A skipped plate runs no UpdateNameplateMT, so it skips this per-frame call (forceUpdate false) with it. The other
        // callers pass forceUpdate true (plate construction, owner changes, the view's forced refresh) and still run. A new
        // call here is a step the skip rule has to account for.
        CollectionAssert.AreEqual(
            new[]
            {
            "NameplateVM.RefreshDynamicProperties", "SettlementNameplateVM.get_Settlement", "Settlement.get_MapFaction",
            "SettlementNameplateVM.get_Settlement", "Settlement.get_MapFaction", "IFaction.get_Color", "Color.UIntToColorString",
            "String.Concat", "SettlementNameplateVM.get_Settlement", "Settlement.get_Banner", "Banner.GetVersionNo",
            "BannerExtensions.IsContentsSameWith", "BannerImageIdentifierVM..ctor", "BannerImageIdentifierVM..ctor",
            "SettlementNameplateVM.get_Settlement", "Settlement.get_MapFaction", "SettlementNameplateVM.get_Settlement",
            "Settlement.get_Party", "PartyBase.get_IsVisualDirty", "SettlementNameplateVM.get_Settlement", "Settlement.get_Party",
            "PartyBase.get_Name", "Object.ToString", "Campaign.get_Current", "Campaign.get_VisualTrackerManager",
            "SettlementNameplateVM.get_Settlement", "VisualTrackerManager.CheckTracked", "SettlementNameplateVM.get_Settlement",
            "Settlement.get_IsHideout", "SettlementNameplateVM.get_Settlement", "Settlement.get_IsVisible", "SettlementNameplateVM.get_Settlement",
            "Settlement.get_IsInspected", "SettlementNameplateVM.get_Settlement", "Settlement.get_HasPort", "SettlementNameplateVM.get_Settlement",
            "List`1.get_Item", "Building.get_BuildingType", "MBObjectBase.get_StringId", "String.op_Equality", "List`1.get_Item",
            "Building.get_CurrentLevel", "List`1.get_Count", "SettlementNameplateVM.get_Settlement", "Settlement.get_FerryTarget",
            }, calls, "calls: " + string.Join(", ", calls));
    }

    // ---- Event registration follows IsInRange, so a skipped plate skips no event tick ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void RefreshBindValues_PushesTheBoundValuesAndTicksNotificationsAndEventsOnlyWhenTheirEventsAreRegistered()
    {
        RequireGame();

        var calls = Calls(PlateType.GetMethod("RefreshBindValues", Type.EmptyTypes)!);

        // A skipped plate runs no RefreshBindValues either. The whole ordered list is pinned, so a new unconditional step
        // fails here instead of being skipped silently.
        CollectionAssert.AreEqual(
            new[]
            {
            "NameplateVM.set_FactionColor", "SettlementNameplateVM.set_Banner", "SettlementNameplateVM.set_Relation",
            "SettlementNameplateVM.set_WPos", "SettlementNameplateVM.set_WSign", "SettlementNameplateVM.set_IsInside",
            "NameplateVM.set_Position", "NameplateVM.set_IsVisibleOnMap", "SettlementNameplateVM.set_IsInRange",
            "SettlementNameplateVM.set_HasPort", "SettlementNameplateVM.set_PortLevel", "SettlementNameplateVM.set_HasFerry",
            "SettlementNameplateVM.set_IsTracked", "NameplateVM.set_IsTargetedByTutorial", "NameplateVM.set_DistanceToCamera",
            "SettlementNameplateVM.set_Name", "SettlementNameplateVM.get_SettlementNotifications", "SettlementNameplateNotificationsVM.get_IsEventsRegistered",
            "SettlementNameplateVM.get_SettlementNotifications", "SettlementNameplateNotificationsVM.Tick", "SettlementNameplateVM.get_SettlementEvents",
            "SettlementNameplateEventsVM.get_IsEventsRegistered", "SettlementNameplateVM.get_SettlementEvents", "SettlementNameplateEventsVM.Tick",
            }, calls, "calls: " + string.Join(", ", calls));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void IsInRangeSetter_IsTheOnlyPlaceAPlateRegistersItsEvents_InTheNameplateAssembly()
    {
        RequireGame();
        var setter = PlateType.GetProperty("IsInRange")!.GetSetMethod()!;
        var setterCalls = Calls(setter);
        Assert.AreEqual(1, setterCalls.Count(c => c == "SettlementNameplateNotificationsVM.RegisterEvents"));
        Assert.AreEqual(1, setterCalls.Count(c => c == "SettlementNameplateEventsVM.RegisterEvents"));
        Assert.AreEqual(1, setterCalls.Count(c => c == "SettlementNameplatePartyMarkersVM.RegisterEvents"));

        var plateTypes = new HashSet<string>
        {
            "SettlementNameplateNotificationsVM", "SettlementNameplateEventsVM", "SettlementNameplatePartyMarkersVM",
        };
        var callers = IlCallScanner.FindCallers(PlateType.Assembly,
            m => m.Name == "RegisterEvents" && m.DeclaringType != null && plateTypes.Contains(m.DeclaringType.Name),
            out var unreadable, out var scanned);

        Assert.IsTrue(scanned > 1000, "the scan walked " + scanned + " bodies");
        CollectionAssert.AreEquivalent(new[] { "SandBox.ViewModelCollection.Nameplate.SettlementNameplateVM.set_IsInRange" }, callers.Distinct().ToArray(),
            "unreadable bodies: " + unreadable.Count);
    }

    // ---- The adapter, bound on the installed engine ----

    private const string HarmonyId = "taom.tests.nameplatecull.adapter";

    // Runs `check` with the real Patch104 class applied to the real Update, then takes the patch off again.
    private static void WithPatch104Applied(Action check)
    {
        var harmony = new Harmony(HarmonyId);
        try
        {
            harmony.CreateClassProcessor(PatchType).Patch();
            check();
        }
        finally
        {
            harmony.UnpatchAll(HarmonyId);
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void Adapter_InitializeOnTheInstalledEngine_WithPatch104Attached_BindsEveryMember()
    {
        RequireGame();

        WithPatch104Applied(() =>
        {
            var reason = new NameplateCullAdapter().Initialize();

            Assert.IsNull(reason, reason);
        });
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void Adapter_InitializeOnTheInstalledEngine_WithoutPatch104_SaysItIsNotAttached()
    {
        RequireGame();

        var reason = new NameplateCullAdapter().Initialize();

        Assert.AreEqual("Patch104 is not attached to SettlementNameplatesVM.Update", reason);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void Adapter_InitializeOnTheInstalledEngine_AForeignPrefixIsNotPatch104()
    {
        RequireGame();
        var harmony = new Harmony(HarmonyId);
        try
        {
            harmony.Patch(Update(), prefix: new HarmonyMethod(typeof(NameplateCullBindingTests), nameof(ForeignPrefix)));

            var reason = new NameplateCullAdapter().Initialize();

            Assert.AreEqual("Patch104 is not attached to SettlementNameplatesVM.Update", reason);
        }
        finally
        {
            harmony.UnpatchAll(HarmonyId);
        }
    }

    private static bool ForeignPrefix() => true;

    // ---- The patch class ----

    private static readonly Type PatchType = typeof(SettlementNameplatesVM_Update_NameplateCull_Patch);

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchClass_TargetsUpdate_InTheLiteralCategory()
    {
        RequireGame();

        var info = HarmonyMethod.Merge(PatchType.GetCustomAttributes<HarmonyPatch>().Select(a => a.info).ToList());
        var categories = PatchType.GetCustomAttributes<HarmonyPatchCategory>().Select(c => c.info.category).ToList();

        Assert.AreEqual(VmType, info.declaringType);
        Assert.AreEqual("Update", info.methodName);
        CollectionAssert.AreEqual(new[] { "Patch104_NameplateCull" }, categories);
        Assert.AreEqual("Patch104_NameplateCull", NameplateCullModule.PatchCategory);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchMethod_IsOneBoolPrefix_WithTheParameterNameHarmonyBindsBy()
    {
        RequireGame();
        var prefixes = PatchType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.GetCustomAttribute<HarmonyPrefix>() != null).ToList();

        Assert.AreEqual(1, prefixes.Count);
        Assert.AreEqual("Prefix", prefixes[0].Name);
        Assert.AreEqual(typeof(bool), prefixes[0].ReturnType, "true runs vanilla, false skips it");
        CollectionAssert.AreEqual(new[] { "__instance" }, prefixes[0].GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual(VmType, prefixes[0].GetParameters()[0].ParameterType);
        Assert.IsNull(PatchType.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<HarmonyFinalizer>() != null), "no finalizer: the prefix catches its own errors");
    }

    // ---- PatchShield ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchShield_StillWrapsTheUpdate_ByNameAndByNamespace()
    {
        RequireGame();

        // Once per map frame, main thread: stays under the shield (decision D13).
        var type = VmType;
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod(type.FullName, "Update"));
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetNamespace(type.Namespace));
    }

    // ---- The MCM toggle ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Setting_CullHiddenNameplates_IsAMapPerformanceBoolOnByDefaultThatNeedsNoRestart()
    {
        var property = typeof(BattleLoadDiagnosticsSettings).GetProperty("CullHiddenNameplates");

        Assert.IsNotNull(property, "BattleLoadDiagnosticsSettings.CullHiddenNameplates is missing.");
        Assert.AreEqual(typeof(bool), property.PropertyType);
        var attribute = property.GetCustomAttribute<SettingPropertyBoolAttribute>();
        Assert.IsNotNull(attribute);
        Assert.AreEqual("Cull Hidden Settlement Nameplates", attribute.DisplayName);
        Assert.IsFalse(attribute.RequireRestart, "read on every frame, so a live A/B works");
        Assert.IsFalse(string.IsNullOrWhiteSpace(attribute.HintText));
        Assert.AreEqual("Map Performance", property.GetCustomAttribute<SettingPropertyGroupAttribute>()!.GroupName);
        Assert.AreEqual(true, new BattleLoadDiagnosticsSettings().CullHiddenNameplates);
    }
}
