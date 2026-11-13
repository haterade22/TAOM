using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.KingdomAllegiance;

namespace TAOM.Tests.Features.KingdomAllegiance;

/// <summary>
/// Kingdom Allegiance (docs/features/kingdom-allegiance.md, #1111): an AI clan never joins, defects to or hires on with a realm of the
/// opposed side, and a new campaign starts with no Free-vs-Evil friendships. Sides are read from the faction id: "free" is Free,
/// "evil" is Evil, anything else is Neutral.
/// </summary>
[TestClass]
public class KingdomAllegianceServiceTests
{
    private static readonly SideIds Free = new("free", "c");
    private static readonly SideIds Evil = new("evil", "c");
    private static readonly SideIds Neutral = new("neutral", "c");

    private IKingdomAllegianceSettingsProvider _settings = null!;
    private IAlignmentService _alignment = null!;
    private ICoopSessionProvider _coop = null!;
    private IModLogger _logger = null!;
    private OutcomeLog _outcomes = null!;
    private KingdomAllegianceService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IKingdomAllegianceSettingsProvider>();
        _settings.Enabled.Returns(true);
        _alignment = Substitute.For<IAlignmentService>();
        _alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>()).Returns(call => call.ArgAt<string>(0) switch
        {
            "free" => FactionSide.Free,
            "evil" => FactionSide.Evil,
            _ => FactionSide.Neutral,
        });
        _coop = Substitute.For<ICoopSessionProvider>();
        _logger = Substitute.For<IModLogger>();
        _outcomes = new OutcomeLog(_logger);
        _service = new KingdomAllegianceService(_settings, _alignment, _coop, _outcomes);
    }

    private static AllegianceFacts AiJoin(SideIds clan, SideIds kingdom) =>
        new(clan, kingdom, PlayerControl.NotPlayer, PlayerControl.NotPlayer, AlreadyServes: false);

    // --- the join veto --------------------------------------------------------------------------------------------------

    [TestMethod]
    public void JoinVeto_AFreeClanAndAnEvilRealm_IsVetoed_EitherWayRound()
    {
        Assert.AreEqual(KingdomAllegianceService.VetoScore, _service.JoinVeto(AiJoin(Free, Evil)));
        Assert.AreEqual(KingdomAllegianceService.VetoScore, _service.JoinVeto(AiJoin(Evil, Free)));
    }

    [TestMethod]
    public void JoinVeto_IsVanillasOwnVetoValue()
    {
        Assert.AreEqual(-100000000f, KingdomAllegianceService.VetoScore);
    }

    [DataTestMethod]
    [DataRow("free", "free")]
    [DataRow("evil", "evil")]
    [DataRow("neutral", "free")]
    [DataRow("neutral", "evil")]
    [DataRow("free", "neutral")]
    [DataRow("evil", "neutral")]
    [DataRow("neutral", "neutral")]
    public void JoinVeto_SidesThatAreNotOpposed_LeaveVanillasScore(string clan, string kingdom)
    {
        Assert.IsNull(_service.JoinVeto(AiJoin(new SideIds(clan, "c"), new SideIds(kingdom, "c"))));
    }

    [TestMethod]
    public void JoinVeto_NoFacts_LeavesVanillasScore()
    {
        Assert.IsNull(_service.JoinVeto(null));
    }

    [DataTestMethod]
    [DataRow(PlayerControl.Player)]
    [DataRow(PlayerControl.Unknown)]
    public void JoinVeto_APlayerClan_OrOneInDoubt_KeepsVanillasFreedom(PlayerControl clan)
    {
        Assert.IsNull(_service.JoinVeto(AiJoin(Free, Evil) with { Clan = clan }));
    }

    [DataTestMethod]
    [DataRow(PlayerControl.Player)]
    [DataRow(PlayerControl.Unknown)]
    public void JoinVeto_ARealmAPlayerLeads_OrOneInDoubt_KeepsVanillasFreedom(PlayerControl leader)
    {
        Assert.IsNull(_service.JoinVeto(AiJoin(Evil, Free) with { KingdomLeader = leader }));
    }

    [TestMethod]
    public void JoinVeto_ACoopPeerDeferringToTheHost_LeavesVanillasScore()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.IsNull(_service.JoinVeto(AiJoin(Free, Evil)));
    }

    [TestMethod]
    public void JoinVeto_TheSwitchOff_LeavesVanillasScore()
    {
        _settings.Enabled.Returns(false);

        Assert.IsNull(_service.JoinVeto(AiJoin(Free, Evil)));
    }

    [TestMethod]
    public void JoinVeto_CountsEachVeto_AndNothingElse()
    {
        _service.JoinVeto(AiJoin(Free, Evil));
        _service.JoinVeto(AiJoin(Evil, Free));
        _service.JoinVeto(AiJoin(Free, Free));

        Assert.AreEqual(2, _outcomes.Counter(KingdomAllegianceService.VetoCounterId).Passes);
    }

    [TestMethod]
    public void JoinVeto_TheClansOwnRealm_LeavesVanillasScore_AndIsNotCounted()
    {
        // Vanilla's mercenary leave score subtracts the join score of the clan's own realm: a veto there would push an opposed
        // mercenary that already serves out of its realm (Mike: clans that already serve stay).
        Assert.IsNull(_service.JoinVeto(AiJoin(Evil, Free) with { AlreadyServes = true }));
        Assert.AreEqual(0, _outcomes.Counter(KingdomAllegianceService.VetoCounterId).Passes);
    }

    [TestMethod]
    public void JoinVeto_ThePlayerChecksComeBeforeTheSides()
    {
        _service.JoinVeto(AiJoin(Free, Evil) with { Clan = PlayerControl.Player });

        _alignment.DidNotReceiveWithAnyArgs().ResolveSide(default!, default!);
    }

    // --- the new-game friendship flip -----------------------------------------------------------------------------------

    private sealed class FakeLords : IAllegianceRelationAdapter
    {
        private readonly List<SideIds> _sides;
        public readonly Dictionary<(int, int), int> Relations = new();
        public readonly List<(int, int, int)> Writes = new();
        public int Reads;
        public int Releases;
        public bool ThrowOnRead;

        public FakeLords(params SideIds[] sides) => _sides = sides.ToList();

        public IReadOnlyList<SideIds> ReadLords() => _sides;

        public int ReadRelation(int first, int second)
        {
            Reads++;
            if (ThrowOnRead)
                throw new System.InvalidOperationException("read fault");
            return Relations.TryGetValue((first, second), out var value) ? value : 0;
        }

        public void Release() => Releases++;

        public void WriteRelation(int first, int second, int value)
        {
            Writes.Add((first, second, value));
            Relations[(first, second)] = value;
        }
    }

    [TestMethod]
    public void Flip_AFreeAndEvilFriendship_BecomesEnmityOfTheSameSize()
    {
        var lords = new FakeLords(Free, Evil);
        lords.Relations[(0, 1)] = 42;

        Assert.AreEqual(1, _service.FlipOpposedFriendships(lords));
        CollectionAssert.AreEqual(new[] { (0, 1, -42) }, lords.Writes);
    }

    [DataTestMethod]
    [DataRow(-30)]
    [DataRow(0)]
    public void Flip_AnOpposedPairThatIsNotFriends_IsLeftAlone(int relation)
    {
        var lords = new FakeLords(Evil, Free);
        lords.Relations[(0, 1)] = relation;

        Assert.AreEqual(0, _service.FlipOpposedFriendships(lords));
        Assert.AreEqual(0, lords.Writes.Count);
    }

    [TestMethod]
    public void Flip_SameSideAndNeutralPairs_AreNotRead()
    {
        var lords = new FakeLords(Free, Free, Neutral, Evil, Evil);

        _service.FlipOpposedFriendships(lords);

        // Only the four Free-Evil pairs: (0,3) (0,4) (1,3) (1,4).
        Assert.AreEqual(4, lords.Reads);
    }

    [TestMethod]
    public void Flip_SameSideRivalries_Stay()
    {
        var lords = new FakeLords(Free, Free, Evil);
        lords.Relations[(0, 1)] = -50;
        lords.Relations[(0, 2)] = 10;
        lords.Relations[(1, 2)] = -10;

        _service.FlipOpposedFriendships(lords);

        Assert.AreEqual(-50, lords.Relations[(0, 1)]);
        Assert.AreEqual(-10, lords.Relations[(0, 2)]);
        Assert.AreEqual(-10, lords.Relations[(1, 2)]);
        Assert.AreEqual(1, lords.Writes.Count);
    }

    [TestMethod]
    public void Flip_ResolvesEachLordsSideOnce()
    {
        var lords = new FakeLords(Free, Evil, Free, Evil);

        _service.FlipOpposedFriendships(lords);

        _alignment.ReceivedWithAnyArgs(4).ResolveSide(default!, default!);
    }

    [TestMethod]
    public void Flip_IsIdempotent()
    {
        var lords = new FakeLords(Free, Evil);
        lords.Relations[(0, 1)] = 42;

        _service.FlipOpposedFriendships(lords);
        Assert.AreEqual(0, _service.FlipOpposedFriendships(lords));
        Assert.AreEqual(-42, lords.Relations[(0, 1)]);
    }

    [TestMethod]
    public void Flip_TheSwitchOff_ReadsNothing()
    {
        _settings.Enabled.Returns(false);
        var lords = new FakeLords(Free, Evil);
        lords.Relations[(0, 1)] = 42;

        Assert.AreEqual(0, _service.FlipOpposedFriendships(lords));
        Assert.AreEqual(0, lords.Reads);
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.StartsWith("[Check] id=" + KingdomAllegianceService.FlipCheckId + " result=skip") && s.Contains("reason=switch-off")));
    }

    [TestMethod]
    public void Flip_ACoopPeerDeferringToTheHost_ReadsNothing()
    {
        _coop.ShouldDeferToHost.Returns(true);
        var lords = new FakeLords(Free, Evil);
        lords.Relations[(0, 1)] = 42;

        Assert.AreEqual(0, _service.FlipOpposedFriendships(lords));
        Assert.AreEqual(0, lords.Reads);
    }

    [TestMethod]
    public void Flip_ReleasesTheLords_WhenTheRunEnds()
    {
        var lords = new FakeLords(Free, Evil);

        _service.FlipOpposedFriendships(lords);

        Assert.AreEqual(1, lords.Releases);
    }

    [TestMethod]
    public void Flip_AFaultMidRun_StillReleasesTheLords()
    {
        var lords = new FakeLords(Free, Evil) { ThrowOnRead = true };

        Assert.ThrowsException<System.InvalidOperationException>(() => _service.FlipOpposedFriendships(lords));
        Assert.AreEqual(1, lords.Releases);
    }

    [TestMethod]
    public void Flip_WritesOnePassLine_WithTheCounts()
    {
        var lords = new FakeLords(Free, Evil, Neutral);
        lords.Relations[(0, 1)] = 5;

        _service.FlipOpposedFriendships(lords);

        _logger.Received().LogInfo("[Check] id=" + KingdomAllegianceService.FlipCheckId + " result=pass lords=3 opposed=1 flipped=1");
    }
}
