using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LordRetinue;

/// <summary>
/// #1475. LordRetinue's OnPartyDisbandStarted handler runs inside the engine's battle-death loop:
/// MapEventSide.HandleMapEventEndForPartyInternal iterates a lazy WhereQ over GetTroopRoster() and kills each
/// marked hero; a leader killed with no other hero aboard reaches DisbandPartyAction.StartDisband, which fires
/// the event. GetTroopRoster() refills that same cached list in place after the kill removed the leader, so a
/// call from the handler makes the engine loop read past the end of its list.
/// </summary>
[TestClass]
public class LordRetinueRosterReadTests
{
    [TestMethod]
    public void LordRetinue_NeverCallsGetTroopRoster()
    {
        foreach (var file in new[] { "LordRetinueBehavior", "LordRetinueWorld" })
        {
            var src = RepoPaths.ReadSource($"Main/Features/LordRetinue/Hooks/{file}.cs", stripComments: true);
            Assert.IsFalse(src.Contains("GetTroopRoster("),
                $"{file} calls GetTroopRoster(), which rebuilds the cached list the engine's battle-death loop "
                + "is still reading; read the roster by index with GetElementCopyAtIndex (#1475)");
        }
    }
}
