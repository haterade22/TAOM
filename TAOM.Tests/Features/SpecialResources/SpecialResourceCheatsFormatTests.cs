using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Cheats;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// The console echo is the whole UX of the cheat — if it says "(cap 500)" when a clamp actually
/// fired, the command lied about what it did. These cover every branch of the message, including
/// the state a happy path can't reach: a stored balance ABOVE a cap that was lowered after the
/// save was written.
/// </summary>
[TestClass]
public class SpecialResourceCheatsFormatTests
{
    private static ResourceGrantResult Granted(float before, float after, float cap = 500f) =>
        new(true, "war_spoils", "War Spoils", before, after, cap);

    [TestMethod]
    public void FormatResult_NormalGrant_ReportsCapWithoutClaimingAClamp()
    {
        var message = SpecialResourceCheats.FormatResult(Granted(120f, 320f), 200f);

        Assert.AreEqual("War Spoils: 120 -> 320 (cap 500)", message);
    }

    [TestMethod]
    public void FormatResult_GrantExceedingCap_ReportsClamp()
    {
        var message = SpecialResourceCheats.FormatResult(Granted(480f, 500f), 999999f);

        StringAssert.Contains(message, "clamped at cap 500");
    }

    [TestMethod]
    public void FormatResult_NegativeGrantOnLegacyOverCapBalance_ReportsClamp()
    {
        // A save written before the cap was lowered to 500: 550 − 10 clamps to 500, not 540.
        // Keying the report on `amount > 0` hid this case entirely.
        var message = SpecialResourceCheats.FormatResult(Granted(550f, 500f), -10f);

        StringAssert.Contains(message, "clamped at cap 500");
    }

    [TestMethod]
    public void FormatResult_NegativeGrantBelowZero_ReportsFloor()
    {
        var message = SpecialResourceCheats.FormatResult(Granted(40f, 0f), -500f);

        StringAssert.Contains(message, "floored at 0");
    }

    [TestMethod]
    public void FormatResult_NegativeGrantLandingExactlyOnCap_DoesNotClaimAClamp()
    {
        // 700 − 200 == 500 by arithmetic alone; no clamp fired, so none should be reported.
        var message = SpecialResourceCheats.FormatResult(Granted(700f, 500f), -200f);

        Assert.AreEqual("War Spoils: 700 -> 500 (cap 500)", message);
    }

    [TestMethod]
    public void FormatResult_Unresolved_ReportsNoResourceInsteadOfANumericChange()
    {
        var message = SpecialResourceCheats.FormatResult(ResourceGrantResult.None, 500f);

        StringAssert.Contains(message, "grants no special resource");
    }
}
