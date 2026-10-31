using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsVolleyHitsPartNameTests
{
    [TestMethod]
    public void PartName_MatchesTheEnumName_AndReusesTheSameString()
    {
        Assert.AreEqual(BoneBodyPartType.Chest.ToString(), FordsVolleyHits.PartName(BoneBodyPartType.Chest));
        Assert.AreSame(FordsVolleyHits.PartName(BoneBodyPartType.Head), FordsVolleyHits.PartName(BoneBodyPartType.Head));
    }

    [TestMethod]
    public void PartName_AnUnnamedValue_FallsBackToItsNumber()
    {
        Assert.AreEqual("99", FordsVolleyHits.PartName((BoneBodyPartType)99));
    }
}
