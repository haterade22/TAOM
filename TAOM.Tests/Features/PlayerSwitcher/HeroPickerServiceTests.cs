using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.PlayerSwitcher;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// Issue #514. Every eligibility and grouping rule for the hero picker. The service is
/// deliberately engine-free so all of this runs with no campaign: the adapter hands it plain
/// PickableHeroInfo and the service decides what the player may take over.
/// </summary>
[TestClass]
public class HeroPickerServiceTests
{
    private const string Culture = "erebor";

    private IHeroPickerAdapter _adapter = null!;
    private HeroPickerService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IHeroPickerAdapter>();
        _sut = new HeroPickerService(_adapter);
    }

    private static PickableHeroInfo Hero(
        string id,
        string name = "Lord",
        string culture = Culture,
        string clanId = "clan_a",
        int race = 0,
        bool isFemale = false,
        bool isChild = false,
        bool isWanderer = false,
        bool isNotable = false,
        bool isMainHero = false,
        bool isClanLeader = false,
        bool isKingdomLeader = false,
        bool isSpouseOfKingdomLeader = false,
        bool isChildOfKingdomLeader = false,
        bool isLoreLocked = false)
        => new PickableHeroInfo(id, name, culture, clanId, race, isFemale, isChild,
            isWanderer, isNotable, isMainHero, isClanLeader, isKingdomLeader,
            isSpouseOfKingdomLeader, isChildOfKingdomLeader, isLoreLocked);

    private void Given(params PickableHeroInfo[] heroes)
        => _adapter.GetCandidates(Culture).Returns(heroes.ToList());

    private HeroPickList Build(PlayerSwitchPolicy? policy = null)
        => _sut.BuildPickList(Culture, policy ?? PlayerSwitchPolicy.Default);

    private static string[] Ids(IReadOnlyList<HeroPickRow> rows) => rows.Select(r => r.HeroId).ToArray();

    // ---------- Ruling house grouping ----------

    [TestMethod]
    public void KingdomLeader_IsFirstRowOfRulingHouse()
    {
        Given(
            Hero("child", isChildOfKingdomLeader: true),
            Hero("king", isKingdomLeader: true, isClanLeader: true));

        var list = Build();

        Assert.AreEqual("king", list.RulingHouse[0].HeroId, "the ruler must lead their own list");
    }

    [TestMethod]
    public void RulingHouse_IncludesAdultChildrenAndSpouseOfTheRuler()
    {
        Given(
            Hero("king", isKingdomLeader: true, isClanLeader: true),
            Hero("son", isChildOfKingdomLeader: true),
            Hero("queen", isSpouseOfKingdomLeader: true, isFemale: true));

        var list = Build();

        CollectionAssert.AreEquivalent(new[] { "king", "son", "queen" }, Ids(list.RulingHouse));
    }

    [TestMethod]
    public void RulingHouse_ExcludesChildren()
    {
        Given(
            Hero("king", isKingdomLeader: true, isClanLeader: true),
            Hero("infant", isChildOfKingdomLeader: true, isChild: true));

        var list = Build();

        CollectionAssert.DoesNotContain(Ids(list.RulingHouse), "infant");
    }

    // ---------- Clan leaders grouping ----------

    [TestMethod]
    public void ClanLeaders_ContainsLeadersWhoAreNotInTheRulingHouse()
    {
        Given(
            Hero("king", clanId: "clan_royal", isKingdomLeader: true, isClanLeader: true),
            Hero("thane", clanId: "clan_b", isClanLeader: true));

        var list = Build();

        CollectionAssert.AreEquivalent(new[] { "thane" }, Ids(list.ClanLeaders));
    }

    [TestMethod]
    public void ClanLeaders_DoesNotRepeatAHeroAlreadyInTheRulingHouse()
    {
        Given(Hero("king", isKingdomLeader: true, isClanLeader: true));

        var list = Build();

        Assert.AreEqual(1, list.RulingHouse.Count);
        Assert.AreEqual(0, list.ClanLeaders.Count, "the ruler must not appear twice");
    }

    [TestMethod]
    public void NonLeaders_OutsideTheRulingHouse_AreNotOffered()
    {
        Given(Hero("random_vassal", clanId: "clan_b", isClanLeader: false));

        var list = Build();

        Assert.AreEqual(0, list.TotalCount, "only rulers, their family, clan leaders and wanderers");
    }

    // ---------- Wanderers ----------

    [TestMethod]
    public void Wanderers_ArePresentWhenThePolicyAllowsThem()
    {
        Given(Hero("drifter", clanId: "", isWanderer: true));

        var list = Build();

        CollectionAssert.AreEquivalent(new[] { "drifter" }, Ids(list.Wanderers));
    }

    [TestMethod]
    public void Wanderers_AreOmittedWhenThePolicyExcludesThem()
    {
        Given(Hero("drifter", clanId: "", isWanderer: true));

        var list = Build(new PlayerSwitchPolicy(true, includeWanderers: false, false, false));

        Assert.AreEqual(0, list.Wanderers.Count);
    }

    [TestMethod]
    public void Wanderer_RowIsMarkedAsHavingNoClan()
    {
        Given(Hero("drifter", clanId: "", isWanderer: true));

        Assert.IsFalse(Build().Wanderers[0].HasClan, "the planner routes on HasClan");
    }

    // ---------- Data hygiene ----------

    [TestMethod]
    public void PlaceholderHeroes_AreFilteredOut()
    {
        Given(
            Hero("p1", name: "Place Holder Lord", isClanLeader: true),
            Hero("p2", name: "placeholder dwarf", isClanLeader: true),
            Hero("real", name: "Dain", isClanLeader: true));

        var list = Build();

        CollectionAssert.AreEquivalent(new[] { "real" }, Ids(list.ClanLeaders));
    }

    [TestMethod]
    public void Notables_AreNeverOffered()
    {
        Given(Hero("merchant", isNotable: true, isClanLeader: true));

        Assert.AreEqual(0, Build().TotalCount);
    }

    [TestMethod]
    public void TheCurrentPlayerCharacter_IsNeverOffered()
    {
        Given(Hero("me", isMainHero: true, isClanLeader: true));

        Assert.AreEqual(0, Build().TotalCount);
    }

    [TestMethod]
    public void HeroesOfAnotherCulture_AreNeverOffered()
    {
        Given(Hero("foreigner", culture: "gondor", isClanLeader: true));

        Assert.AreEqual(0, Build().TotalCount, "the adapter may over-return; the service filters");
    }

    [TestMethod]
    public void ADuplicateHeroId_AppearsOnlyOnce()
    {
        Given(
            Hero("thane", clanId: "clan_b", isClanLeader: true),
            Hero("thane", clanId: "clan_b", isClanLeader: true));

        Assert.AreEqual(1, Build().ClanLeaders.Count);
    }

    // ---------- Lore-locked heroes ----------

    [TestMethod]
    public void LoreLockedHeroes_AreOfferedByDefault()
    {
        Given(Hero("sauron", isKingdomLeader: true, isClanLeader: true, isLoreLocked: true));

        CollectionAssert.AreEquivalent(new[] { "sauron" }, Ids(Build().RulingHouse),
            "on by default since #1067; the setting's hint carries the capture caveat");
    }

    [TestMethod]
    public void LoreLockedHeroes_AreHiddenWhenThePlayerTurnsThemOff()
    {
        Given(Hero("sauron", isKingdomLeader: true, isClanLeader: true, isLoreLocked: true));

        var list = Build(new PlayerSwitchPolicy(true, true, allowLoreLockedHeroes: false, false));

        Assert.AreEqual(0, list.TotalCount,
            "Patch76 defers to vanilla for MainHero, so a player-controlled dark lord loses capture immunity");
    }

    // ---------- Feature gating and empty states ----------

    [TestMethod]
    public void WhenTheFeatureIsDisabled_NothingIsOfferedAndTheAdapterIsNotEvenAsked()
    {
        Given(Hero("king", isKingdomLeader: true, isClanLeader: true));

        var list = _sut.BuildPickList(Culture, PlayerSwitchPolicy.Disabled);

        Assert.IsTrue(list.IsEmpty);
        _adapter.DidNotReceive().GetCandidates(Arg.Any<string>());
    }

    [TestMethod]
    public void ACultureWithNoEligibleHeroes_YieldsThreeEmptyNonNullGroups()
    {
        Given();

        var list = Build();

        Assert.IsNotNull(list.RulingHouse);
        Assert.IsNotNull(list.ClanLeaders);
        Assert.IsNotNull(list.Wanderers);
        Assert.IsTrue(list.IsEmpty, "empty groups must render their 'none' text, never a null binding");
    }

    [TestMethod]
    public void ANullOrEmptyCultureId_YieldsAnEmptyList()
    {
        Assert.IsTrue(_sut.BuildPickList("", PlayerSwitchPolicy.Default).IsEmpty);
        Assert.IsTrue(_sut.BuildPickList(null!, PlayerSwitchPolicy.Default).IsEmpty);
    }

    [TestMethod]
    public void RowsCarryTheRaceAndGenderThePreviewNeeds()
    {
        Given(Hero("dain", race: 3, isFemale: false, isClanLeader: true));

        var row = Build().ClanLeaders.Single();

        Assert.AreEqual(3, row.Race);
        Assert.IsFalse(row.IsFemale);
        Assert.IsTrue(row.IsLeader);
        Assert.IsTrue(row.HasClan);
    }

    // ---------- One hero picked on the faction screen (#704) ----------

    private HeroPickRow Find(string heroId, PlayerSwitchPolicy? policy = null)
        => _sut.FindTakeover(heroId, Culture, policy ?? PlayerSwitchPolicy.Default);

    [TestMethod]
    public void FindTakeover_AClanLeaderOfTheCulture_ReturnsTheRowTheHandoverPlansFrom()
    {
        Given(Hero("dain", name: "Dain", race: 3, isClanLeader: true));

        var row = Find("dain");

        Assert.AreEqual("dain", row.HeroId);
        Assert.AreEqual("Dain", row.Name, "the handover's toast names the hero");
        Assert.IsTrue(row.HasClan, "the planner takes over a hero with a clan");
        Assert.AreEqual(3, row.Race);
    }

    [TestMethod]
    public void FindTakeover_AClanMemberWhoLeadsNothing_IsTakenOverToo()
    {
        // The face-generator list offers only rulers, their family, clan leaders and wanderers; the
        // faction screen's named cards include members such as a lord's sister, and the handover
        // makes any taken-over member the clan's leader (#550).
        Given(Hero("eowyn", clanId: "clan_eomund", isFemale: true));

        Assert.AreEqual("eowyn", Find("eowyn").HeroId);
    }

    [TestMethod]
    public void FindTakeover_AClanlessWanderer_ReturnsARowWithoutAClan()
    {
        // #1067: a named card (Gimli, Aragorn) is an explicit pick, adopted into the player's new clan.
        // The planner routes HasClan false to AdoptIntoPlayerClan.
        Given(Hero("aragorn", name: "Aragorn", clanId: "", isWanderer: true));

        var row = Find("aragorn");

        Assert.AreEqual("aragorn", row.HeroId);
        Assert.IsFalse(row.HasClan);
    }

    [TestMethod]
    public void FindTakeover_AClanlessWandererWithWanderersExcluded_StillReturnsARow()
    {
        // The wanderer list is a browse list; a card is a named pick and does not need the setting.
        Given(Hero("aragorn", clanId: "", isWanderer: true));

        var row = Find("aragorn", new PlayerSwitchPolicy(true, includeWanderers: false, true, false));

        Assert.AreEqual("aragorn", row.HeroId);
    }

    [TestMethod]
    public void FindTakeover_AClanlessHeroWhoIsNoWanderer_IsNotTakenOver()
    {
        // Nothing marks such a hero as a pickable character; adoption is the wanderer path.
        Given(Hero("limbo", clanId: "", isWanderer: false));

        Assert.IsTrue(Find("limbo").IsEmpty);
    }

    [TestMethod]
    public void FindTakeover_AClanlessWandererWhoIsAChild_IsNotTakenOver()
    {
        Given(Hero("kid", clanId: "", isWanderer: true, isChild: true));

        Assert.IsTrue(Find("kid").IsEmpty);
    }

    // ---------- Across cultures (#1067) ----------

    [TestMethod]
    public void FindTakeover_AHeroOfAnotherCulture_IsFoundThroughHisOwnCulture()
    {
        // Haldir: a Mirkwood lord on the Lothlorien card.
        _adapter.CultureOf("haldir").Returns("mirkwood");
        _adapter.GetCandidates("mirkwood").Returns(new List<PickableHeroInfo>
        {
            Hero("haldir", name: "Haldir", culture: "mirkwood", clanId: "clan_mirkwood_3"),
        });

        var row = _sut.FindTakeover("haldir", Culture, PlayerSwitchPolicy.Default);

        Assert.AreEqual("haldir", row.HeroId);
        Assert.IsTrue(row.HasClan);
    }

    [TestMethod]
    public void FindTakeover_WhenTheCultureLookupReturnsNull_FallsBackToTheScreensCulture()
    {
        _adapter.CultureOf("dain").Returns((string?)null);
        Given(Hero("dain", isClanLeader: true));

        Assert.AreEqual("dain", Find("dain").HeroId);
    }

    [TestMethod]
    public void FindTakeover_WhenTheCultureLookupReturnsEmpty_FallsBackToTheScreensCulture()
    {
        _adapter.CultureOf("dain").Returns("");
        Given(Hero("dain", isClanLeader: true));

        Assert.AreEqual("dain", Find("dain").HeroId);
    }

    [TestMethod]
    public void FindTakeover_AHeroOfAnotherCultureStillMeetsEveryOtherRule()
    {
        _adapter.CultureOf("kid").Returns("mirkwood");
        _adapter.GetCandidates("mirkwood").Returns(new List<PickableHeroInfo>
        {
            Hero("kid", culture: "mirkwood", isChild: true),
        });

        Assert.IsTrue(Find("kid").IsEmpty);
    }

    [TestMethod]
    public void BuildPickList_StillFiltersByTheScreensCulture()
    {
        // Player Switcher's own panel keeps "culture must match": it never asks for a hero's own culture.
        Given(Hero("foreigner", culture: "mirkwood", isClanLeader: true));

        Assert.AreEqual(0, Build().TotalCount);
        _adapter.DidNotReceive().CultureOf(Arg.Any<string>());
    }

    [TestMethod]
    public void FindTakeover_AnIdNoLivingHeroOfTheCultureHas_IsNotTakenOver()
    {
        // A legend, a troop, a wanderer template, or a hero of another culture the adapter left out.
        Given(Hero("dain", isClanLeader: true));

        Assert.IsTrue(Find("taom_fui_thorin").IsEmpty);
    }

    [TestMethod]
    public void FindTakeover_AHeroOfAnotherCulture_IsNotTakenOver()
    {
        Given(Hero("foreigner", culture: "gondor", isClanLeader: true));

        Assert.IsTrue(Find("foreigner").IsEmpty, "the adapter may over-return; the service filters");
    }

    [TestMethod]
    public void FindTakeover_AChild_IsNotTakenOver()
    {
        Given(Hero("infant", isChild: true, isChildOfKingdomLeader: true));

        Assert.IsTrue(Find("infant").IsEmpty);
    }

    [TestMethod]
    public void FindTakeover_ANotable_IsNotTakenOver()
    {
        Given(Hero("merchant", isNotable: true, isClanLeader: true));

        Assert.IsTrue(Find("merchant").IsEmpty);
    }

    [TestMethod]
    public void FindTakeover_TheCurrentPlayerCharacter_IsNotTakenOver()
    {
        Given(Hero("me", isMainHero: true, isClanLeader: true));

        Assert.IsTrue(Find("me").IsEmpty);
    }

    [TestMethod]
    public void FindTakeover_APlaceholderHero_IsNotTakenOver()
    {
        Given(Hero("p1", name: "Place Holder Lord", isClanLeader: true));

        Assert.IsTrue(Find("p1").IsEmpty);
    }

    [TestMethod]
    public void FindTakeover_ALoreLockedHero_IsTakenOverUnlessThePlayerTurnedThemOff()
    {
        Given(Hero("sauron", isKingdomLeader: true, isClanLeader: true, isLoreLocked: true));

        Assert.AreEqual("sauron", Find("sauron").HeroId, "on by default since #1067");
        Assert.IsTrue(Find("sauron", new PlayerSwitchPolicy(true, true, allowLoreLockedHeroes: false, false)).IsEmpty);
    }

    [TestMethod]
    public void FindTakeover_WithPlayerSwitcherSwitchedOff_IsNotTakenOverAndTheAdapterIsNotAsked()
    {
        Given(Hero("dain", isClanLeader: true));

        Assert.IsTrue(Find("dain", PlayerSwitchPolicy.Disabled).IsEmpty);
        _adapter.DidNotReceive().GetCandidates(Arg.Any<string>());
    }

    [TestMethod]
    public void FindTakeover_AnEmptyHeroOrCulture_IsNotTakenOverAndTheAdapterIsNotAsked()
    {
        Assert.IsTrue(_sut.FindTakeover("", Culture, PlayerSwitchPolicy.Default).IsEmpty);
        Assert.IsTrue(_sut.FindTakeover(null!, Culture, PlayerSwitchPolicy.Default).IsEmpty);
        Assert.IsTrue(_sut.FindTakeover("dain", "", PlayerSwitchPolicy.Default).IsEmpty);
        _adapter.DidNotReceive().GetCandidates(Arg.Any<string>());
    }

    [TestMethod]
    public void FindTakeover_TheAdapterReturningNothing_IsNotTakenOver()
    {
        _adapter.GetCandidates(Culture).Returns((IReadOnlyList<PickableHeroInfo>)null!);

        Assert.IsTrue(Find("dain").IsEmpty);
    }
}
