using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.EyeOfSauron;

/// <summary>
/// #765 M8, E02: <c>ReadSight</c> runs every campaign frame of a hunt. It must find the hunter with the locator search
/// around the player, never the linear <c>CampaignObjectManager.Find</c> walk over every party, and its reading must
/// be a value type so the frame allocates nothing. The adapter is engine-bound, so its IL is the testable surface.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HunterPartyAdapterIlTests
{
    private static MethodBase[] CallsOf(string methodName)
    {
        var method = typeof(HunterPartyAdapter).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)!;
        return IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()).ToArray();
    }

    [TestMethod]
    public void ReadSight_SearchesAroundThePlayer_NeverTheLinearFind()
    {
        var calls = CallsOf(nameof(HunterPartyAdapter.ReadSight));

        Assert.IsTrue(calls.Any(m => m.DeclaringType == typeof(MobileParty) && m.Name == nameof(MobileParty.StartFindingLocatablesAroundPosition)),
            "ReadSight must search the locator grid around the player");
        Assert.IsFalse(calls.Any(m => m.DeclaringType == typeof(CampaignObjectManager) && m.Name.StartsWith("Find")),
            "ReadSight must not walk every party with CampaignObjectManager.Find each frame");
        Assert.IsFalse(calls.Any(m => m.DeclaringType == typeof(HunterPartyAdapter) && m.Name == "FindParty"),
            "ReadSight must not call FindParty (the linear walk) each frame");
    }

    [TestMethod]
    public void HuntSightReading_IsAValueType_SoAFrameAllocatesNothing() =>
        Assert.IsTrue(typeof(HuntSightReading).IsValueType);

    // Codex F4: the search radius comes from the widened LocatorRadius, never the bare product.
    [TestMethod]
    public void ReadSight_SearchesWithTheWidenedLocatorRadius() =>
        Assert.IsTrue(CallsOf(nameof(HunterPartyAdapter.ReadSight)).Any(m =>
                m.DeclaringType == typeof(HunterPartyAdapter) && m.Name == nameof(HunterPartyAdapter.LocatorRadius)),
            "ReadSight must size its locator search with LocatorRadius");

    // Codex F1: an attached army member counts as in an army, as ReadStepState already reads it.
    [TestMethod]
    public void ReadParty_ReadsTheArmyAttachment() =>
        Assert.IsTrue(CallsOf(nameof(HunterPartyAdapter.ReadParty)).Any(m =>
                m.DeclaringType == typeof(MobileParty) && m.Name == "get_" + nameof(MobileParty.AttachedTo)),
            "ReadParty must read AttachedTo as well as Army");

    // The maintainer, 2026-11-23: the hunt waits out the 5-day safe-passage oath too. NoAttackBarterable writes it to
    // the hunter's map faction (NotAttackableByPlayerUntilTime), and vanilla AI never reads it, so the hold must.
    [TestMethod]
    public void HoldsOff_ReadsTheSafePassageOathOnTheHuntersFaction()
    {
        var method = typeof(HunterPartyAdapter).GetMethod("HoldsOff", BindingFlags.NonPublic | BindingFlags.Static)!;
        var calls = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()).ToArray();

        Assert.IsTrue(calls.Any(m => m.Name == "get_" + nameof(MobileParty.MapFaction)), "HoldsOff must read the hunter's map faction");
        Assert.IsTrue(calls.Any(m => m.DeclaringType == typeof(IFaction) && m.Name == "get_" + nameof(IFaction.NotAttackableByPlayerUntilTime)),
            "HoldsOff must read the faction's safe-passage oath");
        Assert.IsTrue(calls.Any(m => m.Name == "get_" + nameof(MobilePartyAi.DoNotAttackMainPartyUntil)),
            "HoldsOff must still read the party's own promise");
    }
}

/// <summary>
/// Codex F4: vanilla's locator skips a party at <c>d^2 &gt;= r^2</c> (LocatorGrid.cs:131, 139), so a search of exactly
/// 1.5 S would miss a hunter at exactly 1.5 S, which the service's band accepts. The broad phase searches a little
/// wider; the service keeps the exact <c>d &lt;= 1.5 S</c> filter. Pure arithmetic, so no game is needed.
/// </summary>
[TestClass]
public class HunterPartyAdapterLocatorRadiusTests
{
    [DataTestMethod]
    [DataRow(10f)]
    [DataRow(1f)]
    [DataRow(37.5f)]
    public void LocatorRadius_ReachesPastTheExactBand_SoTheBoundaryIsFound(float sight)
    {
        float band = 1.5f * sight;
        float radius = HunterPartyAdapter.LocatorRadius(1.5f, sight);

        Assert.IsTrue(band * band < radius * radius, $"S={sight}: a hunter at exactly 1.5 S must pass d^2 < r^2");
        Assert.IsTrue(radius <= band * 1.05f, $"S={sight}: the widening stays slight ({radius} against {band})");
    }
}
