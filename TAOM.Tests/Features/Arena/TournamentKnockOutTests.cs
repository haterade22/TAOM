using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Arena;

/// <summary>
/// Tournament hits never kill, whatever the weapon (#422): SandBox's <c>TournamentMissionStarter</c> adds
/// <c>ArenaAgentStateDeciderLogic</c> to every tournament mission, and <c>Mission.GetAgentState</c> lets the first
/// <c>IAgentStateDecider</c> decide before the death roll. This pins the one engine fact the culture tournament
/// templates (sharp troop weapons) rest on: the logic still exists and still implements the decider interface.
/// Resolved by name after the game-assembly guard, so no SandBox type loads before the Inconclusive check.
/// </summary>
[TestClass]
public class TournamentKnockOutTests
{
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ArenaAgentStateDeciderLogic_ImplementsIAgentStateDecider()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name == "SandBox")
            .Select(a => a.GetTypes().FirstOrDefault(t => t.Name == "ArenaAgentStateDeciderLogic"))
            .FirstOrDefault(t => t != null);
        Assert.IsNotNull(type, "ArenaAgentStateDeciderLogic is gone from SandBox.dll: sharp tournament weapons may kill again.");
        Assert.IsTrue(type!.GetInterfaces().Any(i => i.Name == "IAgentStateDecider"),
            "ArenaAgentStateDeciderLogic no longer implements IAgentStateDecider.");
    }
}
