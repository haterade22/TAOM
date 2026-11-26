using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.EncounterLines;
using TAOM.Features.Execution;
using TAOM.Features.IncidentVoices;
using TAOM.Features.IncidentVoices.Models;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.IncidentVoices;

/// <summary>The pure rules of Incident Voices: which voice a side and people read, who may see an incident, which place is picked, how keys are built.</summary>
[TestClass]
public class IncidentRulesTests
{
    internal static EncounterGroupMap ShippedMap()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(RepoPath("Main", "_Module", "ModuleData"));
        return new EncounterGroupsConfigProvider(paths, Substitute.For<IModLogger>()).GetMap();
    }

    internal static IncidentRule Rule(string id = "incident_x", string[]? versions = null, int options = 3, IncidentRestrict? restrict = null,
        bool disable = false, bool disease = false, bool vanillaText = false, params PlaceFix[] places) =>
        new(id, "RESTRICT", versions ?? new[] { "all" }, options, new string[0], restrict ?? IncidentRestrict.None, disable, disease, vanillaText, places);

    private static IncidentRestrict Restrict(string[]? groups = null, string[]? families = null, string[]? versions = null) =>
        new(groups ?? new string[0], families ?? new string[0], versions ?? new string[0]);

    // Version picker

    [TestMethod]
    public void Pick_FreeReadsFree_EvilReadsShadow_WhateverThePeople()
    {
        Assert.AreEqual(IncidentVersion.Free, IncidentVersionPicker.Pick(FactionSide.Free, "orcs"));
        Assert.AreEqual(IncidentVersion.Shadow, IncidentVersionPicker.Pick(FactionSide.Evil, "west"));
        Assert.AreEqual(IncidentVersion.Shadow, IncidentVersionPicker.Pick(FactionSide.Evil, null));
    }

    [DataTestMethod]
    [DataRow("west", IncidentVersion.Free)]
    [DataRow("rohirrim", IncidentVersion.Free)]
    [DataRow("northmen", IncidentVersion.Free)]
    [DataRow("dunlendings", IncidentVersion.Free)]
    [DataRow("elves", IncidentVersion.Free)]
    [DataRow("dwarves", IncidentVersion.Free)]
    [DataRow("orcs", IncidentVersion.Shadow)]
    [DataRow("southrons", IncidentVersion.Shadow)]
    [DataRow("easterlings", IncidentVersion.Shadow)]
    [DataRow(null, IncidentVersion.Free)]
    public void Pick_NeutralIsDecidedByPeople(string? group, IncidentVersion expected) =>
        Assert.AreEqual(expected, IncidentVersionPicker.Pick(FactionSide.Neutral, group));

    [TestMethod]
    public void Pick_AnUnreadableSideIsFree()
        => Assert.AreEqual(IncidentVersion.Free, IncidentVersionPicker.Pick(null, "orcs"));

    [TestMethod]
    public void Pick_ASideChangeBetweenTwoCallsChangesTheVoice()
    {
        Assert.AreEqual(IncidentVersion.Free, IncidentVersionPicker.Pick(FactionSide.Free, "west"));
        Assert.AreEqual(IncidentVersion.Shadow, IncidentVersionPicker.Pick(FactionSide.Evil, "west"));
    }

    [TestMethod]
    public void Describe_ReadsFamilyFromRace_AndGroupFromCultureWhenTheyAgree()
    {
        var map = ShippedMap();

        var elf = IncidentVersionPicker.Describe(map, "lindon", "elf");
        var rohan = IncidentVersionPicker.Describe(map, "vlandia", "human");
        var orc = IncidentVersionPicker.Describe(map, "mordor", "orc");

        Assert.AreEqual(new PeopleFacts("elves", "elf"), elf);
        Assert.AreEqual(new PeopleFacts("rohirrim", "man"), rohan);
        Assert.AreEqual(new PeopleFacts("orcs", "orc"), orc);
    }

    [TestMethod]
    public void Describe_AHumanOfAnOrcCulture_IsMenWithNoGroup()
        => Assert.AreEqual(new PeopleFacts(null, "man"), IncidentVersionPicker.Describe(ShippedMap(), "mordor", "human"));

    [TestMethod]
    public void Describe_AnUnknownRace_FallsBackToTheCultureGroupsFamily()
        => Assert.AreEqual(new PeopleFacts("dwarves", "dwarf"), IncidentVersionPicker.Describe(ShippedMap(), "erebor", string.Empty));

    [TestMethod]
    public void Describe_NothingKnown_IsUnknown()
        => Assert.AreEqual(PeopleFacts.Unknown, IncidentVersionPicker.Describe(ShippedMap(), "nowhere", string.Empty));

    // Eligibility

    [TestMethod]
    public void Allowed_ADisabledIncidentIsNeverAllowed()
    {
        var rule = Rule(disable: true);

        foreach (var version in new[] { IncidentVersion.Free, IncidentVersion.Shadow })
            Assert.IsFalse(IncidentEligibility.Allowed(rule, version, new PeopleFacts("west", "man")));
    }

    [TestMethod]
    public void Allowed_AnUnrestrictedIncidentIsOpenToEveryone_EvenWithUnknownPeople()
    {
        Assert.IsTrue(IncidentEligibility.Allowed(Rule(), IncidentVersion.Shadow, PeopleFacts.Unknown));
        Assert.IsTrue(IncidentEligibility.Allowed(Rule(), IncidentVersion.Free, new PeopleFacts("orcs", "orc")));
    }

    [DataTestMethod]
    [DataRow("man", true)]
    [DataRow("elf", false)]
    [DataRow("dwarf", false)]
    [DataRow("orc", false)]
    [DataRow(null, false)]
    public void Allowed_MenOnly(string? family, bool expected) =>
        Assert.AreEqual(expected, IncidentEligibility.Allowed(Rule(restrict: Restrict(families: new[] { "man" })), IncidentVersion.Free, new PeopleFacts(null, family)));

    [DataTestMethod]
    [DataRow("man", true)]
    [DataRow("elf", true)]
    [DataRow("dwarf", false)]
    [DataRow("orc", false)]
    public void Allowed_HorseFolkAreMenAndElves(string family, bool expected) =>
        Assert.AreEqual(expected, IncidentEligibility.Allowed(Rule(restrict: Restrict(families: new[] { "man", "elf" })), IncidentVersion.Free, new PeopleFacts(null, family)));

    [DataTestMethod]
    [DataRow("easterlings", true)]
    [DataRow("southrons", true)]
    [DataRow("west", false)]
    [DataRow(null, false)]
    public void Allowed_ANomadIncidentNeedsAGroup(string? group, bool expected) =>
        Assert.AreEqual(expected, IncidentEligibility.Allowed(Rule(restrict: Restrict(groups: new[] { "easterlings", "southrons" })), IncidentVersion.Free, new PeopleFacts(group, "man")));

    [TestMethod]
    public void Allowed_ARestrictToTheFreeVoice_ClosesTheShadowVoice()
    {
        var rule = Rule(restrict: Restrict(versions: new[] { "free" }));
        var people = new PeopleFacts("west", "man");

        Assert.IsTrue(IncidentEligibility.Allowed(rule, IncidentVersion.Free, people));
        Assert.IsFalse(IncidentEligibility.Allowed(rule, IncidentVersion.Shadow, people));
    }

    [TestMethod]
    public void Allowed_ElvesNeverSeeADiseaseIncident_EveryoneElseDoes()
    {
        var rule = Rule(disease: true);

        Assert.IsFalse(IncidentEligibility.Allowed(rule, IncidentVersion.Free, new PeopleFacts("elves", "elf")));
        Assert.IsTrue(IncidentEligibility.Allowed(rule, IncidentVersion.Free, new PeopleFacts("west", "man")));
        Assert.IsTrue(IncidentEligibility.Allowed(rule, IncidentVersion.Shadow, new PeopleFacts("orcs", "orc")));
        Assert.IsTrue(IncidentEligibility.Allowed(rule, IncidentVersion.Free, new PeopleFacts("dwarves", "dwarf")));
    }

    [TestMethod]
    public void ClosedReason_IsNullWhenOpen_AndNamesTheReasonWhenClosed()
    {
        Assert.IsNull(IncidentEligibility.ClosedReason(Rule(), IncidentVersion.Free, PeopleFacts.Unknown));
        StringAssert.Contains(IncidentEligibility.ClosedReason(Rule(disable: true), IncidentVersion.Free, PeopleFacts.Unknown)!, "closed");
        StringAssert.Contains(IncidentEligibility.ClosedReason(Rule(disease: true), IncidentVersion.Free, new PeopleFacts("elves", "elf"))!, "Elves");
        StringAssert.Contains(IncidentEligibility.ClosedReason(Rule(restrict: Restrict(families: new[] { "man" })), IncidentVersion.Free, new PeopleFacts("dwarves", "dwarf"))!, "dwarf");
    }

    // Place picker

    private static PlaceFacts Place(string name, string faction = "", string culture = "") => new(name, new SideIds(faction, culture), culture);

    [TestMethod]
    public void PlacePicker_TheBestRankWins_AndNeverAPlaceOfTheOtherSide()
    {
        var own = Place("Pelargir", "empire_w");
        var people = Place("Edoras", "other", "vlandia");
        var enemy = Place("Barad", "empire_s");
        int Rank(PlaceFacts p) => p == own ? 1 : p == people ? 2 : 0;

        var all = IncidentPlacePicker.Pick(new[] { enemy, people, own }, Rank, 7, "incident_market_manipulation");
        var withoutOwn = IncidentPlacePicker.Pick(new[] { enemy, people }, Rank, 7, "incident_market_manipulation");

        Assert.AreSame(own, all);
        Assert.AreSame(people, withoutOwn);
    }

    [TestMethod]
    public void PlacePicker_NoPlaceOfTheSide_GivesNone()
        => Assert.IsNull(IncidentPlacePicker.Pick(new[] { Place("Barad") }, _ => 0, 1, "incident_x"));

    [TestMethod]
    public void PlacePicker_NoCandidates_GivesNone()
        => Assert.IsNull(IncidentPlacePicker.Pick(new PlaceFacts[0], _ => 1, 1, "incident_x"));

    [TestMethod]
    public void PlacePicker_TheSameSeedPicksTheSamePlace_InAnyInputOrder()
    {
        var places = Enumerable.Range(0, 12).Select(i => Place("Town" + i)).ToList();

        var first = IncidentPlacePicker.Pick(places, _ => 1, 4242, "incident_market_manipulation");
        var again = IncidentPlacePicker.Pick(Enumerable.Reverse(places).ToList(), _ => 1, 4242, "incident_market_manipulation");

        Assert.AreEqual(first!.Name, again!.Name);
    }

    [TestMethod]
    public void PlacePicker_ADifferentSeedCanPickAnotherPlace()
    {
        var places = Enumerable.Range(0, 12).Select(i => Place("Town" + i)).ToList();

        var names = Enumerable.Range(0, 24).Select(seed => IncidentPlacePicker.Pick(places, _ => 1, seed, "incident_x")!.Name).Distinct().Count();

        Assert.IsTrue(names > 1, "the seed moved no pick");
    }

    [TestMethod]
    public void PlacePicker_ANegativeSeedStaysInRange()
        => Assert.IsNotNull(IncidentPlacePicker.Pick(new[] { Place("A"), Place("B") }, _ => 1, long.MinValue + 1, "incident_x"));

    // Keys

    [TestMethod]
    public void Keys_FollowTheAgreedShape()
    {
        var variation = IncidentTextKeys.Variation("rooster_theft", IncidentTextKeys.All, IncidentTextKeys.OptionSlot(2));

        Assert.AreEqual("rooster_theft_all_opt2", variation);
        Assert.AreEqual("taom_inc_rooster_theft_all_opt2", IncidentTextKeys.Key(variation));
        Assert.AreEqual("voc_shadow_holy_place", IncidentTextKeys.VocabVariation(IncidentVersion.Shadow, "HOLY_PLACE"));
        Assert.AreEqual("taom_inc_voc_free_gods", IncidentTextKeys.Key(IncidentTextKeys.VocabVariation(IncidentVersion.Free, "GODS")));
    }

    [TestMethod]
    public void Slots_AreTitleDescriptionThenEachOption()
        => CollectionAssert.AreEqual(new[] { "title", "desc", "opt1", "opt2" }, IncidentTextKeys.Slots(2).ToArray());

    [DataTestMethod]
    [DataRow("opt1", 1)]
    [DataRow("opt12", 12)]
    [DataRow("opt0", 0)]
    [DataRow("title", 0)]
    [DataRow("optx", 0)]
    public void OptionNumber_ParsesOnlyOneBasedOptionSlots(string slot, int expected)
        => Assert.AreEqual(expected, IncidentTextKeys.OptionNumber(slot));

    [TestMethod]
    public void ShortId_DropsThePrefixOnce()
    {
        Assert.AreEqual("camp_fever", Rule("incident_camp_fever").ShortId);
        Assert.AreEqual("odd", Rule("odd").ShortId);
    }
}
