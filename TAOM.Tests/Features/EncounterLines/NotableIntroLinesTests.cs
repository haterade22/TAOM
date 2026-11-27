using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.EncounterLines;
using TAOM.Features.EncounterLines.Models;
using TAOM.Features.Execution;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Messengers;

namespace TAOM.Tests.Features.EncounterLines;

/// <summary>
/// The notable introductions of Encounter Lines: a dwarf, elf or orc artisan, headman or rural notable introduces
/// themselves with a row of their race family instead of vanilla's human one. Every gate that leaves vanilla's line has
/// its own test (tests.md "Skip-Guard Exhaustion").
/// </summary>
[TestClass]
public class NotableIntroLinesTests
{
    private const string Seam = "TAOM_ENC_LINE";

    private IEncounterLinesSettingsProvider _settings = null!;
    private IEncounterLinesAdapter _adapter = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private IMessengerConversationState _messenger = null!;
    private EncounterLinesService _service = null!;

    private static EncounterGroupMap Map() => new(
        new Dictionary<string, string> { ["gondor"] = "west" },
        new Dictionary<string, string> { ["west"] = "man" },
        new Dictionary<string, string>
        {
            ["human"] = "man", ["dwarf"] = "dwarf", ["elf"] = "elf", ["orc"] = "orc", ["uruk"] = "orc", ["troll"] = "none",
        },
        new Dictionary<string, BanditClan>());

    private static NotableFacts Notable(string race, bool isNotable = true, string id = "notable_1") => new(isNotable, race, id);

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IEncounterLinesSettingsProvider>();
        _settings.Enabled.Returns(true);
        _adapter = Substitute.For<IEncounterLinesAdapter>();
        _adapter.ConversationNotable().Returns(Notable("dwarf"));
        _adapter.TrySetVariable(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var groups = Substitute.For<IEncounterGroupsConfigProvider>();
        groups.GetMap().Returns(Map());
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _messenger = Substitute.For<IMessengerConversationState>();
        _service = new EncounterLinesService(_settings, _adapter, groups, Substitute.For<IAlignmentService>(), Substitute.For<IRandomSource>(), _dedicated, _messenger);
    }

    private void AssertNothingSet() => _adapter.DidNotReceiveWithAnyArgs().TrySetVariable(default!, default!, default!);

    // --- The sentence table ---

    [TestMethod]
    public void TheTable_MapsTheThreeVanillaIntroductions_ToTheirTaomTexts()
    {
        CollectionAssert.AreEquivalent(
            new[] { "artisan_introduction", "headman_introduction", "rural_notable_introduction" },
            NotableIntroLines.TextOfSentence.Keys.ToList());
        Assert.AreEqual("str_taom_notable_artisan", NotableIntroLines.TextOfSentence["artisan_introduction"]);
        Assert.AreEqual("str_taom_notable_headman", NotableIntroLines.TextOfSentence["headman_introduction"]);
        Assert.AreEqual("str_taom_notable_rural", NotableIntroLines.TextOfSentence["rural_notable_introduction"]);
    }

    [TestMethod]
    public void TheTable_SharesNoSentenceWithTheLiegeTable()
    {
        Assert.AreEqual(0, NotableIntroLines.TextOfSentence.Keys.Intersect(LiegeLines.TextOfSentence.Keys).Count());
    }

    [TestMethod]
    public void TheSpokenFamilies_AreDwarfElfAndOrc_AndEachHasTwoOrMoreVariants()
    {
        CollectionAssert.AreEquivalent(new[] { "dwarf", "elf", "orc" }, NotableIntroLines.Families.ToList());
        Assert.IsTrue(NotableIntroLines.Variants >= 2);
    }

    // --- Who gets which row ---

    [DataTestMethod]
    [DataRow("artisan_introduction", "str_taom_notable_artisan")]
    [DataRow("headman_introduction", "str_taom_notable_headman")]
    [DataRow("rural_notable_introduction", "str_taom_notable_rural")]
    public void EachSentence_ForADwarf_SetsItsOwnTextUnderADwarfVariation(string sentence, string textId)
    {
        Assert.IsTrue(_service.TrySetSentenceLine(sentence));

        _adapter.Received(1).TrySetVariable(Seam, textId, Arg.Is<string>(v => v.StartsWith("dwarf_")));
    }

    [DataTestMethod]
    [DataRow("dwarf", "dwarf_")]
    [DataRow("elf", "elf_")]
    [DataRow("orc", "orc_")]
    [DataRow("uruk", "orc_")]
    public void TheVariation_IsTheRaceFamilyOfTheSpeaker(string race, string prefix)
    {
        _adapter.ConversationNotable().Returns(Notable(race));

        Assert.IsTrue(_service.TrySetSentenceLine("headman_introduction"));

        _adapter.Received(1).TrySetVariable(Seam, "str_taom_notable_headman", Arg.Is<string>(v => v.StartsWith(prefix)));
    }

