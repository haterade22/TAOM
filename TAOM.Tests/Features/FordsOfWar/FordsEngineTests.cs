using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsEngineTests
{
    [TestMethod]
    public void FinitePosition_AllComponentsFinite_ReturnsThePoint()
    {
        var position = FordsEngine.FinitePosition(new Vec3(1f, 2f, 3f, -1f));

        Assert.AreEqual(new Vec3f(1f, 2f, 3f), position);
    }

    [TestMethod]
    public void FinitePosition_AnyComponentNotFinite_ReturnsNull()
    {
        Assert.IsNull(FordsEngine.FinitePosition(new Vec3(float.NaN, 2f, 3f, -1f)));
        Assert.IsNull(FordsEngine.FinitePosition(new Vec3(1f, float.PositiveInfinity, 3f, -1f)));
        Assert.IsNull(FordsEngine.FinitePosition(new Vec3(1f, 2f, float.NegativeInfinity, -1f)));
    }

    [TestMethod]
    public void DisplayName_LeadingSortTag_IsStripped()
    {
        Assert.AreEqual("Hill Troll", FordsEngine.DisplayName("[AMordor] Hill Troll"));
    }

    [TestMethod]
    public void DisplayName_OnlyTheFirstTagIsStripped()
    {
        Assert.AreEqual("[Elite] Guard", FordsEngine.DisplayName("[AGondor] [Elite] Guard"));
    }

    [TestMethod]
    public void DisplayName_NoTag_IsUnchanged()
    {
        Assert.AreEqual("Hill Troll", FordsEngine.DisplayName("Hill Troll"));
        Assert.AreEqual("Guard [Elite]", FordsEngine.DisplayName("Guard [Elite]"));
    }

    [TestMethod]
    public void DisplayName_TagWithoutTheFollowingSpace_IsUnchanged()
    {
        Assert.AreEqual("[AMordor]Hill Troll", FordsEngine.DisplayName("[AMordor]Hill Troll"));
    }

    [TestMethod]
    public void DisplayName_TagIsTheWholeName_IsUnchanged()
    {
        Assert.AreEqual("[AMordor] ", FordsEngine.DisplayName("[AMordor] "));
        Assert.AreEqual("[AMordor]", FordsEngine.DisplayName("[AMordor]"));
    }

    [TestMethod]
    public void DisplayName_EmptyBrackets_AreStripped()
    {
        Assert.AreEqual("Guard", FordsEngine.DisplayName("[] Guard"));
    }
}
