using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.TournamentRewards;
using TAOM.Features.TrainingGrounds.Waves;
using TAOM.Features.TrainingGrounds.Waves.Domain;

namespace TAOM.Tests.Features.TrainingGrounds.Waves;

/// <summary>
/// A purse that moves gold the way the engine's does, with each seam settable: a charge that takes a different amount, a charge that is
/// refused, a grant that adds a different amount (testing-qa.md: a fake whose field the code reads must be settable, and one test must move it).
/// </summary>
internal sealed class FakePurse : ICareerPurseAdapter
{
    public int Gold { get; set; } = 100_000;

    /// <summary>When set, a charge takes this much whatever the amount (a purse that lies).</summary>
    public int? ChargeTakes { get; set; }

    public bool ChargeRefused { get; set; }

    /// <summary>When set, a grant adds this much whatever the amount.</summary>
    public int? GrantAdds { get; set; }

    public bool ChargeGold(int amount)
    {
        if (ChargeRefused || amount < 0 || Gold < amount)
            return false;
        Gold -= ChargeTakes ?? amount;
        return true;
    }

    public bool GrantGold(int amount)
    {
        if (amount <= 0)
            return false;
        Gold += GrantAdds ?? amount;
        return true;
    }
}

/// <summary>The service with every seam it reads set to a state in which the offer shows, in a town, on day 100, with 100,000 gold.</summary>
internal sealed class WaveBetFixture
{
    public const string TownId = "town_a";

    public WaveBetFixture()
    {
        Config.GetTable().Returns(WaveTable.Default);
        Settings.Enabled.Returns(true);
        Tournament.MaxBetPerRound.Returns(15_000);
        Adapter.Today.Returns(100);
        Adapter.TownId.Returns(TownId);
        Adapter.TalkingToPracticeMaster.Returns(true);
        Adapter.DeepPocketsFactor.Returns(1);
        Adapter.GiveToTown(Arg.Any<int>()).Returns(true);
        Coop.IsAuthority.Returns(true);
        Service = new WaveBetService(Config, Settings, Tournament, Purse, Adapter, Coop, Dedicated, Outcomes, Logger);
        Service.AttachArena();
    }

    public ITrainingWavesConfigProvider Config { get; } = Substitute.For<ITrainingWavesConfigProvider>();

    public ITrainingWavesSettingsProvider Settings { get; } = Substitute.For<ITrainingWavesSettingsProvider>();

    public ITournamentRewardsSettingsProvider Tournament { get; } = Substitute.For<ITournamentRewardsSettingsProvider>();

    public FakePurse Purse { get; } = new();

    public ITrainingWavesAdapter Adapter { get; } = Substitute.For<ITrainingWavesAdapter>();

    public ICoopSessionProvider Coop { get; } = Substitute.For<ICoopSessionProvider>();

    public IDedicatedServerProvider Dedicated { get; } = Substitute.For<IDedicatedServerProvider>();

    public IOutcomeLog Outcomes { get; } = Substitute.For<IOutcomeLog>();

    public IModLogger Logger { get; } = Substitute.For<IModLogger>();

    public WaveBetService Service { get; }

    /// <summary>Places a stake on the first wave and starts the fight, so the run is Fighting.</summary>
    public WaveRun Fighting(int stake = 200)
    {
        Service.PrepareRun();
        Assert.IsTrue(Service.PlaceStake(stake), "the fixture's stake was refused");
        Service.Run!.BeginFight();
        return Service.Run;
    }
}
