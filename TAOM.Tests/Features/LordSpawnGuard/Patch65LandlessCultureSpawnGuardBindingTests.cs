using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LordSpawnGuard;

/// <summary>
/// Drift-guard for Patch65. <c>HarmonyPatchBindingTests</c> already resolves the target, but this
/// one pins the two things that make the patch silently useless rather than loudly broken:
///
///  - <c>SpawnLordParty</c> is <b>private</b> and matched by name. On a rename, Harmony does NOT
///    fail silently — verified against the shipped 0Harmony: <c>PatchClassProcessor
///    .PatchWithAttributes</c> throws <c>ArgumentException("Undefined target method for patch
///    method …")</c> when <c>GetOriginalMethod()</c> returns null, and <c>ReportException</c>
///    rethrows it as a <c>HarmonyException</c>. Since <c>SubModule</c>'s
///    <c>OnGameInitializationFinished</c> batch does not wrap its <c>PatchCategory</c> calls, that
///    surfaces as a module-load crash taking the rest of the batch with it. This test exists to
///    turn that into a red build instead. (An earlier revision of this comment claimed the
///    opposite — "no warning anywhere" — which would send crash triage looking for a silent no-op
///    that cannot happen.)
///  - The finalizer declares <c>ref MobileParty __result</c>. If the return type ever changes,
///    Harmony throws while applying the category at module load, same path.
///
/// Also asserts the <c>Hero</c> first parameter the prefix binds by name.
/// </summary>
[TestClass]
public class Patch65LandlessCultureSpawnGuardBindingTests
{
    private const string BehaviorTypeName =
        "TaleWorlds.CampaignSystem.CampaignBehaviors.HeroSpawnCampaignBehavior";
    private const string MethodName = "SpawnLordParty";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SpawnLordParty_BindingResolves_AgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var behaviorType = AccessTools.TypeByName(BehaviorTypeName);
        Assert.IsNotNull(
            behaviorType,
            BehaviorTypeName + " did not resolve against the installed engine — Patch65's target type is gone.");

        var method = AccessTools.Method(behaviorType, MethodName);
        Assert.IsNotNull(
            method,
            "HeroSpawnCampaignBehavior.SpawnLordParty did not resolve — Patch65 would apply to nothing " +
            "and the landless-culture CTD would return unguarded.");

        Assert.AreEqual(
            "MobileParty", method.ReturnType.Name,
            "SpawnLordParty no longer returns MobileParty — Patch65's `ref MobileParty __result` " +
            "finalizer would throw while applying the category at module load.");

        var parameters = method.GetParameters();
        Assert.IsTrue(parameters.Length >= 1, "SpawnLordParty takes no parameters — signature drifted.");
        Assert.AreEqual(
            "Hero", parameters[0].ParameterType.Name,
            "SpawnLordParty's first parameter is no longer a Hero — Patch65's prefix binds `Hero hero` by name.");
        Assert.AreEqual(
            "hero", parameters[0].Name,
            "SpawnLordParty's first parameter was renamed — Harmony binds prefix arguments by parameter name.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ClanInitialHomeSettlement_SetterIsWritable_AgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var clanType = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Clan");
        Assert.IsNotNull(clanType, "TaleWorlds.CampaignSystem.Clan did not resolve.");

        var setter = AccessTools.Method(clanType, "SetInitialHomeSettlement");
        Assert.IsNotNull(
            setter,
            "Clan.SetInitialHomeSettlement is gone — LordSpawnGuardAdapter cannot anchor a clan.");

        // The kingdom path goes through a private setter, cached in a static field on the adapter.
        var kingdomType = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Kingdom");
        Assert.IsNotNull(kingdomType, "TaleWorlds.CampaignSystem.Kingdom did not resolve.");

        MethodInfo kingdomSetter = AccessTools.PropertySetter(kingdomType, "InitialHomeSettlement");
        Assert.IsNotNull(
            kingdomSetter,
            "Kingdom.InitialHomeSettlement has no setter — LordSpawnGuardAdapter's kingdom path is dead " +
            "(it degrades to a logged warning plus the Patch65 finalizer, but the repair is lost).");
    }
}
