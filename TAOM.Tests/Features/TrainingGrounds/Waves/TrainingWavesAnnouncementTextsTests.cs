using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TrainingGrounds.Waves.Hooks;

namespace TAOM.Tests.Features.TrainingGrounds.Waves;

/// <summary>The four quick notices of a wave fight: a keyed literal each, with variables that no other notice and no master line reads.</summary>
[TestClass]
public class TrainingWavesAnnouncementTextsTests
{
    private static readonly Regex Variable = new(@"\{([A-Z][A-Z0-9_]*)\}", RegexOptions.Compiled);

    private static Dictionary<string, string> Notices() =>
        typeof(TrainingWavesAnnouncementTexts).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string) && ((string)f.GetRawConstantValue()!).StartsWith("{=taom_tgw_"))
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!);

    [TestMethod]
    public void EveryNotice_IsAKeyedLiteral_WithItsOwnKey()
    {
        var notices = Notices();

        CollectionAssert.AreEquivalent(new[] { "Start", "Cleared", "Lost", "Refunded" }, notices.Keys.ToList());
        var keys = notices.Values.Select(t => Regex.Match(t, @"^\{=([A-Za-z0-9_]+)\}").Groups[1].Value).ToList();
        Assert.AreEqual(4, keys.Distinct().Count(), "two notices share a key");
        Assert.IsTrue(keys.All(k => k.StartsWith("taom_tgw_an_")), "the keys of the notices carry the taom_tgw_an_ prefix");
    }

    [TestMethod]
    public void EveryNoticeVariable_IsReadByOneNotice_AndByNoMasterLine()
    {
        var master = typeof(TrainingWavesTexts).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .SelectMany(f => Variable.Matches((string)f.GetRawConstantValue()!).Cast<Match>().Select(m => m.Groups[1].Value))
            .ToHashSet();
        var seen = new HashSet<string>();

        foreach (var notice in Notices())
            foreach (Match m in Variable.Matches(notice.Value))
            {
                Assert.IsTrue(seen.Add(m.Groups[1].Value), m.Groups[1].Value + " is read by two notices");
                Assert.IsFalse(master.Contains(m.Groups[1].Value), m.Groups[1].Value + " is also a variable of a master line");
            }
    }

    [TestMethod]
    public void TheNotices_NameTheWaveAndTheirNumber()
    {
        var notices = Notices();

        StringAssert.Contains(notices["Start"], "{TAOM_TGW_AN_START_WAVE}");
        StringAssert.Contains(notices["Start"], "{TAOM_TGW_AN_START_COUNT}");
        StringAssert.Contains(notices["Cleared"], "{TAOM_TGW_AN_CLEARED_WAVE}");
        StringAssert.Contains(notices["Cleared"], "{TAOM_TGW_AN_CLEARED_PAYOUT}");
        StringAssert.Contains(notices["Lost"], "{TAOM_TGW_AN_LOST_WAVE}");
        StringAssert.Contains(notices["Lost"], "{TAOM_TGW_AN_LOST_STAKE}");
    }

    [TestMethod]
    public void TheRefundNotice_HasItsKey_ItsText_AndAVariableOfItsOwn()
    {
        Assert.AreEqual(
            "{=taom_tgw_an_refunded}The wave was called off. Your wager of {TAOM_TGW_AN_REFUNDED_STAKE} denars is returned.",
            Notices()["Refunded"]);
        var others = Notices().Where(n => n.Key != "Refunded").SelectMany(n => Variable.Matches(n.Value).Cast<Match>().Select(m => m.Groups[1].Value));
        CollectionAssert.DoesNotContain(others.ToList(), "TAOM_TGW_AN_REFUNDED_STAKE");
    }
}
