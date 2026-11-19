using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CoopInterop.Missions;

/// <summary>
/// #967 C7: AddTaomBehavior skips an Unsafe behavior during a co-op session before anything is added, and the
/// session flag is read once per mission, outside the local function.
/// </summary>
[TestClass]
public class CoopMissionAddFilterPinTests
{
    [TestMethod]
    public void AddTaomBehavior_FiltersByRoleBeforeAddingAnything_WithTheSessionReadOnce()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var start = src.IndexOf("void AddBuiltTaomBehavior(MissionBehavior behavior)", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "AddBuiltTaomBehavior is gone; update this pin.");
        var body = src.Substring(start, 900);

        var filter = body.IndexOf("CoopMissionRolePolicy.ShouldAdd(behavior.GetType(), coopSessionActive)", StringComparison.Ordinal);
        var add = body.IndexOf("mission.AddMissionBehavior(behavior)", StringComparison.Ordinal);
        Assert.IsTrue(filter >= 0 && add > filter, "the role filter must run before the behavior is added");
        StringAssert.Contains(body, "[CoopMission] skipped");

        Assert.AreEqual(1, Regex.Matches(src, @"var coopSessionActive = ").Count);
        Assert.IsTrue(src.IndexOf("var coopSessionActive = ", StringComparison.Ordinal) < start);
    }
}
