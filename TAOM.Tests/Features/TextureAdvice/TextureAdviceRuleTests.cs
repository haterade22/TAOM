using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TextureAdvice;

namespace TAOM.Tests.Features.TextureAdvice;

[TestClass]
public class TextureAdviceRuleTests
{
    [TestMethod]
    public void ShouldAdvise_A16GbMachineAtTheTop_IsTrue()
        => Assert.IsTrue(TextureAdviceRule.ShouldAdvise(16321, 2, 2, false));

    [TestMethod]
    public void ShouldAdvise_ExactlyAtTheThreshold_IsTrue()
        => Assert.IsTrue(TextureAdviceRule.ShouldAdvise(17408, 2, 2, false));

    [TestMethod]
    public void ShouldAdvise_OneMbAboveTheThreshold_IsFalse()
        => Assert.IsFalse(TextureAdviceRule.ShouldAdvise(17409, 2, 2, false));

    [TestMethod]
    public void ShouldAdvise_A32GbMachine_IsFalse()
        => Assert.IsFalse(TextureAdviceRule.ShouldAdvise(32768, 2, 2, false));

    [TestMethod]
    public void ShouldAdvise_MediumTextures_IsFalse()
        => Assert.IsFalse(TextureAdviceRule.ShouldAdvise(16321, 1, 2, false));

    [TestMethod]
    public void ShouldAdvise_ShownBefore_IsFalse()
        => Assert.IsFalse(TextureAdviceRule.ShouldAdvise(16321, 2, 2, true));

    [TestMethod]
    public void ShouldAdvise_UnreadableMemory_IsFalse()
        => Assert.IsFalse(TextureAdviceRule.ShouldAdvise(0, 2, 2, false));

    [TestMethod]
    public void ShouldAdvise_UnknownTop_IsFalse()
        => Assert.IsFalse(TextureAdviceRule.ShouldAdvise(16321, 2, -1, false));

    [TestMethod]
    public void RoundedGigabytes_A16GbMachine_Is16()
        => Assert.AreEqual(16, TextureAdviceRule.RoundedGigabytes(16321));
}
