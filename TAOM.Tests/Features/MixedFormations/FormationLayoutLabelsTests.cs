using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MixedFormations.Models;

namespace TAOM.Tests.Features.MixedFormations;

[TestClass]
public class FormationLayoutLabelsTests
{
    [DataTestMethod]
    [DataRow(FormationLayoutType.InfantryFrontRangedBack, "Infantry front, Ranged back")]
    [DataRow(FormationLayoutType.RangedFrontInfantryBack, "Ranged front, Infantry back")]
    [DataRow(FormationLayoutType.RangedWingsInfantryCenter, "Ranged wings, Infantry center")]
    [DataRow(FormationLayoutType.Checkerboard, "Checkerboard")]
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
