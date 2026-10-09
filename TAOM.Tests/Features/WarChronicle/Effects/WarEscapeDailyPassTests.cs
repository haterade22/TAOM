using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.WarChronicle.Effects;

namespace TAOM.Tests.Features.WarChronicle.Effects;

[TestClass]
public class WarEscapeDailyPassTests
{
    private IPrisonerEscapeAdapter _adapter = null!;
    private IWarEffectService _effects = null!;
    private IModLogger _logger = null!;
    private WarEscapeDailyPass _sut = null!;
    private IReadOnlyCollection<string>? _askedKingdoms;

    [TestInitialize]
    public void Setup()
    {
        _askedKingdoms = null;
        _rows.Clear();
        _adapter = Substitute.For<IPrisonerEscapeAdapter>();
        _adapter.GetCapturedLords(Arg.Do<IReadOnlyCollection<string>>(ids => _askedKingdoms = ids))
            .Returns(new List<PrisonerEscapeSnapshot>());
        _adapter.NextRoll().Returns(0f);
        _adapter.Escape(Arg.Any<string>()).Returns(true);
        _effects = Substitute.For<IWarEffectService>();
        _effects.Snapshot().Returns(new List<WarEffect>());
        _effects.GetMultiplier(Arg.Any<string>(), Arg.Any<WarEffectKind>()).Returns(1f);
        _logger = Substitute.For<IModLogger>();
        _sut = new WarEscapeDailyPass(_adapter, _effects, new WarEscapeService(), _logger);
    }

    private readonly List<WarEffect> _rows = new List<WarEffect>();

    private void Boost(string kingdom, float multiplier, WarEffectKind kind = WarEffectKind.PrisonerEscape, string source = "rally")
    {
        _rows.Add(new WarEffect(source, kingdom, kind, 0.5f, 1000d));
        _effects.Snapshot().Returns(_rows.ToList());
        _effects.GetMultiplier(kingdom, kind).Returns(multiplier);
    }

    private void Lords(params PrisonerEscapeSnapshot[] lords) =>
        _adapter.GetCapturedLords(Arg.Any<IReadOnlyCollection<string>>()).Returns(lords.ToList());

    private static PrisonerEscapeSnapshot Lord(string id, string kingdom = "empire_w") => new PrisonerEscapeSnapshot
    {
        HeroId = id,
        KingdomId = kingdom,
        IsAlive = true,
        IsPrisoner = true,
        CanBeReleased = true,
        EscapeFactor = 1f,
    };

    [TestMethod]
    public void Run_NoEscapeEffects_ReadsNoLords()
    {
        var escaped = _sut.Run();

        Assert.AreEqual(0, escaped);
        _adapter.DidNotReceive().GetCapturedLords(Arg.Any<IReadOnlyCollection<string>>());
    }

    [TestMethod]
    public void Run_OnlyAVolunteerEffect_ReadsNoLords()
    {
        Boost("empire_w", 1.3f, WarEffectKind.VolunteerRate);

        _sut.Run();

        _adapter.DidNotReceive().GetCapturedLords(Arg.Any<IReadOnlyCollection<string>>());
    }

    [TestMethod]
    public void Run_EscapeEffectBakedToOne_ReadsNoLords()
    {
        Boost("empire_w", 1f);

        _sut.Run();

        _adapter.DidNotReceive().GetCapturedLords(Arg.Any<IReadOnlyCollection<string>>());
    }

    [TestMethod]
    public void Run_AsksOnlyForKingdomsWhoseEscapeMultiplierIsAboveOne_Once()
    {
        Boost("empire_w", 1.5f);
        Boost("empire_s", 1f);
        Boost("vlandia", 2f);
        Boost("vlandia", 2f, WarEffectKind.VolunteerRate);
        Boost("empire_w", 1.5f, WarEffectKind.PrisonerEscape, "event");

        _sut.Run();

        CollectionAssert.AreEquivalent(new[] { "empire_w", "vlandia" }, _askedKingdoms!.ToList());
    }

    [TestMethod]
    public void Run_EligibleLordAndLowRoll_EscapesAndCountsIt()
    {
        Boost("empire_w", 2f);
        Lords(Lord("lord_1"));

        var escaped = _sut.Run();

        Assert.AreEqual(1, escaped);
        _adapter.Received(1).Escape("lord_1");
    }

    [TestMethod]
    public void Run_RollAboveTheChance_DoesNotEscape()
    {
        Boost("empire_w", 2f);
        Lords(Lord("lord_1"));
        _adapter.NextRoll().Returns(0.9f);

        var escaped = _sut.Run();

        Assert.AreEqual(0, escaped);
        _adapter.DidNotReceive().Escape(Arg.Any<string>());
    }

    [TestMethod]
    public void Run_IneligibleLord_DrawsNoRollAndDoesNotEscape()
    {
        Boost("empire_w", 2f);
        var held = Lord("lord_1");
        held.CaptorInMapEventOrSiege = true;
        Lords(held);

        _sut.Run();

        _adapter.DidNotReceive().NextRoll();
        _adapter.DidNotReceive().Escape(Arg.Any<string>());
    }

    [TestMethod]
    public void Run_UsesEachLordsOwnKingdomMultiplier()
    {
        Boost("empire_w", 2f);
        Boost("vlandia", 1f);
        Lords(Lord("lord_w", "empire_w"), Lord("lord_v", "vlandia"));

        _sut.Run();

        _adapter.Received(1).Escape("lord_w");
        _adapter.DidNotReceive().Escape("lord_v");
    }

    [TestMethod]
    public void Run_TheAdapterDeclinesTheEscape_IsNotCounted()
    {
        Boost("empire_w", 2f);
        Lords(Lord("lord_1"));
        _adapter.Escape("lord_1").Returns(false);

        Assert.AreEqual(0, _sut.Run());
    }

    [TestMethod]
    public void Run_AnEscape_IsLoggedWithTheHeroAndKingdom()
    {
        Boost("empire_w", 2f);
        Lords(Lord("lord_1"));

        _sut.Run();

        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("lord_1") && m.Contains("empire_w")));
    }

    [TestMethod]
    public void Run_OneEscapeThrows_TheNextLordIsStillProcessed()
    {
        Boost("empire_w", 2f);
        Lords(Lord("lord_1"), Lord("lord_2"));
        _adapter.Escape("lord_1").Returns(_ => throw new InvalidOperationException("engine"));

        var escaped = _sut.Run();

        Assert.AreEqual(1, escaped);
        _adapter.Received(1).Escape("lord_2");
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("lord_1") && m.Contains("engine")));
    }

    [TestMethod]
    public void Run_TheLordReadThrows_LogsAndReturnsZero()
    {
        Boost("empire_w", 2f);
        _adapter.GetCapturedLords(Arg.Any<IReadOnlyCollection<string>>())
            .Returns(_ => throw new InvalidOperationException("engine getter"));

        var escaped = _sut.Run();

        Assert.AreEqual(0, escaped);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("engine getter")));
    }

    [TestMethod]
    public void Run_NaNRoll_DoesNotEscape()
    {
        Boost("empire_w", 2f);
        Lords(Lord("lord_1"));
        _adapter.NextRoll().Returns(float.NaN);

        Assert.AreEqual(0, _sut.Run());
    }
}
