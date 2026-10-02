using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CharacterCreation;
using TAOM.Features.FactionUI.Presets;
using TAOM.Features.PlayerSwitcher;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. The character-creation finalize handlers run in priority order. The hero preset must
/// run after TAOM's own finalize, or TAOM's starting kit and race land over the pick, and before Player
/// Switcher's handover, the order Kysaro's postfix on TAOM's finalize had. A retuned constant anywhere
/// in the three would silently reorder them.
/// </summary>
[TestClass]
public class FactionPresetHandlerPriorityTests
{
    [TestMethod]
    public void ThePresetFinalize_RunsAfterTaomsAndBeforePlayerSwitchers()
    {
        var taom = (int)typeof(CharacterCreationRegistrationBehavior)
            .GetField("HandlerPriority", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetRawConstantValue();

        Assert.IsTrue(taom < FactionPresetRegistrationBehavior.HandlerPriority,
            $"TAOM's finalize ({taom}) must run before the preset ({FactionPresetRegistrationBehavior.HandlerPriority})");
        Assert.IsTrue(FactionPresetRegistrationBehavior.HandlerPriority < PlayerSwitchRegistrationBehavior.HandlerPriority,
            $"the preset ({FactionPresetRegistrationBehavior.HandlerPriority}) must run before Player Switcher ({PlayerSwitchRegistrationBehavior.HandlerPriority})");
    }
}
