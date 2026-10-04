using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CulturalFeats;
using TAOM.Features.CulturalFeats.Models;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CulturalFeats;

/// <summary>
/// The party speed model runs on every speed recompute of every party; it walks the roster for the
/// Rohan infantry penalty only when the service says that penalty can apply to the party's culture.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")] // loads TroopRoster and MobileParty members
public class TaomPartySpeedModelTests
{
    [TestMethod]
    public void CountMountedAndTotal_NotNeeded_ReturnsZeroesWithoutReadingTheRoster()
    {
        var helper = typeof(TaomPartySpeedModel).GetMethod("CountMountedAndTotal", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(helper, "TaomPartySpeedModel.CountMountedAndTotal");

        // A null roster proves it was not read: any read would throw.
        var result = helper.Invoke(null, new object?[] { null, false });

        Assert.AreEqual((0, 0), result);
    }

    [TestMethod]
    public void CountMountedAndTotal_Needed_ReadsTheRoster()
    {
        var helper = typeof(TaomPartySpeedModel).GetMethod("CountMountedAndTotal", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(helper, "TaomPartySpeedModel.CountMountedAndTotal");

        // The mirror of the test above: when the penalty can apply, the null roster is read and throws.
        var ex = Assert.ThrowsException<TargetInvocationException>(() => helper.Invoke(null, new object?[] { null, true }));

        Assert.IsInstanceOfType(ex.InnerException, typeof(NullReferenceException));
    }

    // Pins the call ORDER in the IL of CalculateFinalSpeed's straight-line body, not control flow.
    [TestMethod]
    public void CalculateFinalSpeed_AsksTheServiceBeforeCountingTheRoster_InIlOrder()
    {
        var method = typeof(TaomPartySpeedModel).GetMethod(nameof(TaomPartySpeedModel.CalculateFinalSpeed));
        Assert.IsNotNull(method, "TaomPartySpeedModel.CalculateFinalSpeed");

        var calls = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody().GetILAsByteArray()).ToList();
        int ask = calls.FindIndex(m => m.Name == "NeedsMountedCount" && m.DeclaringType == typeof(ICulturalFeatsService));
        int count = calls.FindIndex(m => m.Name == "CountMountedAndTotal");

        Assert.IsTrue(ask >= 0, "NeedsMountedCount is called");
        Assert.IsTrue(count >= 0, "CountMountedAndTotal is called");
        Assert.IsTrue(ask < count, $"NeedsMountedCount (call #{ask}) comes before CountMountedAndTotal (call #{count})");
    }
}
