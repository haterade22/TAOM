using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.SaveSystem;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Tests.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Pins the save base id of TAOM's formation presets (#1186). The engine adds the class id to
/// the base id, so TAOM's saves hold a preset under 726900601 + 101. With another base id the
/// engine finds no type for a preset in an older save and does not create the object.
/// The original CompanionTactics mod uses the same number and defines nothing when TAOM is
/// loaded. This is not an import of that mod's presets: it wrote the key "FormationPresets",
/// and TAOM reads "TAOM_FormationPresets".
/// The EquipPresets twin is <c>PresetSaveableTypeDefinerTests.BaseId_MatchesPlanned726900501</c>.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FormationPresetSaveableTypeDefinerTests
{
    [TestMethod]
    public void BaseId_Is726900601()
    {
        var sut = new FormationPresetSaveableTypeDefiner();
        var baseIdField = typeof(SaveableTypeDefiner).GetField("_saveBaseId",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(baseIdField, "SaveableTypeDefiner._saveBaseId is the field the engine keeps the base id in");

        Assert.AreEqual(726900601, (int)baseIdField!.GetValue(sut)!,
            "TAOM's saves hold formation presets under save id 726900601 + 101. With another base id the engine "
            + "finds no type for a preset in an older save (docs/reference/external-mod-couplings.md).");
    }
}
