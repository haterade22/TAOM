using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.DreadAura;

/// <summary>
/// Source pin for the shared one-shot morale drain (#1359). The engine types it touches cannot run in a
/// unit test, so the order of its gates is pinned as text: the fear value, the model and the agent gate
/// come before any engine call, and the write is the last step.
/// </summary>
[TestClass]
public class MoraleShockPinTests
{
    private static int At(string text, string part)
    {
        var at = text.IndexOf(part, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, part + " not found");
        return at;
    }

    [TestMethod]
    public void Apply_KeepsItsGatesInOrder_AndWritesLast()
    {
        var source = RepoPaths.ReadSource("Main/Features/DreadAura/Hooks/MoraleShock.cs", stripComments: true);

        var order = new[]
        {
            "!(fearMorale > 0f)",
            "moraleModel == null",
            "DreadAgentGate.CanAffect(victim)",
            "CareerTroopHooks.ScaleFearDrain(careerStats, victim, moraleModel.CalculateMoraleChangeToCharacter(victim, fearMorale))",
            "FearDrainMath.Compute(",
            "dreadRegistry.ResolveResist(victim.Character?.Race)",
            "victim.ChangeMorale(-drain)",
        };
        var last = -1;
        foreach (var part in order)
        {
            var at = At(source, part);
            Assert.IsTrue(at > last, part + " is out of order");
            last = at;
        }
    }
}
