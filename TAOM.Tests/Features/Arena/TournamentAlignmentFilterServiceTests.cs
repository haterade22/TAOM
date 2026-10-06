using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Arena;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.Arena;

/// <summary>
/// #744: a tournament keeps the other side out (no Mordor orcs at Minas Tirith). Runs over the real
/// <see cref="AlignmentService"/> on a stub of <c>execution/alignment.json</c>, so the Free/Evil/Neutral
/// reading under test is the production one. Patch69 only maps the roster and the host into primitives
/// and swaps the barred indices for the filler troop; that boundary is verified in game.
/// </summary>
[TestClass]
public class TournamentAlignmentFilterServiceTests
{
    // Kingdom and culture ids as alignment.json carries them. In TAOM, Dunland is `empire` (Evil) and
    // `battania` is Khand (Neutral); Khand's troops are `khuzait` (Evil), the culture it recruits from.
    private const string GondorKingdom = "empire_w";
    private const string MordorKingdom = "empire_s";
    private const string RohanKingdom = "vlandia";
    private const string Gondor = "gondor";
    private const string Mordor = "mordor";
    private const string Rohan = "vlandia";
    private const string Khand = "battania";
    private const string Rhun = "khuzait";
    private const string PlayerKingdom = "new_kingdom";
    private const string Umbar = "umbar";
    private const string Harad = "aserai";

