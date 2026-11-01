using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Autorun;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Tests.Features.FordsOfWar.Autorun;

/// <summary>The client builds the driver through <see cref="FordsAutorunStart"/> only when the plan is armed, and says why when a set variable is unusable.</summary>
[TestClass]
public class FordsAutorunStartTests
{
    private IModLogger _logger = null!;
    private List<string> _warnings = null!;
    private int _built;

    [TestInitialize]
    public void Init()
    {
        _built = 0;
        _warnings = new List<string>();
        _logger = Substitute.For<IModLogger>();
        _logger.When(l => l.LogWarning(Arg.Any<string>())).Do(c => _warnings.Add(c.Arg<string>()));
    }

    private FordsAutorunDriver Build()
    {
        _built++;
        var report = new FordsRunReport(Substitute.For<IModLogger>());
        var catalog = FordsConfigCatalog.FromTexts(null, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps, Substitute.For<IModLogger>());
        return new FordsAutorunDriver(FordsAutorunPlan.Resolve(_ => "arm.txt", _ => true, _ => "out=E:\\out"), report, catalog,
            Substitute.For<IFordsAutorunHost>(), Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void Driver_UnsetVariable_BuildsNothingAndSaysNothing()
    {
        var driver = FordsAutorunStart.Driver(new FordsAutorunPlan(), Build, _logger);

        Assert.IsNull(driver);
        Assert.AreEqual(0, _built);
        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethod]
    public void Driver_SetVariableThatCannotArm_LogsWhyAndBuildsNothing()
    {
        var plan = FordsAutorunPlan.Resolve(_ => "arm.txt", _ => false, _ => "");

        var driver = FordsAutorunStart.Driver(plan, Build, _logger);

        Assert.IsNull(driver);
        Assert.AreEqual(0, _built);
        Assert.AreEqual(1, _warnings.Count);
        StringAssert.StartsWith(_warnings[0], "[FordsOfWar][Autorun] not armed: ");
        StringAssert.Contains(_warnings[0], "missing file");
    }

    [TestMethod]
    public void Driver_ArmedPlan_BuildsTheDriverOnce()
    {
        var plan = FordsAutorunPlan.Resolve(_ => "arm.txt", _ => true, _ => "out=E:\\out");

        var driver = FordsAutorunStart.Driver(plan, Build, _logger);

        Assert.IsNotNull(driver);
        Assert.AreEqual(1, _built);
        Assert.AreEqual(0, _warnings.Count);
    }
}
