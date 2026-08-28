using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.PlayerSwitcher;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// Issue #514. Resolves the feature through a REAL DryIoc container, following the precedent set by
/// `EconomyDiagnosticsWiringTests`.
///
/// Why this exists: the session store is registered once and exposed under two interfaces, a reader
/// (`IPlayerSwitchSession`) and a writer (`IPlayerSwitchSessionWriter`), via
/// `container.RegisterMapping`. The picker writes the selection through the writer; the handover
/// reads it back through the reader at finalize. If those two ever resolved to DIFFERENT instances,
/// the handover would read an empty selection, the feature would be a silent no-op in game, and
/// every other test in this suite would still pass, because they all construct
/// `PlayerSwitchSessionStore` directly and never touch the container.
///
/// A deep review confirmed by reading DryIoc's own source that `RegisterMapping` re-registers the
/// same `Factory` object rather than forwarding a resolve, so the singleton genuinely is shared.
/// This test is what keeps that true after a refactor, since nothing else in the suite would notice.
/// </summary>
[TestClass]
public class PlayerSwitcherWiringTests
{
    private static IContainer NewContainer()
    {
        var container = new Container();
        PlayerSwitcherIoC.RegisterPlayerSwitcherFeature(container);
        return container;
    }

    [TestMethod]
    public void TheSessionReaderAndWriterResolveToTheSameInstance()
    {
        var container = NewContainer();

        var reader = container.Resolve<IPlayerSwitchSession>();
        var writer = container.Resolve<IPlayerSwitchSessionWriter>();

        Assert.AreSame<object>(reader, writer,
            "the picker writes the selection through the writer and the handover reads it through " +
            "the reader; two instances means the handover silently never fires");
    }

    [TestMethod]
    public void AWriteThroughTheWriterIsVisibleThroughTheReader()
    {
        var container = NewContainer();

        var reader = container.Resolve<IPlayerSwitchSession>();
        var writer = container.Resolve<IPlayerSwitchSessionWriter>();

        writer.Select(new TAOM.Features.PlayerSwitcher.Domain.HeroPickRow(
            "dain", "Dain", TAOM.Features.PlayerSwitcher.Domain.HeroPickerGroup.ClanLeaders,
            race: 3, isFemale: false, isLeader: true, hasClan: true));

        Assert.IsTrue(reader.HasSelection);
        Assert.AreEqual("dain", reader.SelectedHeroId);
    }

    [TestMethod]
    public void TheSessionIsASingletonAcrossResolves()
    {
        var container = NewContainer();

        Assert.AreSame(
            container.Resolve<IPlayerSwitchSession>(),
            container.Resolve<IPlayerSwitchSession>(),
            "a per-resolve session would lose the selection between the picker and the handover");
    }
}
