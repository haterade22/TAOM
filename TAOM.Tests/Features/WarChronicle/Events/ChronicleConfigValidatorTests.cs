using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Events;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Tests.Features.WarChronicle.Events;

/// <summary>
/// <c>war_chronicle.json</c> validation (docs/features/war-chronicle.md "Validation rules"): one test per
/// rule. A bad event is skipped with a warning and the rest load; a bad effect row is skipped alone and
/// its event stays.
/// </summary>
[TestClass]
public class ChronicleConfigValidatorTests
{
    private readonly List<string> _warnings = new List<string>();

    private static JObject Narrative(string id = "fellowship", string anchor = "CampaignStart", int offset = 20) =>
        new JObject
        {
            ["id"] = id,
            ["kind"] = "Narrative",
            ["anchor"] = anchor,
            ["offsetDays"] = offset,
            ["title"] = "{=taom_wc_test_title}Title",
            ["outcomes"] = new JObject
            {
                ["occurred"] = new JObject
                {
                    ["text"] = "{=taom_wc_test_occurred}It happened.",
                    ["effects"] = new JArray(Effect("rivendell")),
                },
            },
        };

    private static JObject Siege(string id = "hornburg", string anchor = "Phase2", int offset = 5) =>
        new JObject
        {
            ["id"] = id,
            ["kind"] = "Siege",
            ["anchor"] = anchor,
            ["offsetDays"] = offset,
            ["windowDays"] = 20,
            ["target"] = "town_V2",
            ["attackers"] = new JArray("isengard", "empire"),
            ["defender"] = "vlandia",
            ["title"] = "{=taom_wc_test_siege}Siege",
            ["openText"] = "{=taom_wc_test_open}Open {TARGET} {DAYS}",
            ["lapsedText"] = "{=taom_wc_test_lapsed}Lapsed",
            ["outcomes"] = new JObject
            {
                ["held"] = new JObject { ["effects"] = new JArray(Effect("defender")) },
                ["fell"] = new JObject { ["effects"] = new JArray(Effect("attacker")) },
            },
        };

    private static JObject Effect(string target, string kind = "VolunteerRate", JToken? magnitude = null, int duration = 20) =>
        new JObject
        {
            ["target"] = target,
            ["kind"] = kind,
            ["magnitude"] = magnitude ?? 0.1,
            ["durationDays"] = duration,
        };

    private IReadOnlyList<ChronicleEventDefinition> Parse(params JObject[] events) =>
        ChronicleConfigValidator.Parse(new JObject { ["schemaVersion"] = 1, ["events"] = new JArray(events) }.ToString(), _warnings);

    private void AssertWarned(string fragment) =>
        Assert.IsTrue(_warnings.Any(w => w.Contains(fragment)), $"expected a warning containing '{fragment}', got:\n" + string.Join("\n", _warnings));

    [TestMethod]
    public void Parse_AValidNarrativeAndSiege_ReturnsBothTyped()
    {
        var events = Parse(Narrative(), Siege());

        Assert.AreEqual(0, _warnings.Count, string.Join("\n", _warnings));
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual(ChronicleEventKind.Narrative, events[0].Kind);
        Assert.AreEqual(20, events[0].OffsetDays);
        Assert.AreEqual(WarEffectKind.VolunteerRate, events[0].Occurred!.Effects[0].Kind);
        Assert.AreEqual(ChronicleEventKind.Siege, events[1].Kind);
        Assert.AreEqual(ChronicleAnchor.Phase2, events[1].Anchor);
        Assert.AreEqual("town_V2", events[1].TargetSettlementId);
        CollectionAssert.AreEqual(new[] { "isengard", "empire" }, events[1].Attackers.ToArray());
        Assert.AreEqual("vlandia", events[1].Defender);
        Assert.AreEqual(20, events[1].WindowDays);
        Assert.AreEqual("defender", events[1].Held!.Effects[0].Target);
    }

    [TestMethod]
    public void Parse_NotJson_ReturnsNothingAndWarns()
    {
        Assert.AreEqual(0, ChronicleConfigValidator.Parse("{ not json", _warnings).Count);
        AssertWarned("not readable JSON");
    }

