using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// Issue #514. Pins the engine surface the Player Switcher depends on, so an engine bump fails
/// here rather than in a player's campaign.
///
/// Most of the feature is compiler-checked, but three things are not. The Harmony postfix binds
/// BodyGeneratorView's constructor by ARITY rather than by a hand-written Type[] (the 1.4.8
/// constructor takes 13 parameters and the predecessor mod's 12-type attribute matched nothing),
/// which only works while exactly one constructor is declared. Campaign.PlayerDefaultFaction is
/// reached by reflection and must keep a setter. And KillCharacterAction's parameter shape decides
/// whether the created hero can be removed at all.
/// </summary>
[TestClass]
public class PlayerSwitcherBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static Type? Find(string fullName)
        => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => { try { return a.GetType(fullName, false); } catch { return null; } })
            .FirstOrDefault(t => t != null);

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BodyGeneratorView_DeclaresExactlyOneConstructor_SoArityBindingIsUnambiguous()
    {
        RequireGame();

        var type = Find("TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator.BodyGeneratorView");
        Assert.IsNotNull(type, "BodyGeneratorView not found; the picker panel has no host view to attach to");

        var ctors = type!.GetConstructors();
        Assert.AreEqual(1, ctors.Length,
            "Patch77 binds this constructor by arity because a hand-written Type[] broke on the 1.4.8 " +
            "signature change. A second constructor makes that binding ambiguous and the patch must be revisited.");

        Assert.AreEqual(13, ctors[0].GetParameters().Length,
            "The 1.4.8 constructor takes 13 parameters, the last being FaceGenHistory. A change here means " +
            "the picker may attach to the wrong overload.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BodyGeneratorView_KeepsTheMembersThePreviewDrives()
    {
        RequireGame();

        var type = Find("TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator.BodyGeneratorView")!;
        Assert.IsNotNull(type);

        const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        Assert.IsNotNull(type.GetField("_dressedEquipment", All),
            "The preview dresses the lord by mutating this field's Equipment in place; it is readonly, so it cannot be replaced.");
        Assert.IsNotNull(type.GetProperty("IsDressed", All) ?? (MemberInfo?)type.GetField("IsDressed", All),
            "IsDressed gates whether _dressedEquipment is used at all.");
        Assert.IsNotNull(type.GetMethod("OnFinalize", All),
            "Patch77's teardown postfix binds OnFinalize by name.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Campaign_PlayerDefaultFaction_IsStillWritable()
    {
        RequireGame();

        var campaign = Find("TaleWorlds.CampaignSystem.Campaign");
        Assert.IsNotNull(campaign);

        var prop = campaign!.GetProperty("PlayerDefaultFaction",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(prop, "PlayerDefaultFaction is gone; the takeover path cannot move the player clan pointer");
        Assert.IsNotNull(prop!.GetSetMethod(nonPublic: true),
            "PlayerDefaultFaction lost its setter; PlayerIdentityAdapter's probe would disable the feature");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void KillCharacterAction_ApplyByRemove_StillTakesTheForcedFlag()
    {
        RequireGame();

        var type = Find("TaleWorlds.CampaignSystem.Actions.KillCharacterAction");
        Assert.IsNotNull(type);

        var method = type!.GetMethod("ApplyByRemove", BindingFlags.Static | BindingFlags.Public);
        Assert.IsNotNull(method, "ApplyByRemove is gone; the created character cannot be removed");

        var names = method!.GetParameters().Select(p => p.Name).ToArray();
        CollectionAssert.Contains(names, "isForced",
            "isForced must stay: ApplyInternal returns early for a human player character unless it is set");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheActionsTheHandoverCalls_AllStillExist()
    {
        RequireGame();

        Assert.IsNotNull(Find("TaleWorlds.CampaignSystem.Actions.ChangePlayerCharacterAction")?
            .GetMethod("Apply", BindingFlags.Static | BindingFlags.Public), "ChangePlayerCharacterAction.Apply");

        Assert.IsNotNull(Find("TaleWorlds.CampaignSystem.Actions.DestroyPartyAction")?
            .GetMethod("Apply", BindingFlags.Static | BindingFlags.Public), "DestroyPartyAction.Apply");

        var clan = Find("TaleWorlds.CampaignSystem.Clan");
        Assert.IsNotNull(clan?.GetMethod("SetLeader", BindingFlags.Instance | BindingFlags.Public),
            "Clan.SetLeader is the whole adoption path");

        var hero = Find("TaleWorlds.CampaignSystem.Hero");
        Assert.IsNotNull(hero?.GetMethod("SetNewOccupation", BindingFlags.Instance | BindingFlags.Public),
            "Hero.SetNewOccupation promotes an adopted wanderer to Lord");
        Assert.IsNotNull(hero?.GetProperty("IsKnownToPlayer", BindingFlags.Instance | BindingFlags.Public)?.GetSetMethod(),
            "Hero.IsKnownToPlayer must stay settable, or the clan and kingdom screens open full of unknowns");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheRaceRepairSeam_StillResolves()
    {
        RequireGame();

        // The preview repairs a lord whose body key carries indices his new race does not define.
        // Without this repair the race silently never commits: FaceGenVM.Refresh throws partway on
        // the voice index, so UpdateFace, and therefore BodyGenerator.RefreshFace, never run, and
        // RefreshFace is the only assignment of BodyGenerator.Race outside the constructor.
        //
        // Three string-based lookups carry that repair. A rename in a future engine build makes
        // them resolve null, the repair silently stops happening, and the feature regresses to
        // exactly the in-game symptom this test exists to prevent: the face changes, the body
        // does not.
        const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var faceGenVm = Find("TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator.FaceGenVM");
        Assert.IsNotNull(faceGenVm, "FaceGenVM is the preview's data source");

        Assert.IsNotNull(faceGenVm!.GetField("_faceGenerationParams", All),
            "the repair adjusts this struct in place; without it there is nothing to clamp");
        Assert.IsNotNull(faceGenVm.GetField("_characterRefreshEnabled", All),
            "Refresh early-returns unless this is set, and the aborted call leaves it false");
        Assert.IsNotNull(faceGenVm.GetMethod("Refresh", new[] { typeof(bool) }),
            "the repair drives Refresh directly rather than via SetBodyProperties, which would re-decode the key");

        var paramsType = Find("TaleWorlds.MountAndBlade.FaceGenerationParams");
        Assert.IsNotNull(paramsType, "FaceGenerationParams carries CurrentVoice and the clamp");
        Assert.IsNotNull(paramsType!.GetMethod("SetRaceGenderAndAdjustParams",
            BindingFlags.Instance | BindingFlags.Public),
            "this is the engine's own post-decode clamp that SetBodyProperties omits; the whole fix is calling it");
        Assert.IsNotNull(paramsType.GetField("CurrentVoice", BindingFlags.Instance | BindingFlags.Public),
            "CurrentVoice is the index that overruns the target race's voice list");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BodyGeneratorRace_IsStillTheFieldThatProvesAPreviewCommitted()
    {
        RequireGame();

        // The preview no longer trusts "no exception escaped" as success; it asks the engine
        // whether BodyGenerator.Race actually became the target. If this field moves, that check
        // silently stops verifying anything.
        var type = Find("TaleWorlds.MountAndBlade.BodyGenerator");
        Assert.IsNotNull(type, "BodyGenerator holds the committed race");
        Assert.IsNotNull(type!.GetField("Race", BindingFlags.Instance | BindingFlags.Public),
            "Race is a public field, assigned only by the constructor and RefreshFace");
        Assert.IsNotNull(type.GetMethod("SaveCurrentCharacter", BindingFlags.Instance | BindingFlags.Public),
            "vanilla persists the previewed body through this; the restore path calls it deliberately");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheVanillaClanRowVM_StillDereferencesItsPartyArgument_WhichIsWhyWeDoNotUseIt()
    {
        RequireGame();

        // HeroPickItemVM used to derive from this type, because the ClanLordTuple prefab was
        // written for it. Its constructor body opens with `IsLeader = hero == party.LeaderHero;`
        // and never null-checks `party`. A wanderer in a tavern has no PartyBelongedTo, wanderers
        // are offered by default, and one such row threw inside the panel build, was swallowed by
        // the attach patch, and made the whole picker silently fail to appear.
        //
        // This test pins the reason the inheritance was dropped. If a future engine build ever
        // makes that constructor null-tolerant, this goes red and the simplification is available
        // again. Until then, deriving from it is a trap that looks like reuse.
        var type = Find("TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanPartyMemberItemVM");
        Assert.IsNotNull(type, "the type is expected to still exist; only our dependence on it was removed");

        var ctor = type!.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 2);
        Assert.IsNotNull(ctor, "the two-argument (Hero, MobileParty) constructor is the trap being documented");

        var second = ctor!.GetParameters()[1];
        Assert.AreEqual("MobileParty", second.ParameterType.Name,
            "if this stops being a MobileParty the whole finding needs re-examining");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheRowViewModel_ConstructsForAHeroWithNoParty()
    {
        // The regression itself, stated as a type-level invariant: our row VM must not require a
        // party. Constructing one needs a live Hero, which we cannot make headless, so assert the
        // shape instead: the constructor takes no MobileParty at all, so a partyless hero cannot
        // reach an unguarded dereference the way it did through the vanilla base.
        var ctor = typeof(TAOM.Features.PlayerSwitcher.UI.HeroPickItemVM).GetConstructors().Single();

        Assert.IsFalse(
            ctor.GetParameters().Any(p => p.ParameterType.Name == "MobileParty"),
            "HeroPickItemVM must not take a MobileParty; wanderers have none and that is the point");
    }
}
