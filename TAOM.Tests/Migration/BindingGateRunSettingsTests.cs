using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Migration;

/// <summary>
/// The binding gate is strict only through TAOM.Tests/binding-gate.runsettings. Without
/// MapInconclusiveToFailed a gate that could not load the game reports Skipped and exits 0;
/// without TreatNoTestsAsError a filter that matches no test (a renamed category, a typo)
/// runs nothing and exits 0. Either way the gate checks nothing and still reads green, so each
/// setting is pinned here, in the default suite, where deleting it goes red. The file also sets TAOM_RUN_BENCHMARKS=1
/// for the test host: a check that is opt-in in the default suite and tagged BindingVerification (the XmlMerge live
/// harness) would otherwise be Inconclusive in the gate, which fails it. That variable is pinned here too.
/// </summary>
[TestClass]
public class BindingGateRunSettingsTests
{
    [DataTestMethod]
    [DataRow("MSTest", "MapInconclusiveToFailed")]
    [DataRow("RunConfiguration", "TreatNoTestsAsError")]
    public void BindingGateRunSettings_StrictSetting_IsTrue(string section, string setting)
    {
        // Arrange
        var doc = XDocument.Load(RepoPath("TAOM.Tests", "binding-gate.runsettings"));

        // Act
        var value = doc.Root?.Element(section)?.Element(setting)?.Value;

        // Assert
        Assert.AreEqual("true", value, $"binding-gate.runsettings must set <{section}><{setting}>true</{setting}></{section}>");
    }

    [TestMethod]
    public void BindingGateRunSettings_OptInVariable_IsSetForTheTestHost()
    {
        // Arrange
        var doc = XDocument.Load(RepoPath("TAOM.Tests", "binding-gate.runsettings"));

        // Act
        var value = doc.Root?.Element("RunConfiguration")?.Element("EnvironmentVariables")?.Element("TAOM_RUN_BENCHMARKS")?.Value;

        // Assert
        Assert.AreEqual("1", value,
            "binding-gate.runsettings must set <TAOM_RUN_BENCHMARKS>1</TAOM_RUN_BENCHMARKS> under <RunConfiguration><EnvironmentVariables>: " +
            "without it the opt-in live merge harness is Inconclusive in the gate, and the gate fails it.");
    }
}
