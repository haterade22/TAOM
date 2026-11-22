using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Autorun.Scenario;

namespace TAOM.Tests.Features.Autorun.Scenario;

[TestClass]
public class ScenarioPlanTests
{
    private const string ArmPath = @"E:\Temp\scenario-runs\arm.txt";
    private const string Head = "kind=campaign-new\nout=E:\\x\\out\n";

    private static ScenarioPlan Resolve(string? text, string? env = ArmPath, bool exists = true, string? fords = null, string? longDefeat = null,
        string? commandTrial = null)
    {
        Func<string, string?> getEnv = name => name switch
        {
            ScenarioPlan.EnvVar => env,
            "TAOM_FORDS_AUTORUN" => fords,
            "TAOM_LONGDEFEAT_AUTORUN" => longDefeat,
            "TAOM_COMMAND_TRIAL" => commandTrial,
            _ => null,
        };
        return ScenarioPlan.Resolve(getEnv, _ => exists, _ => text ?? throw new InvalidOperationException("read"));
    }

    // The host finds the culture in CharacterCreationContent.GetCultures(), which TAOM fills from cultures.json culture_id values
    // (CharacterCreationContentService). A kingdom id such as empire_w is not one, and the run would end creation_failed.
    [TestMethod]
    public void DefaultCulture_IsACharacterCreationCultureId()
    {
        var json = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/_Module/ModuleData/charactercreation/cultures.json");

        StringAssert.Contains(json, "\"culture_id\": \"" + ScenarioPlan.DefaultCulture + "\"");
    }

    [TestMethod]
    public void EnvVar_IsTaomAutorun()
    {
        Assert.AreEqual("TAOM_AUTORUN", ScenarioPlan.EnvVar);
    }

    [TestMethod]
    public void Resolve_EnvVariableUnset_IsUnarmedWithNoProblem()
    {
        var plan = Resolve(Head, env: null);

        Assert.IsFalse(plan.Armed);
        Assert.IsNull(plan.Problem);
    }

    [TestMethod]
    public void Resolve_EnvVariableUnsetButOtherAutorunSet_IsUnarmedWithNoProblem()
    {
        var plan = Resolve(Head, env: null, fords: "x");

        Assert.IsFalse(plan.Armed);
        Assert.IsNull(plan.Problem);
    }

    [TestMethod]
    public void Resolve_FordsVariableAlsoSet_IsUnarmedWithAProblem()
    {
        var plan = Resolve(Head, fords: @"E:\f.txt");

        Assert.IsFalse(plan.Armed);
        StringAssert.Contains(plan.Problem, "two autorun variables are set");
        StringAssert.Contains(plan.Problem, "TAOM_FORDS_AUTORUN");
    }

    [TestMethod]
    public void Resolve_LongDefeatVariableAlsoSet_IsUnarmedWithAProblem()
    {
        var plan = Resolve(Head, longDefeat: @"E:\l.txt");

        Assert.IsFalse(plan.Armed);
        StringAssert.Contains(plan.Problem, "two autorun variables are set");
        StringAssert.Contains(plan.Problem, "TAOM_LONGDEFEAT_AUTORUN");
    }

    // The Seat's field trial (CommandTrialAutostart) also starts a game from the main menu, on the same settle.
    [TestMethod]
    public void Resolve_CommandTrialVariableAlsoSet_IsUnarmedWithAProblem()
    {
        var plan = Resolve(Head, commandTrial: @"E:\t.txt");

        Assert.IsFalse(plan.Armed);
        StringAssert.Contains(plan.Problem, "TAOM_COMMAND_TRIAL");
    }

    [TestMethod]
    public void Resolve_MissingFile_IsUnarmedAndSaysWhy()
    {
        var plan = Resolve(Head, exists: false);

        Assert.IsFalse(plan.Armed);
        StringAssert.Contains(plan.Problem, "missing file");
    }

    [TestMethod]
    public void Resolve_UnreadableFile_IsUnarmedAndNamesTheException()
    {
        var plan = Resolve(null);

        Assert.IsFalse(plan.Armed);
        StringAssert.Contains(plan.Problem, "InvalidOperationException");
    }

