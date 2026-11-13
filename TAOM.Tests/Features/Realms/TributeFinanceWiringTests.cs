using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Features.CareerSystem;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.Realms;

/// <summary>
/// Pins where the finance model adds the Realms tribute (RealmsCampaign.Tribute.cs). The gold change carries both
/// lines, so a vassal's daily tick pays what its liege's tick receives; the income tile carries only what the clan
/// receives and the expenses tile only what it pays. Without the "out" line in the gold change, every liege would be
/// paid while no vassal pays, and no other test would notice. The lines are flat adds, so their order is only the
/// tooltip's and is not pinned.
/// </summary>
[TestClass]
public class TributeFinanceWiringTests
{
    private const string FinanceModel = "Main/Features/CulturalFeats/Models/TaomClanFinanceModel.cs";
    private const string TributeIn = "RealmsCampaign.Current?.AddTributeIncome(clan, includeDetails, ref result);";
    private const string TributeOut = "RealmsCampaign.Current?.AddTributeExpenses(clan, includeDetails, ref result);";

    private static string Member(string name) =>
        CareerCampaignPassivesTests.MemberText(ReadSource(FinanceModel, stripComments: true),
            "public override ExplainedNumber " + name + "(");

    [TestMethod]
    public void CalculateClanGoldChange_AddsBothTributeLinesOnce()
    {
        var member = Member("CalculateClanGoldChange");
        Assert.AreEqual(1, Occurrences(member, TributeIn), member);
        Assert.AreEqual(1, Occurrences(member, TributeOut), member);
    }

    [TestMethod]
    public void CalculateClanIncome_AddsOnlyTheTributeReceived()
    {
        var member = Member("CalculateClanIncome");
        Assert.AreEqual(1, Occurrences(member, TributeIn), member);
        Assert.AreEqual(0, Occurrences(member, "AddTributeExpenses"), member);
    }

    [TestMethod]
    public void CalculateClanExpenses_AddsOnlyTheTributePaid()
    {
        var member = Member("CalculateClanExpenses");
        Assert.AreEqual(1, Occurrences(member, TributeOut), member);
        Assert.AreEqual(0, Occurrences(member, "AddTributeIncome"), member);
    }

    private static int Occurrences(string text, string fragment)
    {
        var count = 0;
        for (var at = text.IndexOf(fragment, System.StringComparison.Ordinal); at >= 0;
             at = text.IndexOf(fragment, at + fragment.Length, System.StringComparison.Ordinal))
            count++;
        return count;
    }
}
