using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
[TestCategory("RequiresGame")]
public class CareerChoiceGroupObjectVMTests
{
    private static CareerChoiceGroupObjectVM Group(int maxPicks) => new CareerChoiceGroupObjectVM(
        new CareerChoiceGroupDefinition("g1", "career", 1, new[] { "a", "b", "c" }, "Group", maxPicks));

    [TestMethod]
    public void SelectionRuleText_OnePickFork_SaysChooseOne()
    {
        Assert.AreEqual("Choose one", Group(1).SelectionRuleText);
    }

    [TestMethod]
    public void SelectionRuleText_LargerFork_NamesItsLimit()
    {
        Assert.AreEqual("Choose 2", Group(2).SelectionRuleText);
    }

    [TestMethod]
    public void SelectionRuleText_GroupWithoutALimit_IsEmpty()
    {
        Assert.AreEqual(string.Empty, Group(0).SelectionRuleText);
    }
}