    private ITournamentAlignmentSettingsProvider _settings = null!;
    private TournamentAlignmentFilterService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var config = Substitute.For<IAlignmentConfigProvider>();
        config.LoadAlignments().Returns(new Dictionary<string, string>
        {
            [GondorKingdom] = "free",
            [Gondor] = "free",
            [RohanKingdom] = "free",
            [MordorKingdom] = "evil",
            [Mordor] = "evil",
            [Khand] = "neutral",
            [Rhun] = "evil",
            [Umbar] = "neutral",
            [Harad] = "evil",
        });
        _settings = Substitute.For<ITournamentAlignmentSettingsProvider>();
        _settings.IsEnabled.Returns(true);
        _sut = new TournamentAlignmentFilterService(
            new AlignmentService(config, Substitute.For<IModLogger>()), _settings);
    }

    private static TournamentEntrant Troop(string? culture) =>
        new TournamentEntrant("troop_" + culture, "Troop", isHero: false, isFemale: false,
            raceName: null, clanId: null, hasMapFaction: false, hasCulture: culture != null,
            kingdomId: null, cultureId: culture);

    private static TournamentEntrant Hero(string? kingdom, string? culture, bool playerClan = false) =>
        new TournamentEntrant("hero_" + culture, "Hero", isHero: true, isFemale: false,
            raceName: null, clanId: "clan", hasMapFaction: true, hasCulture: culture != null,
            kingdomId: kingdom, cultureId: culture, isPlayerOrPlayerClan: playerClan);

    // A town held by its own kind: owner faction and culture match the town, and it recruits its own culture.
    private static TournamentHost Town(string? ownerFaction, string? culture) =>
        new TournamentHost(ownerFaction, culture, culture, culture, culture);

    private List<int> Barred(TournamentHost host, params TournamentEntrant[] roster) =>
        new List<int>(_sut.FindBarredIndices(roster, host));

    [TestMethod]
    public void FindBarredIndices_EvilTroopInFreeTown_IsBarred()
    {
        CollectionAssert.AreEqual(new[] { 1 }, Barred(Town(GondorKingdom, Gondor), Troop(Gondor), Troop(Mordor)));
    }

    [TestMethod]
    public void FindBarredIndices_EvilLordInFreeTown_IsBarred()
    {
        CollectionAssert.AreEqual(new[] { 0 }, Barred(Town(GondorKingdom, Gondor), Hero(MordorKingdom, Mordor)));
    }

    [TestMethod]
    public void FindBarredIndices_EvilWandererWithoutKingdom_IsBarredByCulture()
    {
        CollectionAssert.AreEqual(new[] { 0 }, Barred(Town(GondorKingdom, Gondor), Hero(null, Mordor)));
    }

    [TestMethod]
    public void FindBarredIndices_FreeLordInEvilTown_IsBarred()
    {
        CollectionAssert.AreEqual(new[] { 0 }, Barred(Town(MordorKingdom, Mordor), Hero(GondorKingdom, Gondor)));
    }

    [TestMethod]
    public void FindBarredIndices_AlliedFreeLordInFreeTown_IsAllowed()
    {
        Assert.AreEqual(0, Barred(Town(GondorKingdom, Gondor), Hero(RohanKingdom, Rohan)).Count);
    }

    [TestMethod]
    public void FindBarredIndices_NeutralHeroes_AreAllowedInFreeAndEvilTowns()
    {
        Assert.AreEqual(0, Barred(Town(GondorKingdom, Gondor), Hero(null, Khand), Hero(Khand, Khand)).Count);
        Assert.AreEqual(0, Barred(Town(MordorKingdom, Mordor), Hero(null, Khand), Hero(Khand, Khand)).Count);
    }

    [TestMethod]
    public void FindBarredIndices_NeutralTownOfItsOwnCulture_BarsNobody()
    {
        Assert.AreEqual(0, Barred(Town(Khand, Khand), Hero(MordorKingdom, Mordor), Hero(GondorKingdom, Gondor)).Count);
    }

    [TestMethod]
    public void FindBarredIndices_NeutralKingdomHoldsFreeTown_BarsNobody()
    {
        // Khand holds Minas Tirith: the owner decides, and a Neutral owner opposes nobody.
        var host = new TournamentHost(Khand, Khand, Gondor, Gondor, Gondor);
        Assert.AreEqual(0, Barred(host, Hero(MordorKingdom, Mordor), Troop(Mordor)).Count);
    }

    [TestMethod]
    public void FindBarredIndices_UnlistedPlayerKingdom_SidesByOwnerCulture()
    {
        // A Gondor-culture player's own kingdom holds Minas Morgul: Free, so Mordor's lord is out and the
        // player kingdom's Gondor vassals compete.
        var host = new TournamentHost(PlayerKingdom, Gondor, Mordor, Mordor, Mordor);
        CollectionAssert.AreEqual(new[] { 1 }, Barred(host, Hero(PlayerKingdom, Gondor), Hero(MordorKingdom, Mordor)));
    }

    [TestMethod]
    public void FindBarredIndices_KingdomlessClanOwner_SidesByOwnerCulture()
    {
        // The owner faction is the clan itself; its id is not in the table, so its culture decides.
        var host = new TournamentHost("clan_rebel_mordor", Mordor, Gondor, Gondor, Gondor);
        CollectionAssert.AreEqual(new[] { 0 }, Barred(host, Hero(RohanKingdom, Rohan)));
    }

    [TestMethod]
    public void FindBarredIndices_PlayerClanOrcCompanionInFreeTown_IsAllowed()
    {
        Assert.AreEqual(0, Barred(Town(GondorKingdom, Gondor), Hero(null, Mordor, playerClan: true)).Count);
    }

    [TestMethod]
    public void FindBarredIndices_HeroKingdomOutranksCulture_GondorBornLordOfMordorIsBarred()
    {
        // Rohan hosts, so the host-culture troop exemption cannot be what decides.
        CollectionAssert.AreEqual(new[] { 0 }, Barred(Town(RohanKingdom, Rohan), Hero(MordorKingdom, Gondor)));
    }

    [TestMethod]
    public void FindBarredIndices_CapturedTown_UsesOwnerSide()
    {
        // Mordor holds Minas Tirith: its orcs may compete and Rohan's lord may not.
        var host = new TournamentHost(MordorKingdom, Mordor, Gondor, Gondor, Gondor);
        CollectionAssert.AreEqual(new[] { 1 }, Barred(host, Hero(MordorKingdom, Mordor), Hero(RohanKingdom, Rohan)));
    }

    [TestMethod]
    public void FindBarredIndices_CapturedTown_TownCultureTroopIsAllowed()
    {
        // Vanilla pads, and Patch69 fills, from the town's own troops: those locals always compete, or the
        // filler would be barred and replaced by itself.
        var host = new TournamentHost(MordorKingdom, Mordor, Gondor, Gondor, Gondor);
        Assert.AreEqual(0, Barred(host, Troop(Gondor)).Count);
    }

    [TestMethod]
    public void FindBarredIndices_CapturedTown_KingdomlessHeroOfTownCulture_IsBarred()
    {
        // The local exemption is for troops only: a Gondor wanderer in Mordor-held Minas Tirith is judged by side.
        var host = new TournamentHost(MordorKingdom, Mordor, Gondor, Gondor, Gondor);
        CollectionAssert.AreEqual(new[] { 0 }, Barred(host, Hero(null, Gondor)));
    }

    [TestMethod]
    public void FindBarredIndices_BorrowedTroopTreeCulture_IsAllowed()
    {
        // Khand's towns recruit and pad from khuzait (Evil). Gondor holds one: those troops are the town's
        // own, while a khuzait hero is still judged by side.
        var host = new TournamentHost(GondorKingdom, Gondor, Khand, Rhun, Rhun);
        CollectionAssert.AreEqual(new[] { 1 }, Barred(host, Troop(Rhun), Hero(null, Rhun)));
    }

    [TestMethod]
    public void FindBarredIndices_BasicTroopCultureDiffersFromElite_BothAreAllowed()
    {
        // Umbar pads from its basic troop's tree (aserai, Evil) and fills with its elite troop (umbar). Gondor
        // holds the town: both sets of troops are the town's own.
        var host = new TournamentHost(GondorKingdom, Gondor, Umbar, Harad, Umbar);
        Assert.AreEqual(0, Barred(host, Troop(Harad), Troop(Umbar)).Count);
    }

    [TestMethod]
    public void FindBarredIndices_TownCultureTroop_IsAllowedWhenTroopTreesAreForeign()
    {
        // Only the town-culture arm matches: the town is Mordor-cultured, its troop trees are khuzait.
        var host = new TournamentHost(GondorKingdom, Gondor, Mordor, Rhun, Rhun);
        Assert.AreEqual(0, Barred(host, Troop(Mordor)).Count);
    }

    [TestMethod]
    public void FindBarredIndices_EliteTroopCulture_IsAllowedWhenItDiffersFromTownAndBasic()
    {
        // Only the elite arm matches: town and basic troop are Khand, the elite troop is khuzait.
        var host = new TournamentHost(GondorKingdom, Gondor, Khand, Khand, Rhun);
        Assert.AreEqual(0, Barred(host, Troop(Rhun)).Count);
    }

    [TestMethod]
    public void FindBarredIndices_EntrantWithoutCultureOrKingdom_FailsOpen()
    {
        Assert.AreEqual(0, Barred(Town(GondorKingdom, Gondor), Troop(null), Hero(null, null), default).Count);
    }

    [TestMethod]
    public void FindBarredIndices_HostWithoutOwnerOrCulture_BarsNobody()
    {
        Assert.AreEqual(0, Barred(default, Troop(Mordor), Hero(GondorKingdom, Gondor)).Count);
    }

    [TestMethod]
    public void FindBarredIndices_Disabled_BarsNobody()
    {
        _settings.IsEnabled.Returns(false);
        Assert.AreEqual(0, Barred(Town(GondorKingdom, Gondor), Troop(Mordor), Hero(MordorKingdom, Mordor)).Count);
    }

    [TestMethod]
    public void FindBarredIndices_NullRoster_ReturnsEmpty()
    {
        Assert.AreEqual(0, _sut.FindBarredIndices(null, Town(GondorKingdom, Gondor)).Count);
    }

    [TestMethod]
    public void FindBarredIndices_SeveralOffenders_ReportedInAscendingOrder()
    {
        CollectionAssert.AreEqual(new[] { 0, 2 },
            Barred(Town(GondorKingdom, Gondor), Troop(Mordor), Troop(Gondor), Hero(null, Mordor)));
    }

    [TestMethod]
    public void Describe_BarredEntrant_NamesEntrantAndBothSides()
    {
        var line = _sut.Describe(Hero(MordorKingdom, Mordor), Town(GondorKingdom, Gondor));

        StringAssert.Contains(line, "hero_mordor");
        StringAssert.Contains(line, "Evil");
        StringAssert.Contains(line, "Free");
    }

    [TestMethod]
    public void SettingsProvider_WithoutMcm_ReadsTheCompiledDefault()
    {
        // MCM's Instance is null in the test host, so the provider's fallback must equal the property's default.
        Assert.AreEqual(new TAOM.Features.TaomSettings().TournamentKeepEnemySidesOut,
            new TournamentAlignmentSettingsProvider().IsEnabled);
    }
}
