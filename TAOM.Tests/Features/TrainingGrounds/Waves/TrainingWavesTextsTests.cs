using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TrainingGrounds.Waves.Hooks;

namespace TAOM.Tests.Features.TrainingGrounds.Waves;

/// <summary>The English of the master's lines: odds as a number, a text variable for each line, and the names the place and its keeper go by.</summary>
[TestClass]
public class TrainingWavesTextsTests
{
    private static readonly Regex Variable = new(@"\{([A-Z][A-Z0-9_]*)\}", RegexOptions.Compiled);

    /// <summary>Every <c>const string</c> of the texts class that carries a <c>{=KEY}</c> literal, by field name.</summary>
    private static Dictionary<string, string> Lines() =>
        typeof(TrainingWavesTexts).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string) && ((string)f.GetRawConstantValue()!).StartsWith("{=taom_tgw_"))
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!);

    [DataTestMethod]
    [DataRow(15, "1.5")]
    [DataRow(16, "1.6")]
    [DataRow(20, "2")]
    [DataRow(11, "1.1")]
    [DataRow(40, "4")]
    public void Odds_WritesTenthsAsADecimal_AndDropsAZeroTenth(int tenths, string expected)
    {
        Assert.AreEqual(expected, TrainingWavesTexts.Odds(tenths));
    }

    [TestMethod]
    public void EveryLine_IsAKeyedLiteral_WithItsOwnKey()
    {
        var lines = Lines();

        Assert.AreEqual(17, lines.Count, "13 dialogue lines (offer, rules, three stakes, not today, withdraw, go, go failed, withdrawn, cleared, lost, done) and 4 refusal texts");
        var keys = lines.Values.Select(t => Regex.Match(t, @"^\{=([A-Za-z0-9_]+)\}").Groups[1].Value).ToList();
        Assert.AreEqual(keys.Count, keys.Distinct().Count(), "two lines share a key");
        Assert.IsTrue(keys.All(k => k.StartsWith("taom_tgw_")));
    }

    [TestMethod]
    public void EveryTextVariable_BelongsToOneLine()
    {
        var owners = new Dictionary<string, List<string>>();
        foreach (var line in Lines())
            foreach (Match m in Variable.Matches(line.Value))
            {
                if (!owners.TryGetValue(m.Groups[1].Value, out var list))
                    owners[m.Groups[1].Value] = list = new List<string>();
                if (!list.Contains(line.Key))
                    list.Add(line.Key);
            }

        foreach (var pair in owners)
            Assert.AreEqual(1, pair.Value.Count, $"{pair.Key} is read by {string.Join(", ", pair.Value)}: a player option's text resolves after every condition has run, so a shared variable shows the last value on every line");
    }

    [TestMethod]
    public void EachStakeLine_UsesItsOwnAmountVariable()
    {
        var lines = Lines();
        var low = Variable.Match(lines["StakeLow"]).Groups[1].Value;
        var mid = Variable.Match(lines["StakeMid"]).Groups[1].Value;
        var high = Variable.Match(lines["StakeHigh"]).Groups[1].Value;

        Assert.AreEqual(3, new[] { low, mid, high }.Distinct().Count());
        Assert.AreEqual(low, TrainingWavesTexts.StakeLowAmount);
        Assert.AreEqual(mid, TrainingWavesTexts.StakeMidAmount);
        Assert.AreEqual(high, TrainingWavesTexts.StakeHighAmount);
    }

    [TestMethod]
    public void EveryVariableNameTheLinesSet_IsInATextAndEveryTextVariableIsNamedByAConstant()
    {
        var named = typeof(TrainingWavesTexts).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string) && Regex.IsMatch((string)f.GetRawConstantValue()!, "^TAOM_TGW_[A-Z0-9_]+$"))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();
        var inTexts = Lines().Values.SelectMany(t => Variable.Matches(t).Cast<Match>()).Select(m => m.Groups[1].Value).Distinct().ToList();

        CollectionAssert.AreEquivalent(inTexts, named.Distinct().ToList());
    }

    [TestMethod]
    public void NoLine_NamesThePlaceOrItsKeeperByAnOldName()
    {
        var old = new Regex(@"\b(arenas?|tournament masters?)\b", RegexOptions.IgnoreCase);

        foreach (var line in Lines())
            Assert.IsFalse(old.IsMatch(line.Value), line.Key + ": " + line.Value);
    }

    [TestMethod]
    public void TheLines_SayDenarsAndHouse_AsTheDesignWords()
    {
        var lines = Lines();

        StringAssert.Contains(lines["Rules"], "stays with the house");
        StringAssert.Contains(lines["Lost"], "stays with the house");
        StringAssert.Contains(lines["StakeLow"], "denars");
        StringAssert.StartsWith(lines["Offer"], "{=taom_tgw_offer}I want to fight your waves");
    }
}