    [TestMethod]
    public void Resolve_KindMissing_IsUnarmed()
    {
        var plan = Resolve("out=E:\\x\n");

        Assert.IsFalse(plan.Armed);
        StringAssert.Contains(plan.Problem, "kind=");
    }

    [TestMethod]
    public void Resolve_KindOtherThanCampaignNew_IsUnarmedNeverAFallback()
    {
        var plan = Resolve("kind=campaign-load\nout=E:\\x\n");

        Assert.IsFalse(plan.Armed);
        StringAssert.Contains(plan.Problem, "campaign-load");
        StringAssert.Contains(plan.Problem, "kind=campaign-new, kind=story-mission or kind=story-offer");
    }

    [TestMethod]
    public void Resolve_OutMissingRelativeOrNotAPath_IsUnarmed()
    {
        Assert.IsFalse(Resolve("kind=campaign-new\n").Armed);
        Assert.IsFalse(Resolve("kind=campaign-new\nout=relative\\folder\n").Armed);
        Assert.IsFalse(Resolve("kind=campaign-new\nout=a<b\n").Armed, "a character Path.IsPathRooted throws on is no folder");
        StringAssert.Contains(Resolve("kind=campaign-new\n").Problem, "out=");
    }

    [TestMethod]
    public void Resolve_OnlyRequiredKeys_UsesEveryDefault()
    {
        var plan = Resolve(Head);

        Assert.IsTrue(plan.Armed);
        Assert.IsNull(plan.Problem);
        Assert.AreEqual("campaign-new", plan.Kind);
        Assert.AreEqual(@"E:\x\out", plan.OutFolder);
        Assert.AreEqual("-", plan.Scenario);
        Assert.AreEqual("gondor", plan.Culture);
        Assert.AreEqual(3, plan.Days);
        Assert.AreEqual(15f, plan.Speed);
        Assert.AreEqual(600.0, plan.StallSeconds);
        Assert.AreEqual(3600.0, plan.WallLimitSeconds);
        Assert.IsFalse(plan.Quit);
        Assert.AreEqual(ArmPath, plan.ArmPath);
        Assert.AreEqual(0, plan.Warnings.Count);
    }

    [TestMethod]
    public void Resolve_EveryKey_IsRead()
    {
        var plan = Resolve(Head + "scenario=smoke\nculture=vlandia\ndays=7\nspeed=2.5\nstall_seconds=90\nwall_limit_seconds=120.5\nquit=1\nmission=x\nmission_limit_seconds=77.5\n");

        Assert.IsTrue(plan.Armed);
        Assert.AreEqual("smoke", plan.Scenario);
        Assert.AreEqual("vlandia", plan.Culture);
        Assert.AreEqual(7, plan.Days);
        Assert.AreEqual(2.5f, plan.Speed);
        Assert.AreEqual(90.0, plan.StallSeconds);
        Assert.AreEqual(120.5, plan.WallLimitSeconds);
        Assert.IsTrue(plan.Quit);
        Assert.AreEqual(77.5, plan.MissionLimitSeconds);
    }

