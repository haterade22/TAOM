using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// ADR-002 entry-point ratchet (#967 C1). The behavior was 615 lines before earning moved into
/// SpecialResourceEarningService. Under 150 is a follow-up; this pins the ceiling so it cannot grow back.
/// </summary>
[TestClass]
public class SpecialResourcesBehaviorLineCountTests
{
    private const int Ceiling = 275;

    [TestMethod]
    public void SpecialResourcesBehavior_StaysWithinTheRatchet()
    {
        var path = RepoPaths.RepoPath("Main", "Features", "SpecialResources", "SpecialResourcesBehavior.cs");
        var lines = File.ReadAllLines(path).Length;
        Assert.IsTrue(lines <= Ceiling, "ADR-002 ratchet: SpecialResourcesBehavior.cs is " + lines + " lines, ceiling " + Ceiling);
    }
}
