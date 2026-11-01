using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Tests.Features.FordsOfWar.Diagnostics;

[TestClass]
public class FordsLayoutFitLinesTests
{
    [TestMethod]
    public void Timing_PlanAndTowerTimes_NamesBothInMillisecondsWithOneDecimal()
    {
        var line = FordsLayoutFitLines.Timing(TimeSpan.FromTicks(123_400), TimeSpan.FromMilliseconds(3));

        Assert.AreEqual("[FordsOfWar][Layout] timing plan=12.3 ms towers=3.0 ms", line);
    }

    [TestMethod]
    public void Timing_ASlowPlan_KeepsTheDecimalPointWhateverTheCulture()
    {
        var before = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            var line = FordsLayoutFitLines.Timing(TimeSpan.FromTicks(12_345_000), TimeSpan.Zero);

            StringAssert.Contains(line, "plan=1234.5 ms");
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = before;
        }
    }
}
