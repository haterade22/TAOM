using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Presentation;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// #830: which "you stayed back" line the announcer picks. Pure string choice, so no engine code runs.
/// </summary>
[TestClass]
public class ServiceDailyAnnouncerSatOutTests
{
    [DataTestMethod]
    [DataRow(SitOutVerdict.WoundedFree, "taom_enlist_sat_out_wounded_line")]
    [DataRow(SitOutVerdict.OverwhelmingCost, "taom_enlist_sat_out_line")]
    [DataRow(SitOutVerdict.ForcedOpen, "taom_enlist_sat_out_forced_line")]
    public void SatOutTemplate_EachReason_PicksItsOwnLine(SitOutVerdict reason, string key)
    {
        var template = ServiceDailyAnnouncer.SatOutTemplate(reason);

        Assert.IsNotNull(template);
        StringAssert.StartsWith(template, "{=" + key + "}");
    }

    [TestMethod]
    public void SatOutTemplate_NoReason_SaysNothing()
    {
        Assert.IsNull(ServiceDailyAnnouncer.SatOutTemplate(null));
    }

    [DataTestMethod]
    [DataRow(SitOutVerdict.Unchanged)]
    [DataRow(SitOutVerdict.RefusedField)]
    [DataRow(SitOutVerdict.RefusedSiege)]
    public void SatOutTemplate_ReasonsThatAreNotASitOut_SayNothing(SitOutVerdict reason)
    {
        Assert.IsNull(ServiceDailyAnnouncer.SatOutTemplate(reason));
    }

    [TestMethod]
    public void SatOutTemplate_OverwhelmingLine_NamesNoFractionAndNoSergeants()
    {
        var template = ServiceDailyAnnouncer.SatOutTemplate(SitOutVerdict.OverwhelmingCost)!;

        StringAssert.Contains(template, "your share of the battle's due was cut");
        Assert.IsFalse(template.Contains("half"));
        Assert.IsFalse(template.Contains("sergeants"));
    }
}
