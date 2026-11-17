using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.AlignmentDesertion.Hooks;
using TAOM.Features.CoopInterop;
using TAOM.Tests.Migration;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Tests.Features.AlignmentDesertion;

// #1189 item 1: tavern mercenaries follow the desertion recruit block (Patch158).
[TestClass]
public class DesertionMercenaryPatchTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Cleanup() => Patch158_AiMercenaryHire_Prefix.Initialize(null!, null!, null!);

    private static MobileParty AnyParty() => (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));

    private static IDesertionRecruitGuard Guard(bool wouldDesert)
    {
        var guard = Substitute.For<IDesertionRecruitGuard>();
        guard.WouldDesertFromParty(Arg.Any<PartyBase?>(), Arg.Any<CharacterObject?>()).Returns(wouldDesert);
        return guard;
    }

    private static bool Run(MobileParty party, RecruitmentCampaignBehavior.RecruitingDetail detail = RecruitmentCampaignBehavior.RecruitingDetail.MercenaryFromTavern) =>
        Patch158_AiMercenaryHire_Prefix.Prefix(party, null!, detail);

    private static ICoopSessionProvider Solo(bool defer = false)
    {
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.ShouldDeferToHost.Returns(defer);
        return coop;
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void AiHire_OfADeserter_IsSkipped()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        Patch158_AiMercenaryHire_Prefix.Initialize(Guard(wouldDesert: true), Solo(), Substitute.For<IModLogger>());

        Assert.IsFalse(Run(AnyParty()), "the lord must not pay for mercenaries that desert");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void AiHire_OfATroopThatStays_RunsVanilla()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        Patch158_AiMercenaryHire_Prefix.Initialize(Guard(wouldDesert: false), Solo(), Substitute.For<IModLogger>());

        Assert.IsTrue(Run(AnyParty()));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void AiHire_OnACoopPeerThatDefers_RunsVanilla()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        Patch158_AiMercenaryHire_Prefix.Initialize(Guard(wouldDesert: true), Solo(defer: true), Substitute.For<IModLogger>());

        Assert.IsTrue(Run(AnyParty()), "the guard reads per-peer settings");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void AVolunteerRecruit_ThroughTheSameMethod_RunsVanilla()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        Patch158_AiMercenaryHire_Prefix.Initialize(Guard(wouldDesert: true), Solo(), Substitute.For<IModLogger>());

        Assert.IsTrue(Run(AnyParty(), RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromIndividual),
            "volunteers are capped by the volunteer model; this prefix only guards the tavern");
    }

    [TestMethod]
    public void AiHire_WithNoGuardOrNoParty_RunsVanilla()
    {
        Patch158_AiMercenaryHire_Prefix.Initialize(null!, null!, null!);
        Assert.IsTrue(Run(null!), "no guard");
        Patch158_AiMercenaryHire_Prefix.Initialize(Guard(wouldDesert: true), Solo(), Substitute.For<IModLogger>());
        Assert.IsTrue(Run(null!), "no party");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void AiHire_WhenTheGuardThrows_RunsVanillaAndLogsOnce()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        var guard = Substitute.For<IDesertionRecruitGuard>();
        guard.WouldDesertFromParty(Arg.Any<PartyBase?>(), Arg.Any<CharacterObject?>()).Throws(new InvalidOperationException("x"));
        var logger = Substitute.For<IModLogger>();
        Patch158_AiMercenaryHire_Prefix.Initialize(guard, Solo(), logger);
        typeof(Patch158_AiMercenaryHire_Prefix).GetField("_logged", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);

        Assert.IsTrue(Run(AnyParty()));
        Assert.IsTrue(Run(AnyParty()));
        logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void Targets_ResolveWithTheirParameterNames_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        // Harmony binds patch parameters by name.
        AssertTarget(Patch158_AiMercenaryHire_Prefix.TargetMethod(), typeof(void),
            "side1Party", "settlement", "individual", "troop", "number", "bitCode", "detail");
        AssertTarget(Patch158_MercenaryMenu_Postfix.TargetMethod(), typeof(bool), "args");
        AssertTarget(Patch158_MercenaryDialog_Postfix.TargetMethod(), typeof(void), "campaignGameStarter");
    }

    private static void AssertTarget(MethodBase? method, Type returnType, params string[] parameters)
    {
        var target = method as MethodInfo;
        Assert.IsNotNull(target, "a Patch158 target did not resolve");
        Assert.AreEqual(returnType, target!.ReturnType, target.Name);
        CollectionAssert.AreEqual(parameters, target.GetParameters().Select(p => p.Name).ToArray(), target.Name);
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void BothAiHireBranches_StillCallTheHire_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var check = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "CheckRecruiting", new[] { typeof(MobileParty), typeof(TaleWorlds.CampaignSystem.Settlements.Settlement) });
        Assert.IsNotNull(check);
        var calls = PatchProcessor.GetOriginalInstructions(check!).Count(ci => ci.operand is MethodInfo mi && mi.Name == "ApplyRecruitMercenary");
        Assert.AreEqual(2, calls, "CheckRecruiting no longer hires mercenaries through ApplyRecruitMercenary twice (caravan, lord)");

        var hire = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "ApplyRecruitMercenary");
        var il = PatchProcessor.GetOriginalInstructions(hire!).ToList();
        var call = il.FindIndex(ci => ci.operand is MethodInfo mi && mi.Name == "ApplyInternal");
        Assert.IsTrue(call > 0, "ApplyRecruitMercenary no longer calls ApplyInternal");
        Assert.AreEqual(System.Reflection.Emit.OpCodes.Ldc_I4_0, il[call - 1].opcode, "the tavern hire no longer passes MercenaryFromTavern (0)");
        Assert.AreEqual(0, (int)RecruitmentCampaignBehavior.RecruitingDetail.MercenaryFromTavern);
    }

    // The menu adds the town's TroopType and the dialog adds the speaker; Patch158 checks each of those troops.
    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void PlayerHires_AddTheTroopsPatch158Checks_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        Assert.IsTrue(Calls("buy_mercenaries_on_consequence", "get_TroopType"), "the menu no longer adds the town's TroopType");
        Assert.IsTrue(Calls("BuyMercenaries", "get_OneToOneConversationCharacter"), "the dialog no longer adds the speaker");
    }

    private static bool Calls(string method, string callee)
    {
        var target = AccessTools.Method(typeof(RecruitmentCampaignBehavior), method);
        Assert.IsNotNull(target, method);
        return PatchProcessor.GetOriginalInstructions(target!).Any(ci => ci.operand is MethodInfo mi && mi.Name == callee);
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void PatchCategory_AttachesOnePatchToEachTarget_AndTheJitAccepts()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        const string id = "taom.tests.alignmentdesertion.mercenaries";
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(AlignmentDesertionModule).Assembly).Apply(harmony, AlignmentDesertionModule.MercenaryPatchCategory);

            AssertOnly(Patch158_AiMercenaryHire_Prefix.TargetMethod(), id, typeof(Patch158_AiMercenaryHire_Prefix), prefix: true);
            AssertOnly(Patch158_MercenaryMenu_Postfix.TargetMethod(), id, typeof(Patch158_MercenaryMenu_Postfix), prefix: false);
            AssertOnly(Patch158_MercenaryDialog_Postfix.TargetMethod(), id, typeof(Patch158_MercenaryDialog_Postfix), prefix: false);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    private static void AssertOnly(MethodBase target, string id, Type patchClass, bool prefix)
    {
        var info = Harmony.GetPatchInfo(target);
        Assert.IsNotNull(info, target.Name + " was not patched");
        var prefixes = info!.Prefixes.Where(p => p.owner == id).ToList();
        var postfixes = info.Postfixes.Where(p => p.owner == id).ToList();
        var mine = prefix ? prefixes : postfixes;
        Assert.AreEqual(1, mine.Count, target.Name);
        Assert.AreEqual(patchClass, mine[0].PatchMethod.DeclaringType);
        Assert.AreEqual(1, prefixes.Count + postfixes.Count + info.Finalizers.Count(p => p.owner == id), target.Name);
        RuntimeHelpers.PrepareMethod(target.MethodHandle);
    }
}
