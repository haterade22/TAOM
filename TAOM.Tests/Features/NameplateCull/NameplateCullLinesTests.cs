using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.NameplateCull;

namespace TAOM.Tests.Features.NameplateCull;

/// <summary>Every line the cull writes, pinned literally: the log is how the maintainer reads the A/B.</summary>
[TestClass]
public class NameplateCullLinesTests
{
    [TestMethod]
    public void InstallLine_ToggleOn()
    {
        Assert.AreEqual("[NameplateCull] ON: the campaign map skips hidden settlement nameplates (toggle on)",
            NameplateCullLines.BuildInstallLine(toggleOn: true));
    }

    [TestMethod]
    public void InstallLine_ToggleOff()
    {
        Assert.AreEqual("[NameplateCull] installed, toggle off: every settlement nameplate updates as in the vanilla game",
            NameplateCullLines.BuildInstallLine(toggleOn: false));
    }

    [TestMethod]
    public void UnavailableLine_NamesTheReason()
    {
        Assert.AreEqual("[NameplateCull] OFF: SettlementNameplatesVM._mapCamera is missing; the vanilla nameplate update runs",
            NameplateCullLines.BuildUnavailableLine("SettlementNameplatesVM._mapCamera is missing"));
    }

    [TestMethod]
    public void WindowLine_CarriesFramesAndTheSplit()
    {
        Assert.AreEqual("[NameplateCull] 18000 culled map frames: 612345 nameplate updates run, 4987655 skipped (89.1 percent)",
            NameplateCullLines.BuildWindowLine(18000, 612345, 4987655));
    }

    [TestMethod]
    public void WindowLine_NothingCounted_IsZeroPercentNotNaN()
    {
        Assert.AreEqual("[NameplateCull] 5 culled map frames: 0 nameplate updates run, 0 skipped (0.0 percent)",
            NameplateCullLines.BuildWindowLine(5, 0, 0));
    }

    [TestMethod]
    public void WindowLine_IsCultureInvariant()
    {
        var old = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            Assert.AreEqual("[NameplateCull] 1 culled map frames: 1 nameplate updates run, 2 skipped (66.7 percent)",
                NameplateCullLines.BuildWindowLine(1, 1, 2));
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = old;
        }
    }

    [TestMethod]
    public void SwitchedOffLine_SaysThisGameLaunchNotTheSession()
    {
        StringAssert.Contains(NameplateCullLines.BuildSwitchedOffLine(new InvalidOperationException("boom")),
            "for the rest of this game launch");
    }

    [TestMethod]
    public void SwitchedOffLine_CarriesTheException()
    {
        var line = NameplateCullLines.BuildSwitchedOffLine(new InvalidOperationException("boom"));

        StringAssert.StartsWith(line, "[NameplateCull] OFF after an error, the vanilla nameplate update runs for the rest of this game launch: ");
        StringAssert.Contains(line, "InvalidOperationException: boom");
    }
}
