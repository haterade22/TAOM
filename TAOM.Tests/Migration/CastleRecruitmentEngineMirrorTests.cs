using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.CastleRecruitment.Hooks;

namespace TAOM.Tests.Migration;

/// <summary>
/// Pins seven vanilla facts the castle recruitment code relies on, so an engine bump that changes one
/// goes red instead of silently skewing the feature: the private <c>CheckRecruiting</c> still binds to the
/// dispatcher's delegate shape, <c>RecruitmentCampaignBehavior.HourlyTickParty</c> still skips castles, and
/// <c>GetApproximateVolunteersCanBeRecruitedDataFromSettlement</c> still counts 4 and 2 slots, and the private
/// <c>RecruitVolunteersFromNotable</c> still asks the volunteer model. For the #918 check, that recruit still reaches
/// <c>ApplyInternal</c> through <c>GetRecruitVolunteerFromIndividual</c>, <c>ApplyInternal</c> still raises OnTroopRecruited and the garrison auto-recruit still asks the garrison query, never the hero
/// query the castle rule answers. The dispatcher's copy of the outer party
/// guard, the siege filter and the co-op stand-down are not pinned.
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

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void ApplyInternal_StillRaisesOnTroopRecruited_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var target = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "ApplyInternal");
        Assert.IsNotNull(target, "RecruitmentCampaignBehavior.ApplyInternal did not resolve.");

        var il = PatchProcessor.GetOriginalInstructions(target!).ToList();

        Assert.IsTrue(CallsMethod(il, typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnTroopRecruited)),
            "ApplyInternal no longer calls CampaignEventDispatcher.OnTroopRecruited. CastleRecruitObserver (#918) sees "
            + "AI castle recruits only through that event; without it the check goes blind and passes.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void RecruitVolunteersFromNotable_StillReachesApplyInternal_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var fromNotable = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "RecruitVolunteersFromNotable", new[] { typeof(MobileParty), typeof(Settlement) });
        var fromIndividual = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "GetRecruitVolunteerFromIndividual");
        Assert.IsNotNull(fromNotable, "RecruitmentCampaignBehavior.RecruitVolunteersFromNotable(MobileParty, Settlement) did not resolve.");
        Assert.IsNotNull(fromIndividual, "RecruitmentCampaignBehavior.GetRecruitVolunteerFromIndividual did not resolve.");

        // The other two links of the chain the #918 check watches: the notable recruit, then the slot write, then the event.
        Assert.IsTrue(CallsMethod(PatchProcessor.GetOriginalInstructions(fromNotable!), typeof(RecruitmentCampaignBehavior), "GetRecruitVolunteerFromIndividual"),
            "RecruitVolunteersFromNotable no longer calls GetRecruitVolunteerFromIndividual. CastleRecruitObserver (#918) "
            + "may no longer see AI castle recruits, so the check could pass on a day an AI lord recruited.");
        Assert.IsTrue(CallsMethod(PatchProcessor.GetOriginalInstructions(fromIndividual!), typeof(RecruitmentCampaignBehavior), "ApplyInternal"),
            "GetRecruitVolunteerFromIndividual no longer calls ApplyInternal, which raises OnTroopRecruited. "
            + "CastleRecruitObserver (#918) may no longer see AI castle recruits.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void GarrisonAutoRecruit_UsesTheGarrisonQuery_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var target = AccessTools.Method(typeof(GarrisonRecruitmentCampaignBehavior), "RepopulateVolunteerListCache", new[] { typeof(Town) });
        Assert.IsNotNull(target, "GarrisonRecruitmentCampaignBehavior.RepopulateVolunteerListCache(Town) did not resolve.");

        var il = PatchProcessor.GetOriginalInstructions(target!).ToList();

        Assert.IsTrue(CallsMethod(il, typeof(VolunteerModel), nameof(VolunteerModel.MaximumIndexGarrisonCanRecruitFromHero)),
            "The garrison auto-recruit no longer asks VolunteerModel.MaximumIndexGarrisonCanRecruitFromHero.");
        Assert.IsFalse(CallsMethod(il, typeof(VolunteerModel), nameof(VolunteerModel.MaximumIndexHeroCanRecruitFromHero)),
            "The garrison auto-recruit now asks MaximumIndexHeroCanRecruitFromHero, so TaomVolunteerModel's castle rule "
            + "(#918) may block castle garrisons: re-check the rule's buyer test.");
    }

    // A call to an override names the base declaration (CampaignEventDispatcher.OnTroopRecruited overrides
    // CampaignEventReceiver's), so the operand's type may be the given type or a base of it.
    private static bool CallsMethod(System.Collections.Generic.IEnumerable<CodeInstruction> il, Type type, string name) =>
        il.Any(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
            && ci.operand is MethodInfo mi && mi.Name == name && mi.DeclaringType != null && mi.DeclaringType.IsAssignableFrom(type));

    private static bool CallsGetter(System.Collections.Generic.IEnumerable<CodeInstruction> il, string property)
    {
        var getter = AccessTools.PropertyGetter(typeof(Settlement), property);
        return il.Any(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
            && ci.operand is MethodInfo mi && mi.Name == getter.Name && mi.DeclaringType == getter.DeclaringType);
    }
}
