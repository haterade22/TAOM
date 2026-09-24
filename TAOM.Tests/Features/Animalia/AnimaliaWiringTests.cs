using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Animalia;

namespace TAOM.Tests.Features.Animalia;

/// <summary>
/// Source pins for the Animalia antler attack's wiring (#646). Without the registration the profiles' lazy resolve
/// throws at the first attack; without the mission behavior no tree ever attaches and both antler attacks are silently
/// dead; and a profile built without the reach flag compiles and passes every other test while the moose (1.5x) scans
/// at the 1.0x ranges. The profiles cannot be built in a unit test (they create ActionIndexCaches), hence source pins.
/// </summary>
[TestClass]
public class AnimaliaWiringTests
{
    [TestMethod]
    public void MainIoC_RegistersTheAnimaliaFeature()
    {
        StringAssert.Contains(Source("Main", "IoC.cs"), "Features.Animalia.AnimaliaIoC.RegisterAnimaliaFeature(container);");
    }

    [TestMethod]
    public void SubModule_AddsTheAnimaliaMissionBehavior()
    {
        StringAssert.Contains(Source("Main", "SubModule.cs"), "AddTaomBehavior(new Features.Animalia.AnimaliaMissionBehavior());");
    }

    [TestMethod]
    public void AnimaliaIoC_Registration_ResolvesBothAttackServices()
    {
        var container = new Container();
        AnimaliaIoC.RegisterAnimaliaFeature(container);

        var errors = container.Validate(typeof(IAnimaliaElkAttackService), typeof(IAnimaliaMooseAttackService));

        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(e => e.Value.Message)));
    }

    [TestMethod]
    public void BothProfiles_PassTheReachFlag()
    {
        string combat = Source("Main", "Features", "Animalia", "AnimaliaCombat.cs");
        int profiles = Regex.Matches(combat, @"new ElephantLikeCombatProfile\(|\bnew\(").Count;
        int flagged = Regex.Matches(combat, @"reachScalesWithBody:\s*AnimaliaConfig\.ReachScalesWithBody").Count;

        Assert.AreEqual(2, profiles, "AnimaliaCombat builds one profile per animal");
        Assert.AreEqual(profiles, flagged, "every Animalia profile must pass reachScalesWithBody (its default is false)");
    }

    private static string Source(params string[] relativeParts)
    {
        string? dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            string candidate = Path.Combine(new[] { dir }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = Directory.GetParent(dir)?.FullName;
        }
        Assert.Inconclusive(Path.Combine(relativeParts) + " not found: run from the repo root");
        return null!;
    }
}
