using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Domain;

// The counters behind the race-ability log lines and the taom.race_abilities command: what fired, how
// often, and what each ability actually changed in the fighting. Written from any thread.

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class RaceAbilityTelemetryTests
{
    private RaceAbilityTelemetry _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new RaceAbilityTelemetry();

    [TestMethod]
    public void Get_NothingCounted_IsZero() => Assert.AreEqual(0, _sut.Get("berserk", RaceAbilityStat.Waves));

    [TestMethod]
    public void Add_CountsPerAbilityAndStat()
    {
        _sut.Add("berserk", RaceAbilityStat.Waves);
        _sut.Add("berserk", RaceAbilityStat.Waves);
        _sut.Add("berserk", RaceAbilityStat.BonusDamage, 12);
        _sut.Add("stand_fast", RaceAbilityStat.Waves);

        Assert.AreEqual(2, _sut.Get("berserk", RaceAbilityStat.Waves));
        Assert.AreEqual(12, _sut.Get("berserk", RaceAbilityStat.BonusDamage));
        Assert.AreEqual(1, _sut.Get("stand_fast", RaceAbilityStat.Waves));
    }

    [TestMethod]
    public void Add_FromManyThreads_LosesNothing()
    {
        Parallel.For(0, 10000, _ => _sut.Add("berserk", RaceAbilityStat.CrushesForced));

        Assert.AreEqual(10000, _sut.Get("berserk", RaceAbilityStat.CrushesForced));
    }

    [TestMethod]
    public void AddTrigger_CountsByKind()
    {
        _sut.AddTrigger("berserk", RaceAbilityTriggerKind.TookDamage);
        _sut.AddTrigger("berserk", RaceAbilityTriggerKind.TookDamage);
        _sut.AddTrigger("berserk", null);

        Assert.AreEqual(2, _sut.GetTrigger("berserk", RaceAbilityTriggerKind.TookDamage));
    }

    [TestMethod]
    public void Total_SumsEveryAbility()
    {
        _sut.Add("berserk", RaceAbilityStat.Activations, 4);
        _sut.Add("swiftness", RaceAbilityStat.Activations, 3);

        Assert.AreEqual(7, _sut.Total(RaceAbilityStat.Activations));
    }

    [TestMethod]
    public void Report_NothingCounted_SaysSo() =>
        StringAssert.Contains(_sut.Report(), "no race ability activity");

    [TestMethod]
    public void Report_NamesEachAbilityAndOnlyTheStatsItHas()
    {
        _sut.Add("berserk", RaceAbilityStat.TreesAttached, 12);
        _sut.Add("berserk", RaceAbilityStat.Waves, 3);
        _sut.Add("berserk", RaceAbilityStat.Activations, 9);
        _sut.Add("berserk", RaceAbilityStat.CrushesForced, 5);
        _sut.AddTrigger("berserk", RaceAbilityTriggerKind.TookDamage);

        var report = _sut.Report();

        StringAssert.Contains(report, "berserk:");
        StringAssert.Contains(report, "trees 12");
        StringAssert.Contains(report, "waves 3");
        StringAssert.Contains(report, "soldiers 9");
        StringAssert.Contains(report, "crush forced 5");
        StringAssert.Contains(report, "TookDamage 1");
        Assert.IsFalse(report.Contains("crush held"), "a zero stat is left out");
    }

    [TestMethod]
    public void Report_ListsAbilitiesInNameOrder()
    {
        _sut.Add("swiftness", RaceAbilityStat.Waves);
        _sut.Add("berserk", RaceAbilityStat.Waves);

        var report = _sut.Report();

        Assert.IsTrue(report.IndexOf("berserk:") < report.IndexOf("swiftness:"));
    }

    [TestMethod]
    public void Reset_ForgetsEverything()
    {
        _sut.Add("berserk", RaceAbilityStat.Waves);
        _sut.AddTrigger("berserk", RaceAbilityTriggerKind.Always);

        _sut.Reset();

        Assert.AreEqual(0, _sut.Get("berserk", RaceAbilityStat.Waves));
        Assert.AreEqual(0, _sut.GetTrigger("berserk", RaceAbilityTriggerKind.Always));
        StringAssert.Contains(_sut.Report(), "no race ability activity");
    }

    [TestMethod]
    public void Add_EmptyAbilityId_IsIgnored()
    {
        _sut.Add("", RaceAbilityStat.Waves);
        _sut.Add(null, RaceAbilityStat.Waves);

        Assert.AreEqual(0, _sut.Total(RaceAbilityStat.Waves));
    }
}
