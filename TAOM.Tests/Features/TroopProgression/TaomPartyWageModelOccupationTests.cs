using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TAOM.Features.TroopProgression.Models;

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
}
