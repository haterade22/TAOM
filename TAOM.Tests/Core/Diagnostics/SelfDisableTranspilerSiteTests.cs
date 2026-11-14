using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Core.Diagnostics;
using TAOM.Features.BannerColorPersistence;
using TAOM.Features.BannerColorPersistence.Hooks;
using TAOM.Features.CastleRecruitment.Hooks;
using TAOM.Features.CharacterSelection.Patches;
using TAOM.Features.CreatureBandits.Diagnostics;
using TAOM.Features.CreatureBandits.Hooks;
using TAOM.Features.Enlistment.Hooks;
using TAOM.Features.MarriageAlignment.Hooks;
using TAOM.Features.MarshalsWord.Hooks;
using TAOM.Features.MissionStartGuard.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Core.Diagnostics;

/// <summary>
/// Issue #1185: each transpiler that returns the method unchanged when its anchor is gone reports the fix as
/// off. Harmony counts such a transpiler as applied, so this report is the only sign the player gets. Every
/// test feeds a stream with no anchor, checks the stream comes back untouched and reads the one ledger entry.
/// Removing the report call from a site fails its test.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SelfDisableTranspilerSiteTests
{
    private SelfDisableLedger _saved = null!;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void SwapInAFreshLedger()
    {
        _saved = SelfDisables.Ledger;
        SelfDisables.Ledger = new SelfDisableLedger();
    }

    [TestCleanup]
    public void PutTheOldLedgerBack() => SelfDisables.Ledger = _saved;

    private static List<CodeInstruction> Ret() => new() { new CodeInstruction(OpCodes.Ret) };

    private static void AssertUnchangedAndReported(IEnumerable<CodeInstruction> result, string id)
    {
        CollectionAssert.AreEqual(new[] { OpCodes.Ret }, result.Select(c => c.opcode).ToArray(), "the stream comes back unchanged");
        var taken = SelfDisables.Ledger.Take();
        Assert.AreEqual(1, taken.Count, "exactly one report");
        Assert.AreEqual(id, taken[0].Id);
        Assert.IsTrue(taken[0].Reason.Length > 0, "the report carries a reason");
    }

    [TestMethod]
    public void CastleAiSwap_NoIsCastle_ReportsPatch42() =>
        AssertUnchangedAndReported(CastleAiTranspiler.SwapIsCastleGate(Ret(), null), "Patch42_CastleRecruitment");

    [TestMethod]
    public void CastleAiSwap_IsCastleWithoutTheAnchor_ReportsPatch42()
    {
        var getter = AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.IsCastle));
        var stream = new List<CodeInstruction> { new(OpCodes.Callvirt, getter), new(OpCodes.Ret) };

        var result = CastleAiTranspiler.SwapIsCastleGate(stream, null);

        CollectionAssert.AreEqual(new[] { OpCodes.Callvirt, OpCodes.Ret }, result.Select(c => c.opcode).ToArray());
        Assert.AreSame(getter, result[0].operand, "the gate is not swapped");
        var taken = SelfDisables.Ledger.Take();
        Assert.AreEqual(1, taken.Count);
        Assert.AreEqual("Patch42_CastleRecruitment", taken[0].Id);
    }

    [TestMethod]
    public void BannerColorRewrite_NullResolvers_ReportsThePartyIcon() =>
        AssertUnchangedAndReported(BannerColorTranspiler.Rewrite(Ret(), null, null, null), "Patch23_BannerColorPersistence.PartyIcon");

    [TestMethod]
    public void BannerColorRewrite_NoColourSites_ReportsThePartyIcon()
    {
        var resolve1 = typeof(MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch).GetMethod(nameof(MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch.ResolveColor1))!;
        var resolve2 = typeof(MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch).GetMethod(nameof(MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch.ResolveColor2))!;

        var result = BannerColorTranspiler.Rewrite(Ret(), resolve1, resolve2, null);

        CollectionAssert.AreEqual(new[] { OpCodes.Ret }, result.Select(c => c.opcode).ToArray());
        var taken = SelfDisables.Ledger.Take();
        Assert.AreEqual(1, taken.Count, "both missing sites share one id, so one entry");
        Assert.AreEqual("Patch23_BannerColorPersistence.PartyIcon", taken[0].Id);
    }

    // The two colour sites report under one id, so each miss needs its own stream: with both sites gone, the
    // report of either site alone keeps the test above green.
    [TestMethod]
    public void BannerColorRewrite_OnlyThePrimaryColourSiteMissing_ReportsThePrimarySite()
    {
        var (resolve1, resolve2) = ColourResolvers();
        var color2 = AccessTools.PropertyGetter(typeof(IFaction), nameof(IFaction.Color2));
        var stream = new List<CodeInstruction> { new(OpCodes.Callvirt, color2), new(OpCodes.Ret) };

        var result = BannerColorTranspiler.Rewrite(stream, resolve1, resolve2, null);

        CollectionAssert.AreEqual(new[] { OpCodes.Callvirt, OpCodes.Ldarg_2, OpCodes.Call, OpCodes.Ret },
            result.Select(c => c.opcode).ToArray(), "the secondary site is still patched");
        var taken = SelfDisables.Ledger.Take();
        Assert.AreEqual(1, taken.Count);
        Assert.AreEqual("Patch23_BannerColorPersistence.PartyIcon", taken[0].Id);
        StringAssert.Contains(taken[0].Reason, "primary colour site");
    }

    [TestMethod]
    public void BannerColorRewrite_OnlyTheSecondaryColourSiteMissing_ReportsTheSecondarySite()
    {
        var (resolve1, resolve2) = ColourResolvers();
        var color = AccessTools.PropertyGetter(typeof(IFaction), nameof(IFaction.Color));
        var stream = new List<CodeInstruction> { new(OpCodes.Callvirt, color), new(OpCodes.Ret) };

        var result = BannerColorTranspiler.Rewrite(stream, resolve1, resolve2, null);

        CollectionAssert.AreEqual(new[] { OpCodes.Callvirt, OpCodes.Ldarg_2, OpCodes.Call, OpCodes.Ret },
            result.Select(c => c.opcode).ToArray(), "the primary site is still patched");
        var taken = SelfDisables.Ledger.Take();
        Assert.AreEqual(1, taken.Count);
        Assert.AreEqual("Patch23_BannerColorPersistence.PartyIcon", taken[0].Id);
        StringAssert.Contains(taken[0].Reason, "secondary colour site");
    }

    [TestMethod]
    public void BannerColorRewrite_BothColourSitesPresent_ReportsNothing()
    {
        var (resolve1, resolve2) = ColourResolvers();
        var stream = new List<CodeInstruction>
        {
            new(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IFaction), nameof(IFaction.Color))),
            new(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IFaction), nameof(IFaction.Color2))),
            new(OpCodes.Ret),
        };

        BannerColorTranspiler.Rewrite(stream, resolve1, resolve2, null);

        Assert.IsFalse(SelfDisables.Ledger.HasPending);
    }

    private static (MethodInfo, MethodInfo) ColourResolvers() => (
        typeof(MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch).GetMethod(nameof(MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch.ResolveColor1))!,
        typeof(MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch).GetMethod(nameof(MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch.ResolveColor2))!);

    [TestMethod]
    public void SceneNotificationTranspiler_NoMapFactionRead_ReportsTheSceneNotification() =>
        AssertUnchangedAndReported(
            CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.Transpiler(Ret()).ToList(),
            "Patch23_BannerColorPersistence.SceneNotification");

    [TestMethod]
    public void RefreshCharacterEntityAux_NoAgentVisualsDataNewobj_ReportsLateTranspiler()
    {
        var transpiler = typeof(RefreshCharacterEntityAuxPatch).GetMethod("Transpiler", BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = (IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object?[] { Ret(), null })!;

        AssertUnchangedAndReported(result.ToList(), "Late_Transpiler");
    }

    [TestMethod]
    public void CreatureWeaponState_NoMonsterFlagsRead_ReportsTheWeaponState()
    {
        var savedSites = CreatureBanditDiag.CreationHookSites;
        try
        {
            AssertUnchangedAndReported(Patch93_CreatureBanditWeaponState.Transpiler(Ret()).ToList(), "Patch93_CreatureBandits.WeaponState");
        }
        finally
        {
            CreatureBanditDiag.CreationHookSites = savedSites;
        }
    }

    [TestMethod]
    public void OrderBannerTranspiler_NoToStringCall_ReportsTheOrderBanner() =>
        AssertUnchangedAndReported(
            BehaviorComponent_OnBehaviorActivated_Transpiler.Transpiler(Ret()).ToList(),
            "Patch66_Enlistment.OrderBanner");

    [TestMethod]
    public void MarriageClanDraw_NoClanAllCall_ReportsPatch81()
    {
        var transpiler = typeof(Patch81_MarriageClanDraw).GetMethod("Transpiler", BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = (IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { Ret() })!;

        AssertUnchangedAndReported(result.ToList(), "Patch81_MarriageAlignment");
    }

    [TestMethod]
    public void SpacingTranspiler_NoHasAnyMountedUnitCall_ReportsTheSpacingRepair() =>
        AssertUnchangedAndReported(MarshalSpacingPatch.Transpiler(Ret(), null!).ToList(), "Patch141_MarshalsWord.SpacingRepair");

    [TestMethod]
    public void MissionStartGuardTranspiler_NoStartCalls_ReportsPatch103()
    {
        var savedSwapped = MissionStartGuardSwaps.LastSwapped;
        try
        {
            AssertUnchangedAndReported(Mission_AfterStart_MissionStartGuard_Patch.Transpiler(Ret()).ToList(), "Patch103_MissionStartGuard");
        }
        finally
        {
            MissionStartGuardSwaps.LastSwapped = savedSwapped;
        }
    }

    [TestMethod]
    public void LayerLimitTranspiler_FlagOnAndNoRemoveRange_ReportsPatch15()
    {
        var provider = Substitute.For<IBannerColorConfigProvider>();
        provider.GetConfig().Returns(new BannerColorConfig { EnableLayerLimitTranspiler = true });
        Banner_TryGetBannerDataFromCode_Transpiler.Initialize(provider, Substitute.For<TAOM.Core.Logging.IModLogger>());
        try
        {
            var result = Banner_TryGetBannerDataFromCode_Transpiler.Transpiler(Ret()).ToList();

            CollectionAssert.AreEqual(new[] { OpCodes.Ret }, result.Select(c => c.opcode).ToArray());
            var taken = SelfDisables.Ledger.Take();
            Assert.AreEqual(1, taken.Count);
            Assert.AreEqual("Patch15_BannerLayerLimit", taken[0].Id);
            Assert.AreEqual("RemoveRange not found", taken[0].Reason);
        }
        finally
        {
            Banner_TryGetBannerDataFromCode_Transpiler.Initialize(null!, null!);
        }
    }

    [TestMethod]
    public void LayerLimitTranspiler_FlagOnAndRemoveRangeWithoutItsGuard_ReportsPatch15()
    {
        var provider = Substitute.For<IBannerColorConfigProvider>();
        provider.GetConfig().Returns(new BannerColorConfig { EnableLayerLimitTranspiler = true });
        Banner_TryGetBannerDataFromCode_Transpiler.Initialize(provider, Substitute.For<TAOM.Core.Logging.IModLogger>());
        try
        {
            var removeRange = typeof(List<BannerData>).GetMethod(nameof(List<BannerData>.RemoveRange))!;
            var stream = new List<CodeInstruction> { new(OpCodes.Callvirt, removeRange), new(OpCodes.Ret) };

            var result = Banner_TryGetBannerDataFromCode_Transpiler.Transpiler(stream).ToList();

            CollectionAssert.AreEqual(new[] { OpCodes.Callvirt, OpCodes.Ret }, result.Select(c => c.opcode).ToArray(),
                "the stream comes back unchanged");
            var taken = SelfDisables.Ledger.Take();
            Assert.AreEqual(1, taken.Count);
            Assert.AreEqual("Patch15_BannerLayerLimit", taken[0].Id);
            Assert.AreEqual("Ble_S guard not found", taken[0].Reason);
        }
        finally
        {
            Banner_TryGetBannerDataFromCode_Transpiler.Initialize(null!, null!);
        }
    }
}
