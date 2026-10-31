using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Tests.Features.FordsOfWar.Diagnostics;

[TestClass]
public class FordsLogLineTests
{
    [TestMethod]
    public void InvariantCulture()
    {
        var before = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            var line = FordsLogLine.Of("Run", 3, 1.5, 12.25, "key=1");

            Assert.AreEqual("[FordsOfWar][Run] run=3 w=1.500 mt=12.3 key=1", line);
            Assert.AreEqual("0.5", FordsLogLine.F1(0.5));
            Assert.AreEqual("0.500", FordsLogLine.F3(0.5));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = before;
        }
    }

    [TestMethod]
    public void QuotesAndReplacesDoubleQuotes()
    {
        Assert.AreEqual("\"say 'hi'\"", FordsLogLine.Q("say \"hi\""));
        Assert.AreEqual("\"\"", FordsLogLine.Q(null));
    }

    [TestMethod]
    public void EmptyBody_AddsNoTrailingSpace()
    {
        Assert.AreEqual("[FordsOfWar][Stage] run=0 w=0.000 mt=0.0", FordsLogLine.Of("Stage", 0, 0, 0, ""));
    }
}