    [TestMethod]
    public void Resolve_StoryMissionKind_ReadsMissionAndMissionLimit()
    {
        var plan = Resolve("kind=story-mission\nout=E:\\x\\out\nmission=helms_deep_defend\nmission_limit_seconds=900\n");

        Assert.IsTrue(plan.Armed);
        Assert.AreEqual("story-mission", plan.Kind);
        Assert.AreEqual("helms_deep_defend", plan.MissionId);
        Assert.AreEqual(900.0, plan.MissionLimitSeconds);
        Assert.AreEqual(0, plan.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("mission=\n")]
    [DataRow("mission=-\n")]
    public void Resolve_StoryMissionWithoutMission_IsUnarmed(string extra)
    {
        var plan = Resolve("kind=story-mission\nout=E:\\x\n" + extra);

        Assert.IsFalse(plan.Armed);
        StringAssert.Contains(plan.Problem, "needs mission=<story mission id> for kind=story-mission");
    }

    [TestMethod]
    public void Resolve_Defaults_AreNoMissionAndTwentyMinutes()
    {
        var plan = Resolve(Head);

        Assert.AreEqual("-", plan.MissionId);
        Assert.AreEqual(1200.0, plan.MissionLimitSeconds);
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("0")]
    [DataRow("-5")]
    [DataRow("soon")]
    public void Resolve_BadMissionLimit_WarnsAndKeepsTheDefault(string value)
    {
        var plan = Resolve("kind=story-mission\nout=E:\\x\nmission=a\nmission_limit_seconds=" + value + "\n");

        Assert.IsTrue(plan.Armed);
        Assert.AreEqual(1200.0, plan.MissionLimitSeconds);
        Assert.AreEqual(1, plan.Warnings.Count);
    }

    [TestMethod]
    public void Resolve_MissionKeyUnderCampaignNew_IsIgnoredWithoutAWarning()
    {
        var plan = Resolve(Head + "mission=helms_deep_defend\n");

        Assert.IsTrue(plan.Armed);
        Assert.AreEqual("-", plan.MissionId);
        Assert.AreEqual(0, plan.Warnings.Count);
    }

    [TestMethod]
    public void Resolve_BomCommentsBlankLinesCrlfAndKeyCase_AreTolerated()
    {
        var plan = Resolve("\uFEFF# a comment\r\n\r\nKIND = campaign-new\r\nOut=E:\\x\r\nDays=5\r\n");

        Assert.IsTrue(plan.Armed);
        Assert.AreEqual(5, plan.Days);
        Assert.AreEqual(0, plan.Warnings.Count);
    }

    [TestMethod]
    public void Resolve_UnknownKeyAndLineWithoutKey_AreWarningsNotProblems()
    {
        var plan = Resolve(Head + "bogus=1\njust words\n=nokey\n");

        Assert.IsTrue(plan.Armed);
        Assert.IsNull(plan.Problem);
        Assert.AreEqual(3, plan.Warnings.Count);
        StringAssert.Contains(plan.Warnings[0], "bogus");
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("1001")]
    [DataRow("-3")]
    [DataRow("2.5")]
    [DataRow("many")]
    [DataRow("")]
    public void Resolve_BadDays_WarnsAndKeepsTheDefault(string value)
    {
        var plan = Resolve(Head + "days=" + value + "\n");

        Assert.IsTrue(plan.Armed);
        Assert.AreEqual(3, plan.Days);
        Assert.AreEqual(1, plan.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow("1", 1)]
    [DataRow("1000", 1000)]
    public void Resolve_DaysAtTheEdges_AreKept(string value, int expected)
    {
        Assert.AreEqual(expected, Resolve(Head + "days=" + value + "\n").Days);
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-Infinity")]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("30.01")]
    [DataRow("fast")]
    public void Resolve_BadSpeed_WarnsAndKeepsTheDefault(string value)
    {
        var plan = Resolve(Head + "speed=" + value + "\n");

        Assert.IsTrue(plan.Armed);
        Assert.AreEqual(15f, plan.Speed);
        Assert.AreEqual(1, plan.Warnings.Count);
    }

    [TestMethod]
    public void Resolve_SpeedAtTheUpperEdge_IsKept()
    {
        Assert.AreEqual(30f, Resolve(Head + "speed=30\n").Speed);
        Assert.AreEqual(0.5f, Resolve(Head + "speed=0.5\n").Speed);
    }

    [DataTestMethod]
    [DataRow("stall_seconds", "NaN")]
    [DataRow("stall_seconds", "Infinity")]
    [DataRow("stall_seconds", "0")]
    [DataRow("stall_seconds", "-5")]
    [DataRow("stall_seconds", "soon")]
    [DataRow("wall_limit_seconds", "NaN")]
    [DataRow("wall_limit_seconds", "Infinity")]
    [DataRow("wall_limit_seconds", "0")]
    [DataRow("wall_limit_seconds", "-5")]
    [DataRow("wall_limit_seconds", "soon")]
    public void Resolve_BadSeconds_WarnsAndKeepsTheDefault(string key, string value)
    {
        var plan = Resolve(Head + key + "=" + value + "\n");

        Assert.IsTrue(plan.Armed);
        Assert.AreEqual(600.0, plan.StallSeconds);
        Assert.AreEqual(3600.0, plan.WallLimitSeconds);
        Assert.AreEqual(1, plan.Warnings.Count);
    }

    [TestMethod]
    public void Resolve_QuitFlag_ParsesLikeFords()
    {
        Assert.IsTrue(Resolve(Head + "quit=true\n").Quit);
        Assert.IsFalse(Resolve(Head + "quit=0\n").Quit);

        var bad = Resolve(Head + "quit=2\n");
        Assert.IsFalse(bad.Quit);
        Assert.AreEqual(1, bad.Warnings.Count);
    }

    private const string OfferHead = "kind=story-offer\nout=E:\\x\\out\n";

    [TestMethod]
    public void Resolve_StoryOfferKind_ReadsEventChoiceAndAfterRows()
    {
        var plan = Resolve(OfferHead + "event=hornburg\nchoice=Attack\ncommand=taom.chronicle_open hornburg confirm\nafter=taom.chronicle_status\nafter=taom.b 2\n");

        Assert.IsTrue(plan.Armed, plan.Problem);
        Assert.AreEqual("story-offer", plan.Kind);
        Assert.AreEqual("hornburg", plan.EventId);
        Assert.AreEqual("attack", plan.Choice);
        CollectionAssert.AreEqual(new[] { "taom.chronicle_status", "taom.b 2" }, plan.AfterCommands.ToArray());
        CollectionAssert.AreEqual(new[] { "taom.chronicle_open hornburg confirm" }, plan.Commands.ToArray());
        Assert.AreEqual("-", plan.MissionId);
        Assert.AreEqual(0, plan.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow("choice=defend\n", "event=<id>")]
    [DataRow("event=\nchoice=defend\n", "event=<id>")]
    [DataRow("event=hornburg\n", "choice=defend, choice=attack or choice=fate")]
    [DataRow("event=hornburg\nchoice=run\n", "choice=defend, choice=attack or choice=fate")]
    public void Resolve_StoryOfferWithoutAnEventOrAKnownChoice_IsUnarmedAndSaysWhich(string extra, string needs)
    {
        var plan = Resolve(OfferHead + extra);

        Assert.IsFalse(plan.Armed);
        StringAssert.Contains(plan.Problem, needs);
    }

    [TestMethod]
    public void Resolve_OfferKeysUnderOtherKinds_AreIgnoredWithoutAWarning()
    {
        var plan = Resolve(Head + "event=hornburg\nchoice=defend\nafter=taom.x\n");

        Assert.IsTrue(plan.Armed);
        Assert.AreEqual("-", plan.EventId);
        Assert.AreEqual("-", plan.Choice);
        Assert.AreEqual(0, plan.AfterCommands.Count);
        Assert.AreEqual(0, plan.Warnings.Count);
    }

    [TestMethod]
    public void Describe_NamesTheOfferEventAndChoice()
    {
        var text = Resolve(OfferHead + "event=hornburg\nchoice=fate\nafter=taom.x\n").Describe();

        StringAssert.Contains(text, "event=hornburg");
        StringAssert.Contains(text, "choice=fate");
        StringAssert.Contains(text, "after=1");
    }

    [TestMethod]
    public void Describe_NamesEveryValue()
    {
        var text = Resolve(Head + "scenario=smoke\nquit=1\ncommand=taom.a\ncommand=taom.b 2\n").Describe();

        foreach (var part in new[] { "kind=campaign-new", "scenario=smoke", "mission=-", "mission_limit_seconds=1200", "culture=gondor", "days=3", "speed=15", "stall_seconds=600", "wall_limit_seconds=3600", "quit=1", "commands=2", "out=E:\\x\\out", "arm=" + ArmPath })
            StringAssert.Contains(text, part);
    }

    [TestMethod]
    public void Resolve_StoryOffer_OpenByDateIsAFlag_OffByDefault()
    {
        const string head = "kind=story-offer\nout=E:\\x\\out\nevent=hornburg\nchoice=defend\n";
        ScenarioPlan Plan(string text) => ScenarioPlan.Resolve(n => n == ScenarioPlan.EnvVar ? @"E:\arm.txt" : null, _ => true, _ => text);

        Assert.IsFalse(Plan(head).OpenByDate);
        Assert.IsTrue(Plan(head + "open_by_date=1\n").OpenByDate);
        StringAssert.Contains(Plan(head + "open_by_date=1\n").Describe(), " open_by_date=1 ");
        Assert.IsFalse(Plan("kind=campaign-new\nout=E:\\x\\out\nopen_by_date=1\n").OpenByDate, "only the story-offer kind reads it");
    }
}
