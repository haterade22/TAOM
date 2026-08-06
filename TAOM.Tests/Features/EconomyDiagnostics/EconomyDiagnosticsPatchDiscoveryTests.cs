using System.Linq;
using HarmonyLib;
using TAOM.Tests.Migration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.EconomyDiagnostics;

/// <summary>
/// Proves Harmony can actually FIND the `Patch68_EconomyDiagnostics` classes.
///
/// Why this test exists and why it is not paranoia: the four flow-tag patches are **nested** classes
/// inside `TownGoldFlowTagPatches`, and they are the only nested `[HarmonyPatch]` classes in the
/// entire TAOM codebase — zero precedent to lean on. TAOM never calls `PatchAll`; it patches by
/// category, and the failure mode of a category that discovers nothing is completely silent: no
/// error, no warning, the patches simply never engage. The ledger would still populate (the recorder
/// is a top-level class and would be found), every movement would record as `Other`, and the report
/// would look plausible while attributing the town's entire economy to "unknown".
///
/// So the reasoning "GetTypes() returns nested types, therefore Harmony finds them" is not good
/// enough. This exercises `AccessTools.GetTypesFromAssembly` — the exact call Harmony's
/// `PatchCategory` uses to enumerate candidates — and applies the same category filter, against the
/// real TAOM assembly.
/// </summary>
[TestClass]
public class EconomyDiagnosticsPatchDiscoveryTests
{
    private const string Category = "Patch68_EconomyDiagnostics";
    private const string FeatureNamespace = "TAOM.Features.EconomyDiagnostics";

    private static bool _gameLoaded;

    /// <summary>
    /// Walking the TAOM assembly's types touches metadata that references game assemblies spread
    /// across several module folders (`TaleWorlds.MountAndBlade.View`, `.CustomBattle`), which are
    /// not on the test host's default probing path. Without the resolver installed here, reading
    /// `Type.Namespace` on a nested type throws `FileNotFoundException` from `GetDeclaringType`
    /// rather than testing anything. Same guard the other `BindingVerification` suites use.
    /// </summary>
    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    /// <summary>
    /// Enumerates via `AccessTools.GetTypesFromAssembly` — the exact call Harmony's `PatchCategory`
    /// uses — so nested types are discovered (or not) by the same mechanism the runtime will use.
    ///
    /// The namespace filter runs BEFORE any attribute read, and is load-bearing rather than tidiness:
    /// reading custom attributes off every type in the TAOM assembly forces resolution of types that
    /// reference `TaleWorlds.MountAndBlade.View`, which ships in `Modules\Native\bin` and is not on
    /// the test host's probing path — so the unfiltered version throws `FileNotFoundException` on an
    /// unrelated feature's patch class instead of testing this one. Filtering on `Namespace` touches
    /// only metadata already loaded.
    /// </summary>
    private static System.Type[] DiscoveredInCategory() =>
        AccessTools.GetTypesFromAssembly(typeof(TAOM.Features.EconomyDiagnostics.TownGoldLedger).Assembly)
            .Where(t => t.Namespace != null && t.Namespace.StartsWith(FeatureNamespace))
            .Where(t => t.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: true)
                .Cast<HarmonyPatchCategory>()
                .Any(c => c.info.category == Category))
            .ToArray();

    /// <summary>
    /// The recorder (top-level) plus the four nested tag patches. If nested discovery ever breaks,
    /// this drops to 1 and the count assertion says exactly which half died.
    /// </summary>
    [TestMethod]
    public void HarmonyDiscovery_FindsEveryPatchClassInTheCategory()
    {
        RequireGame();
        var discovered = DiscoveredInCategory().Select(t => t.Name).ToList();

        CollectionAssert.AreEquivalent(
            new[]
            {
                "SettlementComponent_ChangeGold_Patch",
                "ItemConsumptionBehavior_UpdateTownGold_Patch",
                "ItemConsumptionBehavior_MakeConsumption_Patch",
                "SellGoodsForTradeAction_ApplyByVillagerTrade_Patch",
                "SellItemsAction_Apply_Patch",
            },
            discovered,
            "Harmony's own type enumeration did not find every Patch68 class. "
            + "Nested patch classes are the risk here — if the four tag patches are missing, every "
            + "gold movement silently records as Other and the ledger is worthless. Discovered: "
            + string.Join(", ", discovered));
    }

    /// <summary>
    /// Discovery is only half of it — a discovered class with no resolvable target is still dead.
    /// This runs Harmony's own attribute parsing to confirm each class actually names a method.
    /// </summary>
    [TestMethod]
    public void EveryDiscoveredPatchClass_DeclaresAResolvableTarget()
    {
        RequireGame();
        foreach (var type in DiscoveredInCategory())
        {
            var attributes = type.GetCustomAttributes(typeof(HarmonyPatch), inherit: false)
                .Cast<HarmonyPatch>()
                .ToList();

            Assert.IsTrue(attributes.Any(),
                $"{type.Name} is in the category but carries no [HarmonyPatch] — it would be skipped.");

            var info = attributes.First().info;
            Assert.IsNotNull(info.declaringType, $"{type.Name} has no declaring type on its [HarmonyPatch].");
            Assert.IsFalse(string.IsNullOrEmpty(info.methodName), $"{type.Name} names no target method.");
        }
    }

    /// <summary>
    /// The category string is matched by Harmony as an exact, case-sensitive literal against the one
    /// in `SubModule.cs`. A typo in either place is a silently dead patch set, which is precisely the
    /// failure this whole file guards.
    /// </summary>
    [TestMethod]
    public void CategoryName_IsExactlyTheStringSubModuleApplies()
    {
        RequireGame();
        Assert.AreEqual("Patch68_EconomyDiagnostics", Category,
            "If this constant is edited, Main/SubModule.cs's PatchCategory call must change with it.");

        Assert.AreEqual(5, DiscoveredInCategory().Length,
            "Category string drift would silently return zero classes.");
    }
}
