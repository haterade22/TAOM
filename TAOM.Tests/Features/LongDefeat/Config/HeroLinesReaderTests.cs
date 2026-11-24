using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>hero_lines.json (#1569): each bad item is dropped with one warning that names its key, and the rest of the file still loads.</summary>
[TestClass]
public class HeroLinesReaderTests
{
    private const string Good = "Heroes/legolas_a.ogg";

    private static (HeroLinePlan Plan, LongDefeatConfigScope Scope, RecordingLogger Log) Read(string? json, Func<string, string?>? resolve = null)
    {
        var log = new RecordingLogger();
        var scope = new LongDefeatConfigScope(log, "hero_lines.json");
        var plan = HeroLinesReader.Read(json, scope, resolve ?? (p => "C:/m/ModuleSounds/" + p));
        return (plan, scope, log);
    }

    private static string One(string take) => "{\"heroes\":{\"legolas\":[[\"" + take + "\"]]}}";

    private static void AssertDropped(string json, string key, Func<string, string?>? resolve = null)
    {
        var (plan, scope, log) = Read(json, resolve);

        Assert.AreEqual(0, plan.Lines.Count, "the hero is dropped");
        Assert.IsTrue(scope.Warnings >= 1, string.Join("\n", log.Lines));
        Assert.IsTrue(log.Lines.Any(l => l.Contains(key)), "a warning names " + key + ": " + string.Join("\n", log.Lines));
    }

    [TestMethod]
    public void Read_ValidFile_KeepsEveryLineAndTakeWithTheFullPath()
    {
        var json = "{\"heroes\":{\"legolas\":[[\"Heroes/a1.ogg\",\"Heroes/a2.ogg\"],[\"Heroes/b1.ogg\"]],\"gimli\":[[\"Heroes/g1.ogg\"]]}}";

        var (plan, scope, _) = Read(json);

        Assert.AreEqual(0, scope.Warnings);
        Assert.AreEqual(2, plan.Lines.Count);
        Assert.AreEqual(2, plan.Lines["legolas"].Count);
        CollectionAssert.AreEqual(new[] { "C:/m/ModuleSounds/Heroes/a1.ogg", "C:/m/ModuleSounds/Heroes/a2.ogg" }, plan.Lines["legolas"][0].ToArray());
        CollectionAssert.AreEqual(new[] { "C:/m/ModuleSounds/Heroes/b1.ogg" }, plan.Lines["legolas"][1].ToArray());
    }

    [TestMethod]
    public void Read_NullText_GivesNone()
    {
        var (plan, scope, _) = Read(null);

        Assert.AreEqual(0, plan.Lines.Count);
        Assert.AreEqual(0, scope.Warnings);
    }

    [TestMethod]
    public void Read_RootNotAnObject_GivesNoneWithOneWarning()
    {
        var (plan, scope, _) = Read("[1]");

        Assert.AreEqual(0, plan.Lines.Count);
        Assert.AreEqual(1, scope.Warnings);
    }

    [TestMethod]
    public void Read_NotJson_GivesNoneWithOneWarning()
    {
        var (plan, scope, _) = Read("{ not json");

        Assert.AreEqual(0, plan.Lines.Count);
        Assert.AreEqual(1, scope.Warnings);
    }

    [TestMethod]
    public void Read_HeroesNotAnObject_GivesNoneWithOneWarningNamingIt()
    {
        var (plan, scope, log) = Read("{\"heroes\":[1]}");

        Assert.AreEqual(0, plan.Lines.Count);
        Assert.AreEqual(1, scope.Warnings);
        Assert.IsTrue(log.Lines.Any(l => l.Contains("'heroes'")));
    }

    [TestMethod]
    public void Read_HeroKeyNotASetId_DropsThatHero() =>
        AssertDropped("{\"heroes\":{\"Legolas Green\":[[\"" + Good + "\"]]}}", "Legolas Green");

    [TestMethod]
    public void Read_HeroValueNotAnArray_DropsThatHero() =>
        AssertDropped("{\"heroes\":{\"legolas\":\"" + Good + "\"}}", "legolas");

    [TestMethod]
    public void Read_HeroValueEmptyArray_DropsThatHero() =>
        AssertDropped("{\"heroes\":{\"legolas\":[]}}", "legolas");

    [TestMethod]
    public void Read_LineNotAnArray_DropsTheLineAndSoTheHero() =>
        AssertDropped("{\"heroes\":{\"legolas\":[\"" + Good + "\"]}}", "legolas[0]");

    [TestMethod]
    public void Read_LineEmptyArray_DropsTheLineAndSoTheHero() =>
        AssertDropped("{\"heroes\":{\"legolas\":[[]]}}", "legolas[0]");

    [TestMethod]
    public void Read_TakeNotAString_IsDropped() =>
        AssertDropped("{\"heroes\":{\"legolas\":[[7]]}}", "legolas[0][0]");

    [TestMethod]
    public void Read_TakeRooted_IsDropped()
    {
        AssertDropped(One("/Heroes/a.ogg"), "legolas[0][0]");
        AssertDropped(One("\\\\\\\\Heroes\\\\a.ogg"), "legolas[0][0]");
    }

    [TestMethod]
    public void Read_TakeWithDriveLetter_IsDropped() =>
        AssertDropped(One("C:/Heroes/a.ogg"), "legolas[0][0]");

    [TestMethod]
    public void Read_TakeWithParentSegment_IsDropped()
    {
        AssertDropped(One("Heroes/../a.ogg"), "legolas[0][0]");
        AssertDropped(One("Heroes\\\\..\\\\a.ogg"), "legolas[0][0]");
    }

    [TestMethod]
    public void Read_TakeWithOutsideCharacter_IsDropped()
    {
        AssertDropped(One("Heroes/a b.ogg"), "legolas[0][0]");
        AssertDropped(One("Heroes/a;b.ogg"), "legolas[0][0]");
        AssertDropped(One("Heroes/\\u00e9.ogg"), "legolas[0][0]");
    }

    [TestMethod]
    public void Read_TakeLongerThan128_IsDropped() =>
        AssertDropped(One("Heroes/" + new string('a', 118) + ".ogg"), "legolas[0][0]");

    [TestMethod]
    public void Read_TakeOfExactly128_IsKept()
    {
        var path = "Heroes/" + new string('a', 117) + ".ogg";
        Assert.AreEqual(128, path.Length);

        var (plan, scope, _) = Read(One(path));

        Assert.AreEqual(0, scope.Warnings);
        Assert.AreEqual(1, plan.Lines.Count);
    }

    [TestMethod]
    public void Read_TakeNotOgg_IsDropped()
    {
        AssertDropped(One("Heroes/a.wav"), "legolas[0][0]");
        AssertDropped(One("Heroes/a.ogg.txt"), "legolas[0][0]");
    }

    [TestMethod]
    public void Read_TakeWithUpperCaseExtension_IsKept()
    {
        var (plan, scope, _) = Read(One("Heroes/a.OGG"));

        Assert.AreEqual(0, scope.Warnings);
        Assert.AreEqual(1, plan.Lines.Count);
    }

    [TestMethod]
    public void Read_TakeThatResolvesToNoFile_IsDropped() =>
        AssertDropped(One(Good), "legolas[0][0]", _ => null);

    [TestMethod]
    public void Read_OneBadTakeAmongGood_KeepsTheGoodOnes()
    {
        var json = "{\"heroes\":{\"legolas\":[[\"Heroes/a.ogg\",\"Heroes/missing.ogg\",\"Heroes/c.ogg\"]]}}";

        var (plan, scope, log) = Read(json, p => p.Contains("missing") ? null : "F/" + p);

        Assert.AreEqual(1, scope.Warnings);
        Assert.IsTrue(log.Lines.Any(l => l.Contains("legolas[0][1]")));
        CollectionAssert.AreEqual(new[] { "F/Heroes/a.ogg", "F/Heroes/c.ogg" }, plan.Lines["legolas"][0].ToArray());
    }

    [TestMethod]
    public void Read_LineWithNoValidTake_IsDropped_AndTheOtherLinesStay()
    {
        var json = "{\"heroes\":{\"legolas\":[[\"Heroes/missing.ogg\"],[\"Heroes/b.ogg\"]]}}";

        var (plan, scope, _) = Read(json, p => p.Contains("missing") ? null : "F/" + p);

        Assert.AreEqual(1, plan.Lines["legolas"].Count);
        Assert.AreEqual("F/Heroes/b.ogg", plan.Lines["legolas"][0][0]);
        Assert.IsTrue(scope.Warnings >= 1);
    }

    [TestMethod]
    public void Read_HeroWithNoValidLine_IsDropped_AndAnotherHeroStays()
    {
        var json = "{\"heroes\":{\"legolas\":[[\"Heroes/missing.ogg\"]],\"gimli\":[[\"Heroes/g.ogg\"]]}}";

        var (plan, _, _) = Read(json, p => p.Contains("missing") ? null : "F/" + p);

        CollectionAssert.AreEqual(new[] { "gimli" }, plan.Lines.Keys.ToArray());
    }
}