    [TestMethod]
    public void TheVariation_IsOneOfTheShippedVariants_AndStaysTheSameForOneNotable()
    {
        var seen = new List<string>();
        _adapter.TrySetVariable(Seam, Arg.Any<string>(), Arg.Do<string>(seen.Add)).Returns(true);

        for (var i = 0; i < 3; i++)
            Assert.IsTrue(_service.TrySetSentenceLine("artisan_introduction"));

        Assert.AreEqual(1, seen.Distinct().Count(), "a notable repeats the same line");
        Assert.IsTrue(Enumerable.Range(1, NotableIntroLines.Variants).Select(n => "dwarf_" + n).Contains(seen[0]));
    }

    [TestMethod]
    public void DifferentNotables_SpreadOverEveryVariant()
    {
        var seen = new HashSet<string>();
        _adapter.TrySetVariable(Seam, Arg.Any<string>(), Arg.Do<string>(v => seen.Add(v))).Returns(true);

        for (var i = 0; i < 60; i++)
        {
            _adapter.ConversationNotable().Returns(Notable("elf", id: "Hero_" + i));
            _service.TrySetSentenceLine("headman_introduction");
        }

        Assert.AreEqual(NotableIntroLines.Variants, seen.Count);
    }

    // --- Who keeps vanilla ---

    [TestMethod]
    public void AHuman_KeepsVanilla()
    {
        _adapter.ConversationNotable().Returns(Notable("human"));

        Assert.IsFalse(_service.TrySetSentenceLine("artisan_introduction"));

        AssertNothingSet();
    }

    [TestMethod]
    public void ATroll_AndAnUnmappedRace_KeepVanilla()
    {
        foreach (var race in new[] { "troll", "warg", "" })
        {
            _adapter.ConversationNotable().Returns(Notable(race));

            Assert.IsFalse(_service.TrySetSentenceLine("rural_notable_introduction"), race);
        }

        AssertNothingSet();
    }

    [TestMethod]
    public void ASpeakerWhoIsNotANotable_KeepsVanilla()
    {
        _adapter.ConversationNotable().Returns(Notable("dwarf", isNotable: false));

        Assert.IsFalse(_service.TrySetSentenceLine("artisan_introduction"));

        AssertNothingSet();
    }

    [TestMethod]
    public void WithNoNotableInTheConversation_VanillaStands()
    {
        _adapter.ConversationNotable().Returns((NotableFacts?)null);

        Assert.IsFalse(_service.TrySetSentenceLine("headman_introduction"));

        AssertNothingSet();
    }

    [TestMethod]
    public void ALord_IsUnaffected_BecauseTheLordGateStaysSeparate()
    {
        // A lord is no notable: the adapter answers null or IsNotable false, and the sentences are the notables' own.
        _adapter.ConversationNotable().Returns(Notable("dwarf", isNotable: false));
        _adapter.ConversationLord().Returns(new LordFacts(true, false, false, "erebor", "dwarf", new SideIds("f", "erebor"), false, false, "lord_1"));

        Assert.IsFalse(_service.TrySetSentenceLine("headman_introduction"));
        AssertNothingSet();
    }

    [TestMethod]
    public void AMissingRow_KeepsVanilla()
    {
        _adapter.TrySetVariable(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        Assert.IsFalse(_service.TrySetSentenceLine("artisan_introduction"));
    }

    [TestMethod]
    public void TheSwitchOff_ADedicatedServer_AndAnOpenMessengerConversation_KeepVanilla()
    {
        _settings.Enabled.Returns(false);
        Assert.IsFalse(_service.TrySetSentenceLine("artisan_introduction"), "switch off");

        _settings.Enabled.Returns(true);
        _dedicated.IsDedicatedServer.Returns(true);
        Assert.IsFalse(_service.TrySetSentenceLine("artisan_introduction"), "dedicated server");

        _dedicated.IsDedicatedServer.Returns(false);
        _messenger.IsOpen.Returns(true);
        Assert.IsFalse(_service.TrySetSentenceLine("artisan_introduction"), "messenger conversation");

        AssertNothingSet();
    }

    [TestMethod]
    public void InASettlement_TheLineStillShows_BecauseNotablesTalkThere()
    {
        _adapter.InSettlement.Returns(true);

        Assert.IsTrue(_service.TrySetSentenceLine("artisan_introduction"));
    }

    [TestMethod]
    public void AnotherSentence_IsNotTheIntroductionsConcern()
    {
        Assert.IsFalse(_service.TrySetSentenceLine("merchant_introduction"));
        Assert.IsFalse(_service.TrySetSentenceLine(null));

        AssertNothingSet();
    }
}
