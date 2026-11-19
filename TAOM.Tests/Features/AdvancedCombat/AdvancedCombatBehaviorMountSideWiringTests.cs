using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// Wiring pin (#594): the shared creature behavior records a rider's side against his mount when he is built on it
/// and when he mounts one, so a bite can resolve a riderless mount's side. Comments are stripped, so a commented-out
/// call does not pass.
/// </summary>
[TestClass]
public class AdvancedCombatBehaviorMountSideWiringTests
{
    private const string Record = "MountRiderSides.RecordRider(agent)";

    private static string Member(string signatureFragment)
    {
        var source = RepoPaths.ReadSource("Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs", stripComments: true);
        var start = source.IndexOf(signatureFragment, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"'{signatureFragment}' is missing from AdvancedCombatBehavior.");
        var next = Regex.Match(source.Substring(start + signatureFragment.Length), @"\n    (?:public|private|protected|internal)\b");
        return next.Success ? source.Substring(start, signatureFragment.Length + next.Index) : source.Substring(start);
    }

    [TestMethod]
    public void OnAgentBuild_RecordsTheRidersSide()
        => StringAssert.Contains(Member("public override void OnAgentBuild(Agent agent, Banner banner)"), Record);

    [TestMethod]
    public void OnAgentMount_RecordsTheRidersSide()
        => StringAssert.Contains(Member("public override void OnAgentMount(Agent agent)"), Record);
}
