using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.EncounterLines;
using TAOM.Features.EncounterLines.Models;
using TAOM.Features.Execution;
using TAOM.Features.IncidentVoices;
using TAOM.Features.IncidentVoices.Models;

namespace TAOM.Tests.Features.IncidentVoices;

/// <summary>The service end to end over fakes: gates, voices, place fixes, text lookup and its fallbacks, and every reason to stay vanilla.</summary>
[TestClass]
public class IncidentVoiceServiceTests
{
    private IIncidentCatalogProvider _catalogProvider = null!;
    private IIncidentVoiceAdapter _adapter = null!;
    private IAlignmentService _alignment = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private ICoopPresenceProvider _coop = null!;
    private Dictionary<string, string> _texts = null!;
    private Dictionary<string, IReadOnlyList<PlaceFacts>> _places = null!;
    private IncidentVoiceService _sut = null!;
    private List<IncidentRule> _rules = null!;

    [TestInitialize]
    public void Setup()
    {
        _rules = new List<IncidentRule>();
        _texts = new Dictionary<string, string>();
        _places = new Dictionary<string, IReadOnlyList<PlaceFacts>>();
        _catalogProvider = Substitute.For<IIncidentCatalogProvider>();
        _adapter = Substitute.For<IIncidentVoiceAdapter>();
        _alignment = Substitute.For<IAlignmentService>();
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _coop = Substitute.For<ICoopPresenceProvider>();
        var groups = Substitute.For<IEncounterGroupsConfigProvider>();
        var map = IncidentRulesTests.ShippedMap();
        groups.GetMap().Returns(map);

        _adapter.Text(Arg.Any<string>()).Returns(c => _texts.TryGetValue(c.Arg<string>(), out var t) ? t : null);
        _adapter.Places(Arg.Any<string>()).Returns(c => _places.TryGetValue(c.Arg<string>(), out var p) ? p : new PlaceFacts[0]);
        _adapter.IncidentSeed.Returns(99L);
        _alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>()).Returns(c => c.ArgAt<string>(0) switch
        {
            "empire_w" => FactionSide.Free,
            "empire_s" => FactionSide.Evil,
            _ => FactionSide.Neutral,
        });
        Player("gondor", "human", "empire_w");
        Catalog();
        _sut = new IncidentVoiceService(_catalogProvider, _adapter, groups, _alignment, _dedicated, _coop);
    }

    private void Player(string culture, string race, string faction) =>
        _adapter.Player().Returns(new PlayerFacts(culture, race, new SideIds(faction, culture)));

    private void Catalog() =>
        _catalogProvider.GetCatalog().Returns(new IncidentCatalog(_rules.ToDictionary(r => r.Id), new[] { "GODS", "COIN" }));

    private void Add(IncidentRule rule)
    {
        _rules.Add(rule);
        Catalog();
    }

    private static FakeIncident Incident(string id = "incident_x", int options = 2) => new(id, options);

    private sealed class FakeIncident : IIncidentTexts
    {
        public FakeIncident(string id, int options)
        {
            Id = id;
            OptionCount = options;
        }

        public string Id { get; }
        public int OptionCount { get; }
        public Dictionary<string, string?> Slots { get; } = new();
        public Dictionary<string, string> Variables { get; } = new();
        public Dictionary<string, string> Places { get; } = new();

        public void Set(string slot, string? rawText) => Slots[slot] = rawText;
        public void SetVariable(string name, string rawText) => Variables[name] = rawText;
        public void SetPlace(string name, string value) => Places[name] = value;
    }

    private void Text(string variation, string raw) => _texts[variation] = raw;

    // Vanilla stays

    [TestMethod]
    public void Review_VanillaSaidNo_StaysNo_AndTouchesNothing()
    {
        Add(IncidentRulesTests.Rule());
        var incident = Incident();

        Assert.IsFalse(_sut.Review(incident, false));

        Assert.AreEqual(0, incident.Slots.Count);
        _adapter.DidNotReceive().Player();
    }

    [TestMethod]
    public void Review_AnIncidentTheCatalogDoesNotList_KeepsVanillasAnswerAndText()
    {
        var incident = Incident("incident_unlisted");

        Assert.IsTrue(_sut.Review(incident, true));
        Assert.AreEqual(0, incident.Slots.Count);
    }

    [TestMethod]
    public void Review_AnEmptyCatalog_KeepsVanilla()
    {
        _catalogProvider.GetCatalog().Returns(IncidentCatalog.Empty);

        Assert.IsTrue(_sut.Review(Incident(), true));
    }

    [TestMethod]
    public void Review_OnADedicatedServer_KeepsVanilla()
    {
        Add(IncidentRulesTests.Rule(disable: true));
        _dedicated.IsDedicatedServer.Returns(true);

        Assert.IsTrue(_sut.Review(Incident(), true));
    }

    [TestMethod]
    public void Review_InACoopSession_KeepsVanilla()
    {
        Add(IncidentRulesTests.Rule(disable: true));
        _coop.IsCoopActive.Returns(true);

        Assert.IsTrue(_sut.Review(Incident(), true));
    }

    [TestMethod]
    public void Review_NoPlayer_KeepsVanilla()
    {
        Add(IncidentRulesTests.Rule(disable: true));
        _adapter.Player().Returns((PlayerFacts?)null);

        Assert.IsTrue(_sut.Review(Incident(), true));
    }

    // Gates

    [TestMethod]
    public void Review_ADisabledIncident_IsClosed_WithoutWritingText()
    {
        Add(IncidentRulesTests.Rule(disable: true));
        var incident = Incident();

        Assert.IsFalse(_sut.Review(incident, true));
        Assert.AreEqual(0, incident.Slots.Count);
    }

    [TestMethod]
    public void Review_ADiseaseIncident_IsClosedForAnElf_AndOpenForMen()
    {
        Add(IncidentRulesTests.Rule(disease: true));

        Player("lindon", "elf", "empire_w");
        Assert.IsFalse(_sut.Review(Incident(), true));

        Player("gondor", "human", "empire_w");
        Assert.IsTrue(_sut.Review(Incident(), true));
    }

    [TestMethod]
    public void Review_AMenOnlyIncident_IsClosedForAnOrc()
    {
        Add(IncidentRulesTests.Rule(restrict: new IncidentRestrict(new string[0], new[] { "man" }, new string[0])));

        Player("mordor", "orc", "empire_s");

        Assert.IsFalse(_sut.Review(Incident(), true));
    }

    [TestMethod]
    public void Review_AFreeOnlyIncident_ClosesForAShadowPlayer_AndForANeutralOrc()
    {
        Add(IncidentRulesTests.Rule(restrict: new IncidentRestrict(new string[0], new string[0], new[] { "free" })));

        Player("gondor", "human", "empire_w");
        Assert.IsTrue(_sut.Review(Incident(), true));
        Player("mordor", "orc", "empire_s");
        Assert.IsFalse(_sut.Review(Incident(), true));
        Player("mordor", "orc", "khand");
        Assert.IsFalse(_sut.Review(Incident(), true), "a Neutral orc reads the Shadow voice");
        Player("sturgia", "human", "khand");
        Assert.IsTrue(_sut.Review(Incident(), true), "a Neutral Northman reads the Free voice");
    }

    // Texts

    [TestMethod]
    public void Review_OneTextIncident_WritesTheAllTexts_ToEverySlot_AndTheVocabularyForTheVoice()
    {
        Add(IncidentRulesTests.Rule(options: 2));
        Text("x_all_title", "{=taom_inc_x_all_title}Title");
        Text("x_all_desc", "{=taom_inc_x_all_desc}Desc");
        Text("x_all_opt1", "{=taom_inc_x_all_opt1}One");
        Text("x_all_opt2", "{=taom_inc_x_all_opt2}Two");
        Text("voc_free_gods", "{=taom_inc_voc_free_gods}the Valar");
        var incident = Incident();

        Assert.IsTrue(_sut.Review(incident, true));

        Assert.AreEqual("{=taom_inc_x_all_title}Title", incident.Slots["title"]);
        Assert.AreEqual("{=taom_inc_x_all_opt2}Two", incident.Slots["opt2"]);
        Assert.AreEqual("{=taom_inc_voc_free_gods}the Valar", incident.Variables["GODS"]);
        Assert.IsFalse(incident.Variables.ContainsKey("COIN"), "a vocabulary word with no row is not set");
    }

    // Vocabulary by people

    private string GodsFor(string culture, string race, string faction, string? realmCulture = null)
    {
        Add(IncidentRulesTests.Rule(options: 2));
        Text("x_all_title", "{=taom_inc_x_all_title}Title");
        Text("voc_free_gods", "free-side");
        Text("voc_shadow_gods", "shadow-side");
        Text("voc_west_gods", "west");
        Text("voc_elves_gods", "elves");
        Text("voc_dwarves_gods", "dwarves");
        Text("voc_dunlendings_gods", "dunlendings");
        Text("voc_orcs_gods", "orcs");
        Text("voc_isengard_gods", "isengard");
        Text("voc_southrons_shadow_gods", "southrons-shadow");
        Text("voc_easterlings_shadow_gods", "easterlings-shadow");
        _adapter.Player().Returns(new PlayerFacts(culture, race, new SideIds(faction, realmCulture ?? culture)));
        var incident = Incident();
        Assert.IsTrue(_sut.Review(incident, true));
        return incident.Variables["GODS"];
    }

    [DataTestMethod]
    [DataRow("gondor", "human", "empire_w", "west")]
    [DataRow("rivendell", "elf", "empire_w", "elves")]
    [DataRow("erebor", "dwarf", "empire_w", "dwarves")]
    [DataRow("empire", "human", "khand", "dunlendings")]
    [DataRow("gundabad", "orc", "empire_s", "orcs")]
    [DataRow("mordor", "orc", "empire_s", "orcs")]
    [DataRow("isengard", "uruk_hai", "empire_s", "isengard")]
    [DataRow("aserai", "human", "empire_s", "southrons-shadow")]
    [DataRow("khuzait", "human", "empire_s", "easterlings-shadow")]
    public void Review_TheGodsFollowThePlayersPeople(string culture, string race, string faction, string expected)
        => Assert.AreEqual(expected, GodsFor(culture, race, faction));

    [DataTestMethod]
    [DataRow("vlandia", "human", "empire_w", "free-side")]
    [DataRow("sturgia", "human", "khand", "free-side")]
    [DataRow("aserai", "human", "empire_w", "free-side")]
    [DataRow("mordor", "human", "empire_s", "shadow-side")]
    public void Review_APeopleWithNoRow_FallsBackToTheSideRow(string culture, string race, string faction, string expected)
        => Assert.AreEqual(expected, GodsFor(culture, race, faction));

    [TestMethod]
    public void Review_AnIsengardRealm_ReadsTheWhiteHand_EvenForAPlayerOfAnotherCulture()
        => Assert.AreEqual("isengard", GodsFor("gundabad", "orc", "empire_s", "isengard"));

    [TestMethod]
    public void Review_AMissingRow_PutsVanillasSlotBack()
    {
        Add(IncidentRulesTests.Rule(options: 2));
        Text("x_all_title", "{=taom_inc_x_all_title}Title");
        var incident = Incident();

        _sut.Review(incident, true);

        Assert.IsNull(incident.Slots["desc"]);
        Assert.IsNull(incident.Slots["opt1"]);
        Assert.IsNull(incident.Slots["opt2"]);
    }

    [TestMethod]
    public void Review_NoRowAtAll_SetsNoVocabularyAndLeavesEverySlotVanilla()
    {
        Add(IncidentRulesTests.Rule(options: 2));
        Text("voc_free_gods", "{=taom_inc_voc_free_gods}the Valar");
        var incident = Incident();

        Assert.IsTrue(_sut.Review(incident, true));

        Assert.IsTrue(incident.Slots.Values.All(v => v == null));
        Assert.AreEqual(0, incident.Variables.Count);
    }

    [TestMethod]
    public void Review_NeverWritesAnOptionVanillaDoesNotHave()
    {
        Add(IncidentRulesTests.Rule(options: 5));
        var incident = Incident(options: 2);

        _sut.Review(incident, true);

        CollectionAssert.AreEquivalent(new[] { "title", "desc", "opt1", "opt2" }, incident.Slots.Keys.ToArray());
    }

    [TestMethod]
    public void Review_ASplitIncident_ReadsTheShadowSetForAShadowPlayer_AndTheFreeSetForAFreeOne()
    {
        Add(IncidentRulesTests.Rule(versions: new[] { "free", "shadow" }, options: 1));
        Text("x_free_title", "{=taom_inc_x_free_title}Free");
        Text("x_shadow_title", "{=taom_inc_x_shadow_title}Shadow");

        Player("mordor", "orc", "empire_s");
        var shadow = Incident(options: 1);
        _sut.Review(shadow, true);
        Player("gondor", "human", "empire_w");
        var free = Incident(options: 1);
        _sut.Review(free, true);

        Assert.AreEqual("{=taom_inc_x_shadow_title}Shadow", shadow.Slots["title"]);
        Assert.AreEqual("{=taom_inc_x_free_title}Free", free.Slots["title"]);
    }

    [TestMethod]
    public void Review_ASplitIncidentWithNoShadowTitle_FallsBackToTheWholeFreeSet()
    {
        Add(IncidentRulesTests.Rule(versions: new[] { "free", "shadow" }, options: 1));
        Text("x_free_title", "{=taom_inc_x_free_title}Free");
        Text("x_free_opt1", "{=taom_inc_x_free_opt1}FreeOne");
        Text("x_shadow_opt1", "{=taom_inc_x_shadow_opt1}ShadowOne");
        Player("mordor", "orc", "empire_s");
        var incident = Incident(options: 1);

        _sut.Review(incident, true);

        Assert.AreEqual("{=taom_inc_x_free_opt1}FreeOne", incident.Slots["opt1"], "a half-written Shadow set must not mix into the Free one");
    }

    [TestMethod]
    public void Review_AVanillaTextIncident_IsGatedButItsTextIsNeverWritten()
    {
        Add(IncidentRulesTests.Rule(vanillaText: true, restrict: new IncidentRestrict(new string[0], new[] { "man" }, new string[0])));
        Text("x_all_title", "{=taom_inc_x_all_title}Title");

        var open = Incident();
        Assert.IsTrue(_sut.Review(open, true));
        Assert.AreEqual(0, open.Slots.Count);

        Player("lindon", "elf", "empire_w");
        Assert.IsFalse(_sut.Review(Incident(), true));
    }

    // Places

    [TestMethod]
    public void Review_APlaceFix_SetsAPlaceOfThePlayersOwnRealm_NotTheOtherSides()
    {
        Add(IncidentRulesTests.Rule(options: 0, places: new PlaceFix("PLACE", "town")));
        _places["town"] = new[]
        {
            new PlaceFacts("Barad-dur", new SideIds("empire_s", "mordor"), "mordor"),
            new PlaceFacts("Minas Tirith", new SideIds("empire_w", "gondor"), "gondor"),
        };
        var incident = Incident(options: 0);

        Assert.IsTrue(_sut.Review(incident, true));

        Assert.AreEqual("Minas Tirith", incident.Places["PLACE"]);
    }

    [TestMethod]
    public void Review_APlaceFixWithNoPlaceOnTheSide_ClosesTheIncident()
    {
        Add(IncidentRulesTests.Rule(options: 0, places: new PlaceFix("PLACE", "town")));
        _places["town"] = new[] { new PlaceFacts("Barad-dur", new SideIds("empire_s", "mordor"), "mordor") };

        Assert.IsFalse(_sut.Review(Incident(options: 0), true));
    }

    [TestMethod]
    public void Review_APlaceFix_FallsBackToOwnPeopleThenSide()
    {
        Add(IncidentRulesTests.Rule(options: 0, places: new PlaceFix("PLACE", "town")));
        Player("gondor", "human", "founded_kingdom");
        _places["town"] = new[]
        {
            new PlaceFacts("Edoras", new SideIds("rohan", "vlandia"), "vlandia"),
            new PlaceFacts("Pelargir", new SideIds("gondor_k", "gondor"), "gondor"),
        };

        var incident = Incident(options: 0);
        _sut.Review(incident, true);

        Assert.AreEqual("Pelargir", incident.Places["PLACE"], "the player's own people come before another Free realm");
    }

    [TestMethod]
    public void Review_TwoCallsInOneRoll_AgreeOnThePlace()
    {
        Add(IncidentRulesTests.Rule(options: 0, places: new PlaceFix("PLACE", "town")));
        _places["town"] = Enumerable.Range(0, 9).Select(i => new PlaceFacts("Town" + i, new SideIds("empire_w", "gondor"), "gondor")).ToList();
        var first = Incident(options: 0);
        var second = Incident(options: 0);

        _sut.Review(first, true);
        _sut.Review(second, true);

        Assert.AreEqual(first.Places["PLACE"], second.Places["PLACE"]);
    }

    // Console

    [TestMethod]
    public void ResolveId_AcceptsTheShortAndTheFullId_AnyCase()
    {
        Add(IncidentRulesTests.Rule("incident_camp_fever"));

        Assert.AreEqual("incident_camp_fever", _sut.ResolveId("camp_fever"));
        Assert.AreEqual("incident_camp_fever", _sut.ResolveId("INCIDENT_Camp_Fever"));
        Assert.IsNull(_sut.ResolveId("nope"));
        Assert.IsNull(_sut.ResolveId(""));
    }

    [TestMethod]
    public void ClosedReason_NamesWhyAnElfCannotHaveCampFever()
    {
        Add(IncidentRulesTests.Rule("incident_camp_fever", disease: true));
        Player("lindon", "elf", "empire_w");

        StringAssert.Contains(_sut.ClosedReason("camp_fever")!, "Elves");
    }

    [TestMethod]
    public void ClosedReason_IsNullForAnOpenIncident_AndExplainsTheStandDown()
    {
        Add(IncidentRulesTests.Rule("incident_camp_fever", disease: true));

        Assert.IsNull(_sut.ClosedReason("camp_fever"));
        _coop.IsCoopActive.Returns(true);
        StringAssert.Contains(_sut.ClosedReason("camp_fever")!, "co-op");
    }

    [TestMethod]
    public void Command_UsesTheServiceAndTheAdapterQueue()
    {
        Add(IncidentRulesTests.Rule("incident_camp_fever", disease: true));
        _adapter.Queue("incident_camp_fever", null).Returns("queued");

        Assert.AreEqual("queued", IncidentCommands.Run(new List<string> { "camp_fever" }, _sut, _adapter));
        StringAssert.Contains(IncidentCommands.Run(new List<string> { "nope" }, _sut, _adapter), "does not list");
        Assert.AreEqual(IncidentCommands.Usage, IncidentCommands.Run(new List<string>(), _sut, _adapter));
    }
}
