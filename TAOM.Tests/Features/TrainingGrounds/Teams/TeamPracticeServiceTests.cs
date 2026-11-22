using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.TrainingGrounds.Teams;

namespace TAOM.Tests.Features.TrainingGrounds.Teams;

/// <summary>
/// #1528: a town is a team town when its owner is of the Free Peoples, the switch is on and this peer runs the
/// single-player campaign. Runs over the real <see cref="AlignmentService"/> on a stub of
/// <c>execution/alignment.json</c>, so the Free/Evil/Neutral reading is the production one. The ids are the ones the
/// data carries: <c>empire_w</c> is Gondor's kingdom, <c>battania</c> is Khand (Neutral), <c>khuzait</c> is Rhun (Evil).
/// </summary>
[TestClass]
public class TeamPracticeServiceTests
{
    private const string GondorKingdom = "empire_w";
    private const string MordorKingdom = "empire_s";
    private const string Gondor = "gondor";
    private const string Mordor = "mordor";
    private const string Khand = "battania";
    private const string Rhun = "khuzait";
    private const string Umbar = "umbar";

    private ITeamPracticeSettingsProvider _settings = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private TeamPracticeService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var config = Substitute.For<IAlignmentConfigProvider>();
        config.LoadAlignments().Returns(new Dictionary<string, string>
        {
            [GondorKingdom] = "free",
            [Gondor] = "free",
            [MordorKingdom] = "evil",
            [Mordor] = "evil",
            [Khand] = "neutral",
            [Rhun] = "evil",
            [Umbar] = "neutral",
        });
        _settings = Substitute.For<ITeamPracticeSettingsProvider>();
        _settings.IsEnabled.Returns(true);
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _sut = new TeamPracticeService(new AlignmentService(config, Substitute.For<IModLogger>()), _settings, _coop, _dedicated);
    }

    [TestMethod]
    public void IsTeamTown_FreeOwner_IsTrue()
    {
        Assert.IsTrue(_sut.IsTeamTown(new SideIds(GondorKingdom, Gondor)));
    }

    [TestMethod]
    public void IsTeamTown_EvilOwner_IsFalse()
    {
        Assert.IsFalse(_sut.IsTeamTown(new SideIds(MordorKingdom, Mordor)));
    }

    [TestMethod]
    public void IsTeamTown_NeutralOwner_IsFalse()
    {
        Assert.IsFalse(_sut.IsTeamTown(new SideIds(Umbar, Umbar)));
    }

    [TestMethod]
    public void IsTeamTown_NoOwner_IsFalse()
    {
        Assert.IsFalse(_sut.IsTeamTown(SideIds.None));
    }

    [TestMethod]
    public void IsTeamTown_UnlistedFactionWithFreeCulture_FollowsTheCulture()
    {
        // A player-founded kingdom (new_kingdom) is not in the table; its own culture decides.
        Assert.IsTrue(_sut.IsTeamTown(new SideIds("new_kingdom", Gondor)));
    }

    [TestMethod]
    public void IsTeamTown_UnlistedFactionWithEvilCulture_IsFalse()
    {
        Assert.IsFalse(_sut.IsTeamTown(new SideIds("new_kingdom", Mordor)));
    }

    [TestMethod]
    public void IsTeamTown_NeutralListedFactionWithFreeCulture_FollowsTheCulture()
    {
        // ResolveSide falls back to the culture when the id is listed Neutral: the pair's culture is the owner's own.
        Assert.IsTrue(_sut.IsTeamTown(new SideIds(Khand, Gondor)));
    }

    [TestMethod]
    public void IsTeamTown_KingdomlessClanOwner_UsesTheClansPair()
    {
        Assert.IsTrue(_sut.IsTeamTown(new SideIds("clan_free_1", Gondor)));
        Assert.IsFalse(_sut.IsTeamTown(new SideIds("clan_dark_1", Mordor)));
    }

    [TestMethod]
    public void IsTeamTown_SwitchOff_IsFalse()
    {
        _settings.IsEnabled.Returns(false);

        Assert.IsFalse(_sut.IsTeamTown(new SideIds(GondorKingdom, Gondor)));
    }

    [TestMethod]
    public void IsTeamTown_CoopSessionActive_IsFalse()
    {
        _coop.IsSessionActive.Returns(true);

        Assert.IsFalse(_sut.IsTeamTown(new SideIds(GondorKingdom, Gondor)));
    }

    [TestMethod]
    public void IsTeamTown_DefersToHost_IsFalse()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.IsFalse(_sut.IsTeamTown(new SideIds(GondorKingdom, Gondor)));
    }

    [TestMethod]
    public void IsTeamTown_NotTheAuthority_IsFalse()
    {
        _coop.IsAuthority.Returns(false);

        Assert.IsFalse(_sut.IsTeamTown(new SideIds(GondorKingdom, Gondor)));
    }

    [TestMethod]
    public void IsTeamTown_DedicatedServer_IsFalse()
    {
        _dedicated.IsDedicatedServer.Returns(true);

        Assert.IsFalse(_sut.IsTeamTown(new SideIds(GondorKingdom, Gondor)));
    }
}
