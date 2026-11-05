using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.CastleRecruitment.Hooks;
using NSubstitute;
using TAOM.Core.Logging;

namespace TAOM.Tests.Migration;

/// <summary>
/// Pins four vanilla facts the castle recruitment code relies on, so an engine bump that changes one
/// goes red instead of silently skewing the feature: the private <c>CheckRecruiting</c> still binds to the
/// dispatcher's delegate shape, <c>RecruitmentCampaignBehavior.HourlyTickParty</c> still skips castles, and
/// <c>GetApproximateVolunteersCanBeRecruitedDataFromSettlement</c> still counts 4 and 2 slots, and the private
/// <c>RecruitVolunteersFromNotable</c> still asks the volunteer model. The dispatcher's copy of the outer party
/// guard and the siege filter are not pinned.
/// Each test reads the installed engine IL or signature, with no campaign.
/// </summary>
[TestClass]
public class CastleRecruitmentEngineMirrorTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void CheckRecruiting_StillBindsToTheDispatcherDelegateShape_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var m = CastleAiRecruitDispatcher.FindCheckRecruiting();

        Assert.IsNotNull(m, "RecruitmentCampaignBehavior.CheckRecruiting(MobileParty, Settlement) did not resolve.");
        Assert.AreEqual(typeof(void), m!.ReturnType, "CheckRecruiting no longer returns void.");
        Assert.IsFalse(m.IsStatic, "CheckRecruiting became static; the closed delegate bind would fail.");
        // An open-instance delegate proves the same signature compatibility without a campaign.
        Assert.IsNotNull(
            Delegate.CreateDelegate(typeof(Action<RecruitmentCampaignBehavior, MobileParty, Settlement>), m, throwOnBindFailure: false),
            "CheckRecruiting no longer binds to Action<MobileParty, Settlement>; the in-castle AI recruit is inert.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void HourlyTickParty_StillSkipsCastles_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var target = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "HourlyTickParty", new[] { typeof(MobileParty) });
        Assert.IsNotNull(target, "RecruitmentCampaignBehavior.HourlyTickParty(MobileParty) did not resolve.");

        var il = PatchProcessor.GetOriginalInstructions(target!).ToList();

        Assert.IsTrue(CallsGetter(il, nameof(Settlement.IsTown)), "HourlyTickParty no longer checks IsTown.");
        Assert.IsTrue(CallsGetter(il, nameof(Settlement.IsVillage)), "HourlyTickParty no longer checks IsVillage.");
        Assert.IsFalse(CallsGetter(il, nameof(Settlement.IsCastle)),
            "Vanilla HourlyTickParty now mentions IsCastle. If it recruits in castles hourly, the "
            + "CastleRecruitmentBehavior HourlyTickPartyEvent listener would double-recruit: delete the listener.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void VolunteerEstimate_StillCountsFourOwnAndTwoForeignSlots_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var target = AccessTools.Method(typeof(AiVisitSettlementBehavior), "GetApproximateVolunteersCanBeRecruitedDataFromSettlement");
        Assert.IsNotNull(target, "AiVisitSettlementBehavior.GetApproximateVolunteersCanBeRecruitedDataFromSettlement did not resolve.");

        var il = PatchProcessor.GetOriginalInstructions(target!).ToList();

        Assert.IsTrue(il.Any(ci => ci.opcode == OpCodes.Ldc_I4_4),
            "The volunteer estimate no longer loads the constant 4. Re-check CastleVolunteerScan.OwnFactionSlotCount.");
        Assert.IsTrue(il.Any(ci => ci.opcode == OpCodes.Ldc_I4_2),
            "The volunteer estimate no longer loads the constant 2. Re-check CastleVolunteerScan.OwnFactionSlotCount.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void RecruitVolunteersFromNotable_StillAsksTheVolunteerModel_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var target = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "RecruitVolunteersFromNotable", new[] { typeof(MobileParty), typeof(Settlement) });
        Assert.IsNotNull(target, "RecruitmentCampaignBehavior.RecruitVolunteersFromNotable(MobileParty, Settlement) did not resolve.");

        var il = PatchProcessor.GetOriginalInstructions(target!).ToList();

        Assert.IsTrue(
            il.Any(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
                && ci.operand is MethodInfo mi && mi.Name == nameof(VolunteerModel.MaximumIndexHeroCanRecruitFromHero)
                && mi.DeclaringType == typeof(VolunteerModel)),
            "RecruitVolunteersFromNotable no longer calls VolunteerModel.MaximumIndexHeroCanRecruitFromHero. "
            + "TaomVolunteerModel's castle and alignment rules (#918) block AI lord recruits only through this call; "
            + "if it moved, re-check where AI lords take volunteers.");
    }

    private static bool CallsGetter(System.Collections.Generic.IEnumerable<CodeInstruction> il, string property)
    {
        var getter = AccessTools.PropertyGetter(typeof(Settlement), property);
        return il.Any(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
            && ci.operand is MethodInfo mi && mi.Name == getter.Name && mi.DeclaringType == getter.DeclaringType);
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void CastleRecruitment_AiHourlyTickGate_StillResolves_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var target = Patch42_AiHourlyTick_Transpiler.TargetMethod();
        Assert.IsNotNull(target,
            "AiVisitSettlementBehavior.AiHourlyTick did not resolve. AI lords no longer score castles for recruitment.");

        var il = PatchProcessor.GetOriginalInstructions((MethodBase)target!).ToList();
        var logger = Substitute.For<IModLogger>();

        var rewritten = CastleAiTranspiler.SwapIsCastleGate(il, logger);

        logger.DidNotReceive().LogWarning(Arg.Any<string>());

        var toggle = AccessTools.Method(typeof(CastleAiToggle), nameof(CastleAiToggle.IsExcludedFromRecruitScoring));
        Assert.AreEqual(1, rewritten.Count(ci => ci.operand is MethodInfo mi && mi == toggle),
            "Expected exactly one IsCastle gate to be swapped for CastleAiToggle.IsExcludedFromRecruitScoring.");
    }
}
