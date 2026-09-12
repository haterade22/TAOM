using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FiefGranting;

/// <summary>
/// Drift-guard for the siege participation recorder (#565). <c>FiefGrantingCampaignBehavior</c>
/// reads a handful of engine members at the end of every siege battle and listens to two campaign
/// events; each assertion below names the way the feature goes quiet if that member moves. The
/// recorder is not a Harmony patch, so nothing would throw at module load: the record would simply
/// stop being written and every election would run on the holdings terms alone.
/// </summary>
[TestClass]
public class FiefSiegeParticipationBindingTests
{
    private const string MapEventTypeName = "TaleWorlds.CampaignSystem.MapEvents.MapEvent";
    private const string MapEventPartyTypeName = "TaleWorlds.CampaignSystem.MapEvents.MapEventParty";
    private const string MobilePartyTypeName = "TaleWorlds.CampaignSystem.Party.MobileParty";
    private const string PartyBaseTypeName = "TaleWorlds.CampaignSystem.Party.PartyBase";
    private const string CampaignEventsTypeName = "TaleWorlds.CampaignSystem.CampaignEvents";
    private const string DetailEnumTypeName =
        "TaleWorlds.CampaignSystem.Actions.ChangeOwnerOfSettlementAction+ChangeOwnerOfSettlementDetail";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MapEvent_StillExposesTheSideAndOutcomeMembersTheRecorderReads()
    {
        RequireGame();

        var mapEventType = AccessTools.TypeByName(MapEventTypeName);
        Assert.IsNotNull(mapEventType, MapEventTypeName + " did not resolve.");

        foreach (var name in new[] { "WinningSide", "IsSiegeAssault", "IsSallyOut", "IsBlockadeSallyOut", "MapEventSettlement" })
        {
            Assert.IsNotNull(
                AccessTools.PropertyGetter(mapEventType, name),
                $"MapEvent.{name} is gone; the recorder could no longer tell a captured settlement from any other battle.");
        }

        var partiesOnSide = AccessTools.Method(mapEventType, "PartiesOnSide");
        Assert.IsNotNull(partiesOnSide, "MapEvent.PartiesOnSide is gone; the recorder has no way to enumerate the winning side.");
        var parameters = partiesOnSide.GetParameters();
        Assert.AreEqual(1, parameters.Length, "MapEvent.PartiesOnSide no longer takes exactly one parameter.");
        Assert.AreEqual("BattleSideEnum", parameters[0].ParameterType.Name, "MapEvent.PartiesOnSide no longer takes a BattleSideEnum.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MapEventParty_ContributionToBattle_IsStillAnIntGetter()
    {
        RequireGame();

        var partyType = AccessTools.TypeByName(MapEventPartyTypeName);
        Assert.IsNotNull(partyType, MapEventPartyTypeName + " did not resolve.");

        var contribution = AccessTools.PropertyGetter(partyType, "ContributionToBattle");
        Assert.IsNotNull(contribution, "MapEventParty.ContributionToBattle is gone; every share would read as absent.");
        Assert.AreEqual(typeof(int), contribution.ReturnType,
            "MapEventParty.ContributionToBattle is no longer an int; the record sums ints per clan.");

        Assert.IsNotNull(AccessTools.PropertyGetter(partyType, "Party"),
            "MapEventParty.Party is gone; the recorder cannot map a battle party to its clan.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BattleParties_StillResolveToAClan()
    {
        RequireGame();

        var mobilePartyType = AccessTools.TypeByName(MobilePartyTypeName);
        var partyBaseType = AccessTools.TypeByName(PartyBaseTypeName);
        Assert.IsNotNull(mobilePartyType, MobilePartyTypeName + " did not resolve.");
        Assert.IsNotNull(partyBaseType, PartyBaseTypeName + " did not resolve.");

        var actualClan = AccessTools.PropertyGetter(mobilePartyType, "ActualClan");
        Assert.IsNotNull(actualClan, "MobileParty.ActualClan is gone; the recorder's primary clan lookup fails.");
        Assert.AreEqual("Clan", actualClan.ReturnType.Name, "MobileParty.ActualClan no longer returns a Clan.");

        // Mercenary clans are skipped at record time to mirror DetermineInitialCandidates' ballot filter.
        Assert.IsNotNull(AccessTools.PropertyGetter(actualClan.ReturnType, "IsUnderMercenaryService"),
            "Clan.IsUnderMercenaryService is gone; a mercenary company could pin the top share and deny every eligible clan the full bonus.");

        Assert.IsNotNull(AccessTools.PropertyGetter(partyBaseType, "MobileParty"),
            "PartyBase.MobileParty is gone; the recorder cannot reach ActualClan from a battle party.");
        // MobileParty.Owner, not PartyBase.Owner: the latter is a throwing computed getter that
        // PartyOwnerGetterBanTests forbids across the whole assembly.
        var owner = AccessTools.PropertyGetter(mobilePartyType, "Owner");
        Assert.IsNotNull(owner, "MobileParty.Owner is gone; the recorder's fallback clan lookup fails.");
        Assert.AreEqual("Hero", owner.ReturnType.Name, "MobileParty.Owner no longer returns a Hero.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CampaignEvents_StillDispatchTheTwoEventsTheRecorderListensTo()
    {
        RequireGame();

        var eventsType = AccessTools.TypeByName(CampaignEventsTypeName);
        Assert.IsNotNull(eventsType, CampaignEventsTypeName + " did not resolve.");

        var mapEventEnded = AccessTools.PropertyGetter(eventsType, "MapEventEnded");
        Assert.IsNotNull(mapEventEnded, "CampaignEvents.MapEventEnded is gone; the record would never be written.");
        var endedArgs = mapEventEnded.ReturnType.GetGenericArguments();
        Assert.AreEqual(1, endedArgs.Length, "CampaignEvents.MapEventEnded no longer carries exactly one argument.");
        Assert.AreEqual("MapEvent", endedArgs[0].Name, "CampaignEvents.MapEventEnded no longer carries the MapEvent.");

        var ownerChanged = AccessTools.PropertyGetter(eventsType, "OnSettlementOwnerChangedEvent");
        Assert.IsNotNull(ownerChanged, "CampaignEvents.OnSettlementOwnerChangedEvent is gone; a stale record would never be cleared.");
        var changedArgs = ownerChanged.ReturnType.GetGenericArguments();
        Assert.AreEqual(6, changedArgs.Length,
            "CampaignEvents.OnSettlementOwnerChangedEvent no longer carries six arguments; the handler signature would not bind.");
        Assert.AreEqual("Settlement", changedArgs[0].Name, "the first owner-changed argument is no longer the Settlement.");
        Assert.AreEqual("ChangeOwnerOfSettlementDetail", changedArgs[5].Name,
            "the last owner-changed argument is no longer the transfer detail; the BySiege keep/forget split would be blind.");

        var detailType = AccessTools.TypeByName(DetailEnumTypeName);
        Assert.IsNotNull(detailType, DetailEnumTypeName + " did not resolve.");
        Assert.IsTrue(System.Enum.GetNames(detailType).Contains("BySiege"),
            "ChangeOwnerOfSettlementDetail.BySiege is gone; every capture would clear its own record.");
    }
}
