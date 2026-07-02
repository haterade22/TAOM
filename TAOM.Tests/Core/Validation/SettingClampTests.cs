using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Validation;

namespace TAOM.Tests.Core.Validation;

// Shared MCM-setting clamp (R2 consolidation of the byte-identical SafeClamp/SafeClampInt private
// helpers previously copy-pasted across SmartCavalryAI / BanditManagement / CultureConversion /
// CastleRecruitment settings providers). Semantics pinned 1-for-1 with those copies:
// null -> default, NaN/Infinity -> default (float only), then inclusive [min,max] clamp.
[TestClass]
public class SettingClampTests
{
    // ------------------------------------------------------------------ float overload

    [TestMethod]
    public void Clamp_Float_Null_ReturnsDefault()
        => Assert.AreEqual(0.7f, SettingClamp.Clamp((float?)null, 0.7f, 0f, 1f));

    [TestMethod]
    public void Clamp_Float_NaN_ReturnsDefault()
        => Assert.AreEqual(0.7f, SettingClamp.Clamp(float.NaN, 0.7f, 0f, 1f));

    [TestMethod]
    public void Clamp_Float_PositiveInfinity_ReturnsDefault()
        => Assert.AreEqual(0.7f, SettingClamp.Clamp(float.PositiveInfinity, 0.7f, 0f, 1f));

    [TestMethod]
    public void Clamp_Float_NegativeInfinity_ReturnsDefault()
        => Assert.AreEqual(0.7f, SettingClamp.Clamp(float.NegativeInfinity, 0.7f, 0f, 1f));

    [TestMethod]
    public void Clamp_Float_BelowMin_ClampsToMin()
        => Assert.AreEqual(10f, SettingClamp.Clamp(5f, 25f, 10f, 80f));

    [TestMethod]
    public void Clamp_Float_AboveMax_ClampsToMax()
        => Assert.AreEqual(80f, SettingClamp.Clamp(200f, 25f, 10f, 80f));

    [TestMethod]
    public void Clamp_Float_InRange_ReturnsValue()
        => Assert.AreEqual(0.5f, SettingClamp.Clamp(0.5f, 0.7f, 0f, 1f));

    [TestMethod]
    public void Clamp_Float_AtBounds_Inclusive()
    {
        Assert.AreEqual(0f, SettingClamp.Clamp(0f, 0.7f, 0f, 1f));
        Assert.AreEqual(1f, SettingClamp.Clamp(1f, 0.7f, 0f, 1f));
    }

    [TestMethod]
    public void Clamp_Float_NullWithOutOfRangeDefault_DefaultFlowsThroughClamp()
        // Pinned pre-consolidation behavior: a NULL value takes the default and then goes through
        // the range clamp like any value (1.2 clamps to max 1).
        => Assert.AreEqual(1f, SettingClamp.Clamp((float?)null, 1.2f, 0f, 1f));

    [TestMethod]
    public void Clamp_Float_NaNWithOutOfRangeDefault_DefaultReturnedVerbatim()
        // Pinned pre-consolidation behavior: the NaN/Infinity path returns the compiled default
        // VERBATIM (early return before the clamp) — asymmetric with the null path above.
        => Assert.AreEqual(1.2f, SettingClamp.Clamp(float.NaN, 1.2f, 0f, 1f));

    // ------------------------------------------------------------------ int overload

    [TestMethod]
    public void Clamp_Int_Null_ReturnsDefault()
        => Assert.AreEqual(3, SettingClamp.Clamp((int?)null, 3, 1, 5));

    [TestMethod]
    public void Clamp_Int_BelowMin_ClampsToMin()
        => Assert.AreEqual(1, SettingClamp.Clamp(0, 3, 1, 5));

    [TestMethod]
    public void Clamp_Int_AboveMax_ClampsToMax()
        => Assert.AreEqual(5, SettingClamp.Clamp(99, 3, 1, 5));

    [TestMethod]
    public void Clamp_Int_InRange_ReturnsValue()
        => Assert.AreEqual(4, SettingClamp.Clamp(4, 3, 1, 5));

    [TestMethod]
    public void Clamp_Int_AtBounds_Inclusive()
    {
        Assert.AreEqual(1, SettingClamp.Clamp(1, 3, 1, 5));
        Assert.AreEqual(5, SettingClamp.Clamp(5, 3, 1, 5));
    }
}
