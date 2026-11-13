using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionMap.Hooks;

namespace TAOM.Tests.Features.FactionMap;

/// <summary>
/// The three CultureStageView patches find the engine view by these names, first match wins, so a rename
/// or a reorder changes which view they patch. The binding gate (HarmonyPatchBindingTests) invokes each
/// patch's TargetMethod and fails on null, so it proves the probe finds the view and each target on the
/// installed engine; it does not test the second name, which v1.5.7 does not ship.
/// </summary>
[TestClass]
public class CultureStageViewTypeTests
{
    [TestMethod]
    public void PossibleTypeNames_AreTheTwoEngineNamesInProbeOrder()
    {
        CollectionAssert.AreEqual(new[]
        {
            "SandBox.GauntletUI.CharacterCreation.CharacterCreationCultureStageView",
            "SandBox.View.CharacterCreation.CharacterCreationCultureStageView",
        }, CultureStageViewType.PossibleTypeNames);
    }
}
