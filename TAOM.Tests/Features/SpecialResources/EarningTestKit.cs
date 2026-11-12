using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>Shared fakes for the earning service tests: a fixed resource, switchable session flags.</summary>
internal sealed class EarningTestKit
{
    public static readonly SpecialResource Resource = new(
        "war_spoils", new[] { "k" }, new[] { "c" }, "War Spoils", "icon",
        cap: 500f, startingAmount: 10f, dailyPerTown: 1f, perBattleVictoryBase: 1f, perRaid: 1f,
        perSiegeVictory: 1f, perPrisoner: 1f);

    public ISpecialResourceService Service { get; } = Substitute.For<ISpecialResourceService>();
    public ISpecialResourceStorageService Storage { get; } = Substitute.For<ISpecialResourceStorageService>();
    public ICoopSessionProvider Session { get; } = Substitute.For<ICoopSessionProvider>();
    public IDedicatedServerProvider Dedicated { get; } = Substitute.For<IDedicatedServerProvider>();
    public IModLogger Logger { get; } = Substitute.For<IModLogger>();
    public SpecialResourceEarningService Sut { get; }

    public EarningTestKit(bool session = false, bool authority = true, bool dedicated = false)
    {
        Session.IsSessionActive.Returns(session);
        Session.IsAuthority.Returns(authority);
        Dedicated.IsDedicatedServer.Returns(dedicated);
        Service.ResolveResource(Arg.Any<string>(), Arg.Any<string>()).Returns(Resource);
        Sut = new SpecialResourceEarningService(Service, Storage, Session, Dedicated, Logger);
    }

    // Local() is the single-player MainHero. A co-op server's own hero is Local(control: NotPlayer), and a
    // guest swapped in as MainHero (a barter or hideout finalize scope) is Local() with a Player control.
    public static EarnerRef Local(PlayerControl control = PlayerControl.Player)
        => new("main", "k", "c", isLocalMainHero: true, control);

    public static EarnerRef Remote(string id, PlayerControl control = PlayerControl.Player)
        => new(id, "k", "c", isLocalMainHero: false, control);
}
