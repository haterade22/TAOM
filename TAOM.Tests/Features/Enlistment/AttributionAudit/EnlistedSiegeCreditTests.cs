using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Quests;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace TAOM.Tests.Features.Enlistment.AttributionAudit;

/// <summary>
/// #1015: an enlisted soldier is credited for a siege when his own party is on the capturing side of
/// the map event, not when his commander happens to lead that side. On v1.4.8 SiegeCompleted is raised
/// inside MapEvent.FinalizeEventAux (MapEvent.cs:2093 and :2115) before MapEventSide.HandleMapEventEnd
/// clears the sides (:2158), so the sides are still populated when the adapter and the quest read them.
/// Each test stages bare engine objects through reflection.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class EnlistedSiegeCreditTests
{
    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static void SetField(object target, string name, object? value)
    {
        var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{name} is gone; the test cannot stage the siege.");
        field!.SetValue(target, value);
    }

    private static void SetProperty(object target, string name, object? value)
    {
        var setter = target.GetType().GetProperty(name)?.GetSetMethod(nonPublic: true);
        Assert.IsNotNull(setter, $"{target.GetType().Name}.{name} lost its setter; the test cannot stage the siege.");
        setter!.Invoke(target, new[] { value });
    }

    private static MobileParty Mobile(string id, out PartyBase partyBase)
    {
        var mobile = Bare<MobileParty>();
        mobile.StringId = id;
        partyBase = Bare<PartyBase>();
        SetProperty(partyBase, nameof(PartyBase.MobileParty), mobile);
        SetProperty(mobile, nameof(MobileParty.Party), partyBase);
        return mobile;
    }

    private static MapEventSide SideOf(MapEvent battle, params PartyBase[] parties)
    {
        var members = new MBList<MapEventParty>();
        var side = Bare<MapEventSide>();
        foreach (var partyBase in parties)
        {
            var member = Bare<MapEventParty>();
            SetProperty(member, nameof(MapEventParty.Party), partyBase);
            members.Add(member);
            SetField(partyBase, "_mapEventSide", side);
        }
        SetField(side, "_battleParties", members);
        SetField(side, "_mapEvent", battle);
        return side;
    }

    /// <summary>One siege: the attacker side holds the given parties, the defender side the settlement's party.</summary>
    private static (MapEventSide attackers, MapEventSide defenders) Siege(
        PartyBase settlementParty, params PartyBase[] attackerParties)
    {
        var battle = Bare<MapEvent>();
        var attackers = SideOf(battle, attackerParties);
        var defenders = SideOf(battle, settlementParty);
        SetField(battle, "_sides", new[] { defenders, attackers });
        return (attackers, defenders);
    }

    private static PartyBase SoldierParty(out MobileParty soldier)
    {
        soldier = Mobile("main", out var soldierBase);
        return soldierBase;
    }

    // ---------------------------------------------------------------- adapter

    [TestMethod]
    public void Adapter_SoldierInAnArmyLedByAnotherLord_IsCredited()
    {
        var soldierBase = SoldierParty(out _);
        var leader = Mobile("army_leader", out var leaderBase); // not the commander
        Siege(Bare<PartyBase>(), leaderBase, soldierBase);

        Assert.IsTrue(WarEventSnapshotAdapter.PlayerFought(leader, soldierBase));
    }

    [TestMethod]
    public void Adapter_CommanderLedSiegeTheSoldierSatOut_IsNotCredited()
    {
        var soldierBase = SoldierParty(out _);
        var commander = Mobile("commander", out var commanderBase);
        Siege(Bare<PartyBase>(), commanderBase);

        Assert.IsFalse(WarEventSnapshotAdapter.PlayerFought(commander, soldierBase));
    }

    [TestMethod]
    public void Adapter_NotEnlistedPlayerInAnArmyLedByAnotherLord_IsCredited()
    {
        var playerBase = SoldierParty(out _);
        var leader = Mobile("army_leader", out var leaderBase);
        Siege(Bare<PartyBase>(), leaderBase, playerBase);

        Assert.IsTrue(WarEventSnapshotAdapter.PlayerFought(leader, playerBase));
    }

    [TestMethod]
    public void Adapter_NotEnlistedPlayerWhoSatItOut_IsNotCredited()
    {
        var playerBase = SoldierParty(out _);
        var leader = Mobile("army_leader", out var leaderBase);
        Siege(Bare<PartyBase>(), leaderBase);

        Assert.IsFalse(WarEventSnapshotAdapter.PlayerFought(leader, playerBase));
    }

    [TestMethod]
    public void Adapter_SoldierOnTheDefendingSide_IsNotCredited()
    {
        var soldierBase = SoldierParty(out _);
        var leader = Mobile("attacker", out var leaderBase);
        Siege(soldierBase, leaderBase); // the soldier defends the walls

        Assert.IsFalse(WarEventSnapshotAdapter.PlayerFought(leader, soldierBase));
    }

    // ---------------------------------------------------------------- career quest

    [TestMethod]
    public void Quest_SoldierOnTheSideOppositeTheSettlement_IsCredited()
    {
        var soldierBase = SoldierParty(out _);
        var settlementParty = Bare<PartyBase>();
        var leader = Mobile("army_leader", out var leaderBase);
        Siege(settlementParty, leaderBase, soldierBase);
        var settlement = Bare<Settlement>();
        SetProperty(settlement, nameof(Settlement.Party), settlementParty);

        Assert.IsTrue(CareerQuest.PlayerFoughtOnCapturingSide(settlement, soldierBase));
    }

    [TestMethod]
    public void Quest_SoldierNotInTheEvent_IsNotCredited()
    {
        var soldierBase = SoldierParty(out _);
        var settlementParty = Bare<PartyBase>();
        var commander = Mobile("commander", out var commanderBase);
        Siege(settlementParty, commanderBase);
        var settlement = Bare<Settlement>();
        SetProperty(settlement, nameof(Settlement.Party), settlementParty);

        Assert.IsFalse(CareerQuest.PlayerFoughtOnCapturingSide(settlement, soldierBase));
    }

    [TestMethod]
    public void Quest_SoldierOnTheSettlementsOwnSide_IsNotCredited()
    {
        var soldierBase = SoldierParty(out _);
        var settlementParty = Bare<PartyBase>();
        var leader = Mobile("attacker", out var leaderBase);
        Siege(settlementParty, leaderBase);
        var settlement = Bare<Settlement>();
        SetProperty(settlement, nameof(Settlement.Party), settlementParty);
        var defenders = settlementParty.MapEventSide;
        SetField(soldierBase, "_mapEventSide", defenders);

        Assert.IsFalse(CareerQuest.PlayerFoughtOnCapturingSide(settlement, soldierBase));
    }

    [TestMethod]
    public void Quest_SoldierInAnotherBattleEntirely_IsNotCredited()
    {
        var soldierBase = SoldierParty(out _);
        var elsewhere = Mobile("elsewhere", out var elsewhereBase);
        Siege(Bare<PartyBase>(), elsewhereBase, soldierBase); // a different map event
        var settlementParty = Bare<PartyBase>();
        var leader = Mobile("army_leader", out var leaderBase);
        Siege(settlementParty, leaderBase);
        var settlement = Bare<Settlement>();
        SetProperty(settlement, nameof(Settlement.Party), settlementParty);

        Assert.IsFalse(CareerQuest.PlayerFoughtOnCapturingSide(settlement, soldierBase));
    }

    [TestMethod]
    public void QuestSiegeHandler_CreditsEveryPlayerByCapturingSide_NotOnlyAnEnlistedOne()
    {
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "Main", "Features", "CareerSystem", "Quests", "CareerQuest.cs"));
        var start = src.IndexOf("private void OnSettlementOwnerChanged", System.StringComparison.Ordinal);
        var end = src.IndexOf("internal static bool PlayerFoughtOnCapturingSide", System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0 && end > start, "handler or helper moved");
        var body = src.Substring(start, end - start);

        StringAssert.Contains(body, "Campaign.Current?.MainParty?.Party");
        Assert.IsFalse(body.Contains("IsEnlisted"), "the siege credit must not be gated on enlistment");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln");
    }

    // ---------------------------------------------------------------- DefeatEnemyLords

    private static PartyBase PartyOnly(string id)
    {
        Mobile(id, out var partyBase);
        return partyBase;
    }

    [TestMethod]
    public void Capture_ByAnotherPartyOnThePlayersSide_IsCredited()
    {
        var playerBase = SoldierParty(out _);
        var captor = PartyOnly("army_member");
        Siege(Bare<PartyBase>(), captor, playerBase);

        Assert.IsTrue(CareerQuest.IsPlayersCapture(captor, playerBase, isCommanderParty: false));
    }

    [TestMethod]
    public void Capture_ByTheCommanderInABattleThePlayerSatOut_IsNotCredited()
    {
        var playerBase = SoldierParty(out _);
        var commander = PartyOnly("commander");
        Siege(Bare<PartyBase>(), commander);

        Assert.IsFalse(CareerQuest.IsPlayersCapture(commander, playerBase, isCommanderParty: true));
    }

    [TestMethod]
    public void Capture_ByAPartyOnTheOtherSide_IsNotCredited()
    {
        var playerBase = SoldierParty(out _);
        var enemy = PartyOnly("enemy");
        var (_, defenders) = Siege(enemy, PartyOnly("army_member"), playerBase);

        Assert.IsNotNull(defenders);
        Assert.IsFalse(CareerQuest.IsPlayersCapture(enemy, playerBase, isCommanderParty: false));
    }

    [TestMethod]
    public void Capture_OutsideAnyBattle_FallsBackToThePlayerOrTheCommander()
    {
        var playerBase = SoldierParty(out _);
        var commander = PartyOnly("commander");
        var stranger = PartyOnly("stranger");

        Assert.IsTrue(CareerQuest.IsPlayersCapture(playerBase, playerBase, isCommanderParty: false));
        Assert.IsTrue(CareerQuest.IsPlayersCapture(commander, playerBase, isCommanderParty: true));
        Assert.IsFalse(CareerQuest.IsPlayersCapture(stranger, playerBase, isCommanderParty: false));
    }

    // ---------------------------------------------------------------- convergence pins and gates

    private static string Source(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(parts).ToArray()));

    [TestMethod]
    public void SettlementCapture_NonSiegeOwnerChangeWithSidesPopulated_IsNotCredited()
    {
        var playerBase = SoldierParty(out _);
        var settlementParty = Bare<PartyBase>();
        Siege(settlementParty, PartyOnly("army_member"), playerBase);
        var settlement = Bare<Settlement>();
        SetProperty(settlement, nameof(Settlement.Party), settlementParty);
        var mainHero = Bare<Hero>();
        var other = Bare<Hero>();

        Assert.IsTrue(CareerQuest.CreditsSettlementCapture(other, mainHero, settlement,
            TaleWorlds.CampaignSystem.Actions.ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege, playerBase));
        Assert.IsFalse(CareerQuest.CreditsSettlementCapture(other, mainHero, settlement,
            TaleWorlds.CampaignSystem.Actions.ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByBarter, playerBase));
    }

    [TestMethod]
    public void CombatIssueQuest_CountsACaptureThroughTheCareerQuestRule()
    {
        var src = Source("Main", "Features", "LotrIssues", "Templates", "CombatLotrIssue.cs");

        StringAssert.Contains(src, "CareerQuest.IsPlayersCapture(capturer, Campaign.Current?.MainParty?.Party, false)");
    }

    [TestMethod]
    public void CareerQuest_NeverReadsTheThrowingPartyBaseMainParty()
    {
        var src = Source("Main", "Features", "CareerSystem", "Quests", "CareerQuest.cs");

        Assert.IsFalse(src.Contains("PartyBase.MainParty"), "use Campaign.Current?.MainParty?.Party");
    }

    [TestMethod]
    public void FromSiege_KeepsTheSideRuleBesideTheKingdomTest()
    {
        var src = Source("Main", "Adapters", "WarEventSnapshotAdapter.cs");

        StringAssert.Contains(src, "|| PlayerFought(");
    }

    [TestMethod]
    public void OnHeroPrisonerTaken_CallsIsPlayersCapture()
    {
        var src = Source("Main", "Features", "CareerSystem", "Quests", "CareerQuest.cs");
        var start = src.IndexOf("private void OnHeroPrisonerTaken", System.StringComparison.Ordinal);

        Assert.IsTrue(start >= 0, "handler moved");
        StringAssert.Contains(src.Substring(start, 700), "IsPlayersCapture(");
    }
}
