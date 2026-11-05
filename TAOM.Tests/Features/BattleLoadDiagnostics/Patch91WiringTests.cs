using System;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

/// <summary>
/// Patch91 (#634) is dead code unless both classes carry the category and SubModule applies it
/// through plan 009's guard in the game-init batch, after Patch43, with the watchdog started.
/// </summary>
[TestClass]
public class Patch91WiringTests
{
    [TestMethod]
    public void Patch91_IsAppliedThroughTheGuard_AndTheWatchdogStarts()
    {
        foreach (var patch in new[] { typeof(Mission_TickAgentsAndTeamsImp_StallProbe_Patch), typeof(MissionState_TickMissionAux_StallProbe_Patch) })
        {
            Assert.AreEqual(1, patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length, $"{patch.Name} lost its target");
            var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
                .Cast<HarmonyPatchCategory>().Select(c => c.info.category).ToList();
            CollectionAssert.Contains(categories, "Patch91_MissionTickStall", $"{patch.Name} lost its category");
        }

        var source = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        const string apply = "TryPatchCategory(\"Patch91_MissionTickStall\");";
        const string start = "IoC.Resolve<Features.BattleLoadDiagnostics.MissionTickStallWatchdog>().Start();";
        int patch43 = source.IndexOf("TryPatchCategory(\"Patch43_BattleLoadDiagnostics\");", StringComparison.Ordinal);
        int at = source.IndexOf(apply, StringComparison.Ordinal);
        Assert.IsTrue(patch43 >= 0 && at > patch43, "Patch91 must be applied through TryPatchCategory after Patch43");
        Assert.AreEqual(at, source.LastIndexOf(apply, StringComparison.Ordinal), "Patch91 applied twice");
        Assert.IsTrue(source.IndexOf(start, StringComparison.Ordinal) > at, "the watchdog must start after Patch91 applies");
    }
}
