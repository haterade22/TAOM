using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// The arithmetic behind the howdah diagnostics log (#627). The machine feeds it engine floats, so every result that
/// can see a non-finite input must come back NaN rather than a number computed from garbage, and the warning gate must
/// fire on NaN (a positive requirement: only a proven clearance is quiet).
/// </summary>
[TestClass]
public class HowdahDiagnosticsTests
{
    [TestMethod]
    public void CapsuleTopZ_ElephantCapsule_IsFeetPlusHigherPointPlusRadius()
    {
        // lotr_monster_elephant.xml: radius 1.05, both points at z 1.65, so the top is 2.70 m above the feet.
        Assert.AreEqual(12.70f, HowdahDiagnostics.CapsuleTopZ(10f, 1.65f, 1.65f, 1.05f), 1e-4f);
    }

    [TestMethod]
    public void CapsuleTopZ_TiltedCapsule_UsesTheHigherPoint()
    {
        Assert.AreEqual(3.5f, HowdahDiagnostics.CapsuleTopZ(0f, 1.0f, 2.5f, 1.0f), 1e-4f);
    }

    [TestMethod]
    public void CapsuleTopZ_NaNInput_ReturnsNaN()
    {
        Assert.IsTrue(float.IsNaN(HowdahDiagnostics.CapsuleTopZ(float.NaN, 1.65f, 1.65f, 1.05f)));
        Assert.IsTrue(float.IsNaN(HowdahDiagnostics.CapsuleTopZ(0f, float.NaN, 1.65f, 1.05f)));
        Assert.IsTrue(float.IsNaN(HowdahDiagnostics.CapsuleTopZ(0f, 1.65f, float.PositiveInfinity, 1.05f)));
        Assert.IsTrue(float.IsNaN(HowdahDiagnostics.CapsuleTopZ(0f, 1.65f, 1.65f, float.NaN)));
    }

    [TestMethod]
    public void Clearance_FloorAboveCapsule_IsPositive()
    {
        Assert.AreEqual(0.35f, HowdahDiagnostics.Clearance(3.05f, 2.70f), 1e-4f);
    }

    [TestMethod]
    public void Clearance_FloorInsideCapsule_IsNegative()
    {
        Assert.AreEqual(-0.2f, HowdahDiagnostics.Clearance(2.5f, 2.70f), 1e-4f);
    }

    [TestMethod]
    public void Clearance_NaNInput_ReturnsNaN()
    {
        Assert.IsTrue(float.IsNaN(HowdahDiagnostics.Clearance(float.NaN, 2.70f)));
        Assert.IsTrue(float.IsNaN(HowdahDiagnostics.Clearance(3.05f, float.NaN)));
    }

    [TestMethod]
    public void ClearanceWarrantsWarning_PositiveOrZero_IsQuiet()
    {
        Assert.IsFalse(HowdahDiagnostics.ClearanceWarrantsWarning(0.35f));
        Assert.IsFalse(HowdahDiagnostics.ClearanceWarrantsWarning(0f));
    }

    [TestMethod]
    public void ClearanceWarrantsWarning_Negative_Warns()
    {
        Assert.IsTrue(HowdahDiagnostics.ClearanceWarrantsWarning(-0.01f));
    }

    [TestMethod]
    public void ClearanceWarrantsWarning_NaN_Warns()
    {
        Assert.IsTrue(HowdahDiagnostics.ClearanceWarrantsWarning(float.NaN));
    }

    [TestMethod]
    public void Distance_KnownPoints_IsEuclidean()
    {
        Assert.AreEqual(5f, HowdahDiagnostics.Distance(0f, 0f, 0f, 3f, 4f, 0f), 1e-4f);
        Assert.AreEqual(0f, HowdahDiagnostics.Distance(1f, 2f, 3f, 1f, 2f, 3f), 1e-6f);
    }

    [TestMethod]
    public void Distance_NaNInput_ReturnsNaN()
    {
        Assert.IsTrue(float.IsNaN(HowdahDiagnostics.Distance(float.NaN, 0f, 0f, 0f, 0f, 0f)));
        Assert.IsTrue(float.IsNaN(HowdahDiagnostics.Distance(0f, 0f, 0f, 0f, 0f, float.NegativeInfinity)));
    }

    [TestMethod]
    public void Format_FiniteValue_UsesInvariantCultureWhateverTheThreadCulture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Assert.AreEqual("3.15", HowdahDiagnostics.Format(3.1499f, 2));
            Assert.AreEqual("-0.050", HowdahDiagnostics.Format(-0.05f, 3));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [TestMethod]
    public void Format_NonFinite_IsNotAvailable()
    {
        Assert.AreEqual("n/a", HowdahDiagnostics.Format(float.NaN, 2));
        Assert.AreEqual("n/a", HowdahDiagnostics.Format(float.PositiveInfinity, 2));
    }
}
