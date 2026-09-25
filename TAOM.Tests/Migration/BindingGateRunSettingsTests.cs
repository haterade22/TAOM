using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Migration;

/// <summary>
/// The binding gate is strict only through TAOM.Tests/binding-gate.runsettings. Without
/// MapInconclusiveToFailed a gate that could not load the game reports Skipped and exits 0;
/// without TreatNoTestsAsError a filter that matches no test (a renamed category, a typo)
/// runs nothing and exits 0. Either way the gate checks nothing and still reads green, so each
/// setting is pinned here, in the default suite, where deleting it goes red.
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
}
