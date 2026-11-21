using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TAOM.Features.TroopProgression.Models;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.TroopProgression;

/// <summary>
/// Caravan guards pay the regular wage (vanilla applies the 1.5 wage factor to Occupation.Mercenary only)
/// but still count as mercenaries for the recruitment cost and recruitment perks.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")] // loads TaleWorlds.CampaignSystem types
public class TaomPartyWageModelOccupationTests
{
    private static bool Call(string name, Occupation occupation)
    {
        var method = typeof(TaomPartyWageModel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "TaomPartyWageModel." + name);
        return (bool)method.Invoke(null, new object[] { occupation })!;
    }

    [TestMethod]
    [DataRow(Occupation.Mercenary, true)]
    [DataRow(Occupation.Gangster, true)]
    [DataRow(Occupation.CaravanGuard, false)]
    [DataRow(Occupation.Soldier, false)]
    public void IsMercenaryWageOccupation_CaravanGuardPaysRegularWage(Occupation occupation, bool expected)
        => Assert.AreEqual(expected, Call("IsMercenaryWageOccupation", occupation));

    [TestMethod]
    [DataRow(Occupation.Mercenary, true)]
    [DataRow(Occupation.Gangster, true)]
    [DataRow(Occupation.CaravanGuard, true)]
    [DataRow(Occupation.Soldier, false)]
    public void IsMercenaryOccupation_RecruitmentStillCountsCaravanGuard(Occupation occupation, bool expected)
        => Assert.AreEqual(expected, Call("IsMercenaryOccupation", occupation));

    // A swap of the two predicates at a call site would pass both tests above.
    [TestMethod]
    [DataRow("GetCharacterWage", "IsMercenaryWageOccupation", "IsMercenaryOccupation")]
    [DataRow("GetTroopRecruitmentCost", "IsMercenaryOccupation", "IsMercenaryWageOccupation")]
    [DataRow("ResolveBuyerRecruitmentPerks", "IsMercenaryOccupation", "IsMercenaryWageOccupation")]
    public void CallSite_UsesTheRightPredicate(string caller, string expected, string forbidden)
    {
        var method = typeof(TaomPartyWageModel).GetMethod(caller,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        Assert.IsNotNull(method, "TaomPartyWageModel." + caller);

        var calls = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()).ToList();

        Assert.IsTrue(calls.Any(m => m.Name == expected), $"{caller} calls {expected}");
        Assert.IsFalse(calls.Any(m => m.Name == forbidden), $"{caller} does not call {forbidden}");
    }
}
