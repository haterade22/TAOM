using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MixedFormations.Models;

namespace TAOM.Tests.Features.MixedFormations;

[TestClass]
public class FormationLayoutLabelsTests
{
    [DataTestMethod]
    [DataRow(FormationLayoutType.InfantryFrontRangedBack, "{=taom_mf_layout_inf_front}Infantry front, Ranged back")]
    [DataRow(FormationLayoutType.RangedFrontInfantryBack, "{=taom_mf_layout_ranged_front}Ranged front, Infantry back")]
    [DataRow(FormationLayoutType.RangedWingsInfantryCenter, "{=taom_mf_layout_ranged_wings}Ranged wings, Infantry center")]
    [DataRow(FormationLayoutType.Checkerboard, "{=taom_mf_layout_checkerboard}Checkerboard")]
    public void Describe_NamesEveryMixedLayout(FormationLayoutType layout, string expected)
    {
        Assert.AreEqual(expected, FormationLayoutLabels.Describe(layout));
    }

    [TestMethod]
    public void Describe_FallsBackToEnumNameForVanilla()
    {
        Assert.AreEqual("Vanilla", FormationLayoutLabels.Describe(FormationLayoutType.Vanilla));
    }
}
