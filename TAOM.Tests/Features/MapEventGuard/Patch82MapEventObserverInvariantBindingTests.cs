using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MapEventGuard;

/// <summary>
/// Drift-guards for Patch82, the backstop under crash bundle 31942985 (issue #551).
///
/// Every fact this patch stands on is an engine detail it cannot see change: the target method, the
/// internal property it repairs, and the public property whose null value is the whole trigger. A
/// rename in any of them makes the guard silently inert, and the next time TAOM removes the main
/// party from a live map event the game returns to crashing in
/// <c>MapEventSide.AllocateTroops</c> with nothing in the stack to say why.
///
/// The invariant itself cannot be unit-tested — <c>MapEvent</c> has no public constructor and the
/// crash needs a live campaign — so these pin what the patch BINDS to. The behaviour is verified in
/// game, per the feature doc.
/// </summary>
[TestClass]
public class Patch82MapEventObserverInvariantBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static System.Type MapEventType()
    {
        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.MapEvents.MapEvent");
        Assert.IsNotNull(type, "MapEvent did not resolve — Patch82 has no target at all.");
        return type;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SimulateBattleSetup_ResolvesAsThePatchTarget()
    {
        RequireGame();

        var method = AccessTools.Method(MapEventType(), "SimulateBattleSetup");
        Assert.IsNotNull(method, "MapEvent.SimulateBattleSetup did not resolve — the prefix would never apply.");
        Assert.IsTrue(method.IsPublic, "SimulateBattleSetup stopped being public; nameof() in the attribute would not compile.");

        // One parameter, the prior-troops array. Pinned because the prefix takes only __instance:
        // if an overload appeared, Harmony's by-name match would become ambiguous.
        var parameters = method.GetParameters();
        Assert.AreEqual(1, parameters.Length, "SimulateBattleSetup arity drifted.");
        Assert.IsTrue(parameters[0].ParameterType.IsArray, "SimulateBattleSetup no longer takes the prior-troops array.");

        var overloads = MapEventType().GetMethods(AccessTools.all)
            .Count(m => m.Name == "SimulateBattleSetup");
        Assert.AreEqual(1, overloads,
            "SimulateBattleSetup gained an overload — [HarmonyPatch] by name is now ambiguous and must name the signature.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BattleObserver_IsStillAnInternalReadWriteProperty()
    {
        RequireGame();

        var property = AccessTools.Property(MapEventType(), "BattleObserver");
        Assert.IsNotNull(property, "MapEvent.BattleObserver did not resolve — the guard cannot read or clear it.");

        // The repair is a WRITE, and the property is internal on both halves. Losing the setter is
        // the failure that would matter most: the guard could still detect the dangling observer
        // and would then be unable to do anything about it.
        Assert.IsNotNull(property.GetGetMethod(nonPublic: true), "BattleObserver lost its getter.");
        Assert.IsNotNull(property.GetSetMethod(nonPublic: true), "BattleObserver lost its setter — Patch82 cannot repair anything.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TroopUpgradeTracker_IsStillAPublicReferenceTypeProperty()
    {
        RequireGame();

        var property = AccessTools.Property(MapEventType(), "TroopUpgradeTracker");
        Assert.IsNotNull(property, "MapEvent.TroopUpgradeTracker did not resolve — the guard has no trigger condition.");
        Assert.IsNotNull(property.GetGetMethod(nonPublic: false), "TroopUpgradeTracker stopped being publicly readable.");

        // A value type could never be null, so the guard's whole premise would be gone.
        Assert.IsFalse(property.PropertyType.IsValueType,
            "TroopUpgradeTracker became a value type — it can no longer be null and this guard is obsolete.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AllocateTroops_StillExists_AsTheUnguardedReaderThisProtects()
    {
        RequireGame();

        // The crash site itself. If MapEventSide.AllocateTroops is gone, TaleWorlds has restructured
        // simulation setup and this guard needs re-deriving rather than quietly continuing to pass.
        var side = AccessTools.TypeByName("TaleWorlds.CampaignSystem.MapEvents.MapEventSide");
        Assert.IsNotNull(side, "MapEventSide did not resolve — re-derive Patch82 against the new shape.");

        var allocate = AccessTools.Method(side, "AllocateTroops");
        Assert.IsNotNull(allocate,
            "MapEventSide.AllocateTroops is gone — the crash Patch82 guards may no longer exist, or may have moved.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchClass_IsRegisteredInAllThreePlaces()
    {
        // A patch needs all three or it is dead code with no error, warning or log line: the target
        // attribute, the category attribute, and a matching PatchCategory call in SubModule.cs.
        // Patch39_BanditPartySize shipped missing the second and all five deep-review agents
        // missed it (lessons/harmony-il.md), so all three are asserted here rather than assumed.
        var patch = typeof(TAOM.Features.MapEventGuard.Hooks.Patch82_MapEventObserverInvariant);

        var target = patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false);
        Assert.AreEqual(1, target.Length, "Patch82 lost its [HarmonyPatch] target attribute.");

        var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
            .Cast<HarmonyPatchCategory>()
            .Select(c => c.info.category)
            .ToList();
        CollectionAssert.Contains(categories, "Patch82_MapEventObserverInvariant",
            "Patch82 lost its [HarmonyPatchCategory] — SubModule's PatchCategory call would apply nothing.");

        // Source text rather than IL: the call is one literal in one file, and reading it says
        // exactly what a human would check. A commented-out line does not count, which is why the
        // match requires the statement rather than the bare string.
        var subModule = Path.Combine(FindRepoRoot(), "Main", "SubModule.cs");
        Assert.IsTrue(File.Exists(subModule), $"SubModule.cs not found at {subModule}");
        StringAssert.Contains(
            File.ReadAllText(subModule),
            "_harmony.PatchCategory(\"Patch82_MapEventObserverInvariant\")",
            "SubModule.cs no longer applies Patch82_MapEventObserverInvariant — the patch is dead code.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }
}
