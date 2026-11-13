using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionMap.Hooks;

namespace TAOM.Tests.Features.FactionMap;

/// <summary>
/// The three CultureStageView patches find the engine view by these names, first match wins, so a rename
/// or a reorder changes which view they patch. The binding gate (HarmonyPatchBindingTests) proves that the
/// names resolve against the installed engine.
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
