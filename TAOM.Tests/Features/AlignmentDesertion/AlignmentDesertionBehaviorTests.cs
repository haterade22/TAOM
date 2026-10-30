using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.AlignmentDesertion.Hooks;
using TAOM.Features.CoopInterop;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.AlignmentDesertion;

/// <summary>
/// The daily desertion pass asks the service's roster-independent gates before it copies a roster,
/// so the parties and garrisons of a Neutral kingdom (or with the feature off) cost no snapshot.
/// The IL-order test pins when the gate is asked; the two <c>ApplyDesertion</c> tests run the method on
/// a real <see cref="TroopRoster"/> and pin which way its answer is used and which owner it was asked
/// about. Each runs once for every combination of the two flags, the whole truth table (a player-owned or
/// AI-owned party, a player-owned or AI-owned garrison), so any other value for either flag in the gate
/// call or in the calculation call (a constant, the other flag, either flag negated, the two swapped)
/// fails on at least one row.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")] // runs a real TroopRoster and resolves TroopRoster members while reading the IL
public class AlignmentDesertionBehaviorTests
{
    private IAlignmentDesertionService _service = null!;
    private IPlayerControlQuery _playerControl = null!;
    private AlignmentDesertionBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<IAlignmentDesertionService>();
        _service.CalculateDesertion(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IReadOnlyList<DesertionTroopInfo>>())
            .Returns(new List<TroopDesertionResult>());
        _playerControl = Substitute.For<IPlayerControlQuery>();
        _sut = new AlignmentDesertionBehavior(_service, Substitute.For<IModLogger>(), _playerControl);
    }

    // A roster is plain managed code (CreateDummyTroopRoster, a regular troop with no hero), so ApplyDesertion
    // runs on it without a campaign. The service answers for the owner: the roster is only read when it says yes.
    private void ApplyDesertionOn(string troopId, int count, bool isPlayerOwned, bool isGarrison)
    {
        var roster = TroopRoster.CreateDummyTroopRoster();
        roster.AddToCounts(new CharacterObject { StringId = troopId }, count);
        var method = typeof(AlignmentDesertionBehavior).GetMethod("ApplyDesertion", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "AlignmentDesertionBehavior.ApplyDesertion");
        method!.Invoke(_sut, new object[] { roster, "gondor", isPlayerOwned, isGarrison, false });
    }

    // Only the owner and location under test are refused. Every other combination of the two flags answers yes, so
    // a gate call that passes a flag on wrongly is answered yes, calculates, and fails here.
    [DataTestMethod]
    [DataRow(true, false)] // a player-owned party
    [DataRow(false, true)] // an AI-owned garrison
    [DataRow(false, false)] // an AI-owned party, an AI lord party's daily call
    [DataRow(true, true)] // a player-owned garrison
    public void ApplyDesertion_OwnerCannotLoseAnyone_NeverCalculates(bool isPlayerOwned, bool isGarrison)
    {
        _service.ShouldEvaluate(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(true);
        _service.ShouldEvaluate("gondor", isPlayerOwned, isGarrison).Returns(false);

        ApplyDesertionOn("gondor_soldier", 5, isPlayerOwned, isGarrison);

        _service.DidNotReceiveWithAnyArgs().CalculateDesertion(default!, default, default, default!);
    }

    // Unstubbed, the substitute answers false, so the roster is read only when the gate was asked about exactly
    // this owner and location; the calculation must then be asked about the same two flags.
    [DataTestMethod]
    [DataRow(true, false)] // a player-owned party
    [DataRow(false, true)] // an AI-owned garrison
    [DataRow(false, false)] // an AI-owned party, an AI lord party's daily call
    [DataRow(true, true)] // a player-owned garrison
    public void ApplyDesertion_OwnerCanLoseTroops_CalculatesOverASnapshotOfTheRoster(bool isPlayerOwned, bool isGarrison)
    {
        _service.ShouldEvaluate("gondor", isPlayerOwned, isGarrison).Returns(true);

        ApplyDesertionOn("gondor_soldier", 5, isPlayerOwned, isGarrison);

        _service.Received(1).CalculateDesertion("gondor", isPlayerOwned, isGarrison,
            Arg.Is<IReadOnlyList<DesertionTroopInfo>>(rows =>
                rows.Count == 1 && rows[0].TroopId == "gondor_soldier" && rows[0].Count == 5 && !rows[0].IsHero));
    }

    // Pins the call ORDER in the IL of a straight-line method, not control flow: it proves the
    // ShouldEvaluate call is emitted before the roster walk, which in ApplyDesertion's straight-line
    // body means the gate is asked first.
    [TestMethod]
    public void ApplyDesertion_AsksTheServiceBeforeSnapshottingTheRoster_InIlOrder()
    {
        var method = typeof(AlignmentDesertionBehavior).GetMethod("ApplyDesertion", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "AlignmentDesertionBehavior.ApplyDesertion");

        var calls = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody().GetILAsByteArray()).ToList();
        int gate = calls.FindIndex(m => m.Name == "ShouldEvaluate" && m.DeclaringType == typeof(IAlignmentDesertionService));
        int walk = calls.FindIndex(m => m.Name == "GetTroopRoster");

        Assert.IsTrue(gate >= 0, "ShouldEvaluate is called");
        Assert.IsTrue(walk >= 0, "GetTroopRoster is called");
        Assert.IsTrue(gate < walk, $"ShouldEvaluate (call #{gate}) comes before GetTroopRoster (call #{walk})");
    }

    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    // Walks the hierarchy: a private setter or field declared on a base type is invisible from the derived type.
    private static void SetField(object target, string name, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field != null) { field.SetValue(target, value); return; }
        }
        Assert.Fail($"{target.GetType().Name}.{name} is gone; the test cannot stage the party.");
    }

    private static void SetProperty(object target, string name, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var setter = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                ?.GetSetMethod(nonPublic: true);
            if (setter != null) { setter.Invoke(target, new[] { value }); return; }
        }
        Assert.Fail($"{target.GetType().Name}.{name} lost its setter; the test cannot stage the party.");
    }

    // A battle owns its parties' rosters until it ends, and vanilla DesertionCampaignBehavior skips a party in a map
    // event. So a lord party or garrison in a map event stops before the owner check, and the same party out of
    // battle reaches it. The owner check (_playerControl.OfClan) is the first call that needs no live campaign;
    // Clan.PlayerClan, read right after it, throws without one, and the test ignores that.
    [DataTestMethod]
    [DataRow("OnDailyTickParty", true)]
    [DataRow("OnDailyTickParty", false)]
    [DataRow("OnDailyTickSettlement", true)]
    [DataRow("OnDailyTickSettlement", false)]
    public void DailyTick_PartyInAMapEvent_StopsBeforeTheOwnerCheck(string handler, bool inMapEvent)
    {
        _service.IsEnabled.Returns(true);
        var clan = Bare<Clan>();
        SetField(clan, "_kingdom", Bare<Kingdom>());
        var mobile = Bare<MobileParty>();
        var party = Bare<PartyBase>();
        SetProperty(mobile, nameof(MobileParty.Party), party);
        SetProperty(party, nameof(PartyBase.MemberRoster), Bare<TroopRoster>());
        if (inMapEvent)
        {
            var side = Bare<MapEventSide>();
            SetField(side, "_mapEvent", Bare<MapEvent>());
            SetField(party, "_mapEventSide", side);
        }

        object target;
        if (handler == "OnDailyTickParty")
        {
            var hero = Bare<Hero>();
            SetField(hero, "_clan", clan);
            var component = Substitute.For<PartyComponent>();
            component.Leader.Returns(hero);
            SetProperty(mobile, nameof(MobileParty.IsLordParty), true);
            SetField(mobile, "_partyComponent", component);
            target = mobile;
        }
        else
        {
            var garrison = Bare<GarrisonPartyComponent>();
            SetProperty(garrison, nameof(PartyComponent.MobileParty), mobile);
            var town = Bare<Town>();
            town.GarrisonPartyComponent = garrison;
            SetField(town, "_ownerClan", clan);
            var settlement = Bare<Settlement>();
            settlement.Town = town;
            target = settlement;
        }

        var method = typeof(AlignmentDesertionBehavior).GetMethod(handler, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "AlignmentDesertionBehavior." + handler);
        try
        {
            method!.Invoke(_sut, new[] { target });
        }
        catch (TargetInvocationException)
        {
            // Past the owner check Clan.PlayerClan needs a live campaign.
        }

        if (inMapEvent)
            _playerControl.DidNotReceiveWithAnyArgs().OfClan(default!);
        else
            _playerControl.Received(1).OfClan(clan);
    }
}