    [DataTestMethod]
    [DataRow("2")]
    [DataRow("0")]
    [DataRow("\"1\"")]
    [DataRow("99999999999999999999")]
    public void Parse_UnknownSchemaVersion_ReturnsNothingAndWarns(string version)
    {
        var json = "{ \"schemaVersion\": " + version + ", \"events\": [" + Narrative() + "] }";

        Assert.AreEqual(0, ChronicleConfigValidator.Parse(json, _warnings).Count);
        AssertWarned("schemaVersion");
    }

    [TestMethod]
    public void Parse_NoEventsArray_ReturnsNothingAndWarns()
    {
        Assert.AreEqual(0, ChronicleConfigValidator.Parse("{ \"schemaVersion\": 1 }", _warnings).Count);
        AssertWarned("events");
    }

    [TestMethod]
    public void Parse_MoreThanTheEventCap_KeepsTheFirstOnesAndWarns()
    {
        var many = Enumerable.Range(0, ChronicleConfigValidator.MaxEvents + 3).Select(i => Narrative("e" + i)).ToArray();

        var events = Parse(many);

        Assert.AreEqual(ChronicleConfigValidator.MaxEvents, events.Count);
        Assert.AreEqual("e0", events[0].Id);
        AssertWarned("at most");
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    public void Parse_BlankId_SkipsTheEvent(string id)
    {
        var bad = Narrative();
        bad["id"] = id;

        Assert.AreEqual(0, Parse(bad).Count);
        AssertWarned("id");
    }

    [TestMethod]
    public void Parse_DuplicateId_KeepsTheFirstAndSkipsTheSecond()
    {
        var events = Parse(Narrative("a", offset: 1), Narrative("a", offset: 2));

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(1, events[0].OffsetDays);
        AssertWarned("duplicate");
    }

    [TestMethod]
    public void Parse_UnknownKind_SkipsTheEvent()
    {
        var bad = Narrative();
        bad["kind"] = "Battle";

        Assert.AreEqual(0, Parse(bad).Count);
        AssertWarned("kind");
    }

    [TestMethod]
    public void Parse_UnknownAnchor_SkipsTheEvent()
    {
        Assert.AreEqual(0, Parse(Narrative(anchor: "Phase3")).Count);
        AssertWarned("anchor");
    }

    [TestMethod]
    public void Parse_EventAnchorOnALaterEvent_SkipsIt_SoNoCycleCanForm()
    {
        var later = Narrative("later");
        var first = Narrative("first", anchor: "Event");
        first["anchorEvent"] = "later";

        var events = Parse(first, later);

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("later", events[0].Id);
        AssertWarned("earlier");
    }

    [TestMethod]
    public void Parse_EventAnchorOnItself_SkipsIt()
    {
        var self = Narrative("self", anchor: "Event");
        self["anchorEvent"] = "self";

        Assert.AreEqual(0, Parse(self).Count);
    }

    [TestMethod]
    public void Parse_EventAnchorOnASkippedEvent_SkipsIt()
    {
        var broken = Narrative("broken");
        broken["kind"] = "Nope";
        var child = Narrative("child", anchor: "Event");
        child["anchorEvent"] = "broken";

        Assert.AreEqual(0, Parse(broken, child).Count);
    }

    [TestMethod]
    public void Parse_EventAnchorWithoutAnAnchorEvent_SkipsIt()
    {
        Assert.AreEqual(0, Parse(Narrative("orphan", anchor: "Event")).Count);
        AssertWarned("anchorEvent");
    }

    [TestMethod]
    public void Parse_AnchorEventOnAPhaseAnchor_SkipsIt()
    {
        var odd = Narrative("odd", anchor: "Phase1");
        odd["anchorEvent"] = "x";

        Assert.AreEqual(0, Parse(odd).Count);
        AssertWarned("anchorEvent");
    }

    [TestMethod]
    public void Parse_EventAnchorOnAnEarlierEvent_ResolvesItsId()
    {
        var child = Narrative("child", anchor: "Event", offset: 2);
        child["anchorEvent"] = "hornburg";
        child["requiresOutcome"] = "Held";

        var events = Parse(Siege(), child);

        Assert.AreEqual(2, events.Count);
        Assert.AreEqual("hornburg", events[1].AnchorEventId);
        Assert.AreEqual(ChronicleEventState.Held, events[1].RequiresOutcome);
    }

    [TestMethod]
    public void Parse_UnknownRequiredOutcome_SkipsTheEvent()
    {
        var child = Narrative("child", anchor: "Event");
        child["anchorEvent"] = "hornburg";
        child["requiresOutcome"] = "Won";

        Assert.AreEqual(1, Parse(Siege(), child).Count);
        AssertWarned("requiresOutcome");
    }

    [DataTestMethod]
    [DataRow("Held")]
    [DataRow("Fell")]
    [DataRow("Lapsed")]
    public void Parse_SiegeOutcomeRequiredOfANarrativeAnchor_SkipsTheEvent(string outcome)
    {
        var child = Narrative("child", anchor: "Event");
        child["anchorEvent"] = "fellowship";
        child["requiresOutcome"] = outcome;

        Assert.AreEqual(1, Parse(Narrative(), child).Count);
        AssertWarned("requiresOutcome");
    }

    [DataTestMethod]
    [DataRow("Occurred")]
    [DataRow("Skipped")]
    [DataRow("Pending")]
    public void Parse_NonSiegeOutcomeRequiredOfASiegeAnchor_SkipsTheEvent(string outcome)
    {
        var child = Narrative("child", anchor: "Event");
        child["anchorEvent"] = "hornburg";
        child["requiresOutcome"] = outcome;

        Assert.AreEqual(1, Parse(Siege(), child).Count);
    }

    [TestMethod]
    public void Parse_RequiredOutcomeWithoutAnEventAnchor_SkipsTheEvent()
    {
        var odd = Narrative();
        odd["requiresOutcome"] = "Occurred";

        Assert.AreEqual(0, Parse(odd).Count);
        AssertWarned("requiresOutcome");
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(366)]
    public void Parse_OffsetOutsideZeroTo365_SkipsTheEvent(int offset)
    {
        Assert.AreEqual(0, Parse(Narrative(offset: offset)).Count);
        AssertWarned("offsetDays");
    }

    [TestMethod]
    public void Parse_MissingOffset_SkipsTheEvent()
    {
        var bad = Narrative();
        bad.Remove("offsetDays");

        Assert.AreEqual(0, Parse(bad).Count);
        AssertWarned("offsetDays");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(365)]
    public void Parse_OffsetAtTheBounds_IsKept(int offset)
    {
        Assert.AreEqual(1, Parse(Narrative(offset: offset)).Count);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(61)]
    public void Parse_SiegeWindowOutsideOneTo60_SkipsTheEvent(int window)
    {
        var bad = Siege();
        bad["windowDays"] = window;

        Assert.AreEqual(0, Parse(bad).Count);
        AssertWarned("windowDays");
    }

    [TestMethod]
    public void Parse_SiegeWithoutAWindow_SkipsTheEvent()
    {
        var bad = Siege();
        bad.Remove("windowDays");

        Assert.AreEqual(0, Parse(bad).Count);
        AssertWarned("windowDays");
    }

    [TestMethod]
    public void Parse_SiegeWithNoAttackers_SkipsTheEvent()
    {
        var bad = Siege();
        bad["attackers"] = new JArray(" ", "");

        Assert.AreEqual(0, Parse(bad).Count);
        AssertWarned("attackers");
    }

    [TestMethod]
    public void Parse_SiegeWhoseAttackersIncludeTheDefender_SkipsTheEvent()
    {
        var bad = Siege();
        bad["attackers"] = new JArray("isengard", "vlandia");

        Assert.AreEqual(0, Parse(bad).Count);
        AssertWarned("defender");
    }

    [TestMethod]
    public void Parse_SiegeWithoutATarget_SkipsTheEvent()
    {
        var bad = Siege();
        bad.Remove("target");

        Assert.AreEqual(0, Parse(bad).Count);
        AssertWarned("target");
    }

    [TestMethod]
    public void Parse_SiegeWithoutADefender_SkipsTheEvent()
    {
        var bad = Siege();
        bad["defender"] = "";

        Assert.AreEqual(0, Parse(bad).Count);
        AssertWarned("defender");
    }

    [DataTestMethod]
    [DataRow("CampaignStart")]
    [DataRow("Phase1")]
    public void Parse_SiegeOpeningBeforeTheFullWar_SkipsTheEvent(string anchor)
    {
        Assert.AreEqual(0, Parse(Siege(anchor: anchor)).Count);
        AssertWarned("Phase 2");
    }

    [TestMethod]
    public void Parse_SiegeAnchoredOnANarrativeBeforeTheFullWar_SkipsTheEvent()
    {
        var siege = Siege(anchor: "Event");
        siege["anchorEvent"] = "fellowship";

        Assert.AreEqual(1, Parse(Narrative(anchor: "Phase1"), siege).Count);
        AssertWarned("Phase 2");
    }

    [TestMethod]
    public void Parse_SiegeAnchoredOnAFullWarSiege_IsKept()
    {
        var gondor = Siege("gondor", anchor: "Event", offset: 3);
        gondor["anchorEvent"] = "hornburg";

        Assert.AreEqual(2, Parse(Siege(), gondor).Count);
    }

    [TestMethod]
    public void Parse_MissingTitle_SkipsTheEvent()
    {
        var bad = Narrative();
        bad.Remove("title");

        Assert.AreEqual(0, Parse(bad).Count);
        AssertWarned("title");
    }

    [TestMethod]
    public void Parse_NarrativeWithoutAnOccurredOutcome_SkipsTheEvent()
    {
        var bad = Narrative();
        bad["outcomes"] = new JObject();

        Assert.AreEqual(0, Parse(bad).Count);
        AssertWarned("occurred");
    }

    [TestMethod]
    public void Parse_AnOutcomeTheKindNeverReaches_IsIgnoredWithAWarning()
    {
        var odd = Narrative();
        ((JObject)odd["outcomes"]!)["held"] = new JObject { ["effects"] = new JArray(Effect("rivendell")) };
        var siege = Siege();
        ((JObject)siege["outcomes"]!)["lapsed"] = new JObject { ["effects"] = new JArray(Effect("defender")) };

        var events = Parse(odd, siege);

        Assert.AreEqual(2, events.Count);
        Assert.IsNull(events[0].Held);
        AssertWarned("'held'");
        AssertWarned("'lapsed'");
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-Infinity")]
    [DataRow("0")]
    [DataRow("0.26")]
    [DataRow("-0.26")]
    public void Parse_EffectMagnitudeNotFiniteZeroOrPastAQuarter_SkipsTheRowOnly(string magnitude)
    {
        var json = Narrative().ToString().Replace("\"magnitude\": 0.1", "\"magnitude\": " + magnitude);

        var events = ChronicleConfigValidator.Parse("{ \"schemaVersion\": 1, \"events\": [" + json + "] }", _warnings);

        Assert.AreEqual(1, events.Count, "the event stays");
        Assert.AreEqual(0, events[0].Occurred!.Effects.Count);
        AssertWarned("magnitude");
    }

    [DataTestMethod]
    [DataRow(0.25)]
    [DataRow(-0.25)]
    public void Parse_EffectMagnitudeAtAQuarter_IsKept(double magnitude)
    {
        var ok = Narrative();
        ok["outcomes"]!["occurred"]!["effects"] = new JArray(Effect("rivendell", magnitude: magnitude));

        Assert.AreEqual(1, Parse(ok)[0].Occurred!.Effects.Count);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(61)]
    public void Parse_EffectDurationOutsideOneTo60_SkipsTheRow(int duration)
    {
        var bad = Narrative();
        bad["outcomes"]!["occurred"]!["effects"] = new JArray(Effect("rivendell", duration: duration));

        Assert.AreEqual(0, Parse(bad)[0].Occurred!.Effects.Count);
        AssertWarned("durationDays");
    }

    [TestMethod]
    public void Parse_EffectOfAnUnknownKind_SkipsTheRow()
    {
        var bad = Narrative();
        bad["outcomes"]!["occurred"]!["effects"] = new JArray(Effect("rivendell", kind: "MilitiaGrowth"));

        Assert.AreEqual(0, Parse(bad)[0].Occurred!.Effects.Count);
        AssertWarned("kind");
    }

    [TestMethod]
    public void Parse_EffectWithABlankTarget_SkipsTheRow()
    {
        var bad = Narrative();
        bad["outcomes"]!["occurred"]!["effects"] = new JArray(Effect(" "));

        Assert.AreEqual(0, Parse(bad)[0].Occurred!.Effects.Count);
        AssertWarned("target");
    }

    [DataTestMethod]
    [DataRow("attacker")]
    [DataRow("defender")]
    public void Parse_SiegeSideTargetOnANarrative_SkipsTheRow(string target)
    {
        var bad = Narrative();
        bad["outcomes"]!["occurred"]!["effects"] = new JArray(Effect(target));

        Assert.AreEqual(0, Parse(bad)[0].Occurred!.Effects.Count);
        AssertWarned("siege");
    }

    [TestMethod]
    public void Parse_AValidMomentumAward_IsKept()
    {
        var ok = Siege();
        ok["outcomes"]!["held"]!["momentum"] = new JObject { ["side"] = "free", ["units"] = 50 };

        var award = Parse(ok)[0].Held!.Momentum;

        Assert.IsNotNull(award);
        Assert.AreEqual(MomentumSide.Free, award!.Side);
        Assert.AreEqual(50, award.Units);
    }

    [DataTestMethod]
    [DataRow("attacker", 50)]
    [DataRow("gondor", 50)]
    [DataRow("free", 0)]
    [DataRow("evil", ChronicleConfigValidator.MaxMomentumUnits + 1)]
    public void Parse_AMomentumAwardWithABadSideOrUnits_DropsTheAwardOnly(string side, int units)
    {
        var bad = Siege();
        bad["outcomes"]!["held"]!["momentum"] = new JObject { ["side"] = side, ["units"] = units };

        var events = Parse(bad);

        Assert.AreEqual(1, events.Count);
        Assert.IsNull(events[0].Held!.Momentum);
        Assert.AreEqual(1, events[0].Held!.Effects.Count);
        AssertWarned("momentum");
    }

    [TestMethod]
    public void Parse_ANullEventEntry_IsSkippedWithAWarning()
    {
        var json = "{ \"schemaVersion\": 1, \"events\": [ null, " + Narrative() + " ] }";

        Assert.AreEqual(1, ChronicleConfigValidator.Parse(json, _warnings).Count);
        AssertWarned("event 0");
    }

    // ---- Names only (EnumNames): numbers, signs, padding tricks and comma lists never parse --------------

    [DataTestMethod]
    [DataRow("1")]
    [DataRow("+1")]
    [DataRow(" 1")]
    [DataRow("Narrative,Siege")]
    public void Parse_AKindThatIsNotAName_SkipsTheEvent(string kind)
    {
        var bad = Siege();
        bad["kind"] = kind;

        Assert.AreEqual(0, Parse(bad).Count);
        AssertWarned("kind");
    }

    [DataTestMethod]
    [DataRow("1")]
    [DataRow("+1")]
    [DataRow(" 1")]
    [DataRow("CampaignStart,Phase1")]
    public void Parse_AnAnchorThatIsNotAName_SkipsTheEvent(string anchor)
    {
        Assert.AreEqual(0, Parse(Narrative(anchor: anchor)).Count);
        AssertWarned("is not CampaignStart");
    }

    [DataTestMethod]
    [DataRow("+3")]
    [DataRow(" 3")]
    [DataRow("Active,Occurred")]
    public void Parse_ARequiredOutcomeThatIsNotAName_SkipsTheEvent(string requires)
    {
        var child = Narrative("ents", "Event", 2);
        child["anchorEvent"] = "hornburg";
        child["requiresOutcome"] = requires;

        var events = Parse(Siege(), child);

        Assert.AreEqual(1, events.Count);
        AssertWarned("requiresOutcome");
    }

    [DataTestMethod]
    [DataRow("+1")]
    [DataRow(" 1")]
    [DataRow("VolunteerRate,PrisonerEscape")]
    public void Parse_AnEffectKindThatIsNotAName_SkipsTheRowOnly(string kind)
    {
        var bad = Narrative();
        bad["outcomes"]!["occurred"]!["effects"] = new JArray(Effect("rivendell", kind: kind));

        var events = Parse(bad);

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(0, events[0].Occurred!.Effects.Count);
        AssertWarned("kind");
    }

    [TestMethod]
    public void Parse_ANameInAnotherCaseWithPadding_Loads()
    {
        var ok = Siege();
        ok["kind"] = " siege ";

        Assert.AreEqual(ChronicleEventKind.Siege, Parse(ok).Single().Kind);
    }

    // ---- Unknown keys and wrong types warn instead of dropping silently -------------------------------

    [DataTestMethod]
    [DataRow("event")]
    [DataRow("outcome")]
    [DataRow("effect")]
    [DataRow("momentum")]
    public void Parse_AnUnknownKey_WarnsAndKeepsTheEvent(string level)
    {
        var e = Siege();
        var held = (JObject)e["outcomes"]!["held"]!;
        held["momentum"] = new JObject { ["side"] = "free", ["units"] = 100 };
        switch (level)
        {
            case "event": e["RequiresOutcome"] = "Held"; break;
            case "outcome": held["momentm"] = 1; break;
            case "effect": ((JObject)held["effects"]![0]!)["duration"] = 5; break;
            default: ((JObject)held["momentum"]!)["amount"] = 5; break;
        }

        var events = Parse(e);

        Assert.AreEqual(1, events.Count);
        AssertWarned("is not a known key");
    }

    [TestMethod]
    public void Parse_AnUnderscoreCommentKey_DoesNotWarn()
    {
        var e = Narrative();
        e["_comment"] = "a note for the author";

        Parse(e);

        Assert.AreEqual(0, _warnings.Count, string.Join("\n", _warnings));
    }

    [DataTestMethod]
    [DataRow("null")]
    [DataRow("5")]
    [DataRow("\"free\"")]
    [DataRow("[]")]
    public void Parse_AMomentumThatIsNotAnObject_DropsTheAwardOnly(string raw)
    {
        var bad = Siege();
        bad["outcomes"]!["held"]!["momentum"] = JToken.Parse(raw);

        var events = Parse(bad);

        Assert.AreEqual(1, events.Count);
        Assert.IsNull(events[0].Held!.Momentum);
        Assert.AreEqual(1, events[0].Held!.Effects.Count);
        AssertWarned("momentum");
    }

    [TestMethod]
    public void Parse_EffectsThatAreNotAnArray_Warns()
    {
        var bad = Narrative();
        bad["outcomes"]!["occurred"]!["effects"] = new JObject();

        Assert.AreEqual(1, Parse(bad).Count);
        AssertWarned("effects");
    }

    [TestMethod]
    public void Parse_AnImage_IsTheEventWindowsSprite_AndABlankOneIsNone()
    {
        var narrative = Narrative();
        narrative["image"] = "taom_event_khazaddum";
        var siege = Siege();
        siege["image"] = "  ";

        var events = Parse(narrative, siege, Narrative("breaking", offset: 30));

        Assert.AreEqual(0, _warnings.Count, string.Join("\n", _warnings));
        Assert.AreEqual("taom_event_khazaddum", events[0].Image);
        Assert.IsNull(events[1].Image);
        Assert.IsNull(events[2].Image);
    }

    [TestMethod]
    public void Parse_AnImageThatIsNotAString_WarnsAndLoadsTheEventWithoutOne()
    {
        var bad = Narrative();
        bad["image"] = 3;

        var events = Parse(bad);

        Assert.AreEqual(1, events.Count);
        Assert.IsNull(events[0].Image);
        AssertWarned("image");
    }

    [TestMethod]
    public void Parse_ATextThatIsNotAString_Warns()
    {
        var bad = Siege();
        bad["lapsedText"] = 3;

        Assert.AreEqual(1, Parse(bad).Count);
        AssertWarned("lapsedText");
    }

    [TestMethod]
    public void Parse_ARequiredOutcomeThatIsNotAString_SkipsTheEvent()
    {
        var child = Narrative("ents", "Event", 2);
        child["anchorEvent"] = "hornburg";
        child["requiresOutcome"] = 3;

        Assert.AreEqual(1, Parse(Siege(), child).Count);
        AssertWarned("requiresOutcome");
    }

    [TestMethod]
    public void Parse_ASiegeOutcomesThatIsNotAnObject_Warns()
    {
        var bad = Siege();
        bad["outcomes"] = new JArray();

        Assert.AreEqual(1, Parse(bad).Count);
        AssertWarned("outcomes");
    }
}
