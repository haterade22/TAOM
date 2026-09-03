using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TAOM.Features.PlayerSwitcher;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// Issue #514 fast path. Picking a canon lord makes the six backstory menus pointless, because the
/// hero they are answered on is deleted at finalize and only the career choice is carried over.
///
/// The walk is unit-testable only because it runs against INarrativeStageAdapter: the engine's
/// CharacterCreationManager is sealed and cannot be substituted, which is the same limit
/// CareerMenuServiceTests documents for its own happy path.
/// </summary>
[TestClass]
public class NarrativeCareerFastPathServiceTests
{
    private IPlayerSwitchSession _session = null!;
    private IModLogger _logger = null!;
    private NarrativeCareerFastPathService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _session = Substitute.For<IPlayerSwitchSession>();
        _logger = Substitute.For<IModLogger>();
        _sut = new NarrativeCareerFastPathService(_session, _logger);
    }

    /// <summary>
    /// A chain of menus that behaves like the engine's: selecting is required before advancing,
    /// and advancing moves to the next id in the list.
    /// </summary>
    private sealed class FakeStage : INarrativeStageAdapter
    {
        private readonly List<string> _chain;
        private readonly bool _endless;
        private int _index;

        public int SelectCount { get; private set; }
        public int AdvanceCount { get; private set; }
        public bool OptionsAvailable { get; set; } = true;
        public bool AdvanceSucceeds { get; set; } = true;

        public FakeStage(List<string> chain, bool endless = false)
        {
            _chain = chain;
            _endless = endless;
        }

        public string CurrentMenuId => _endless ? "endless_menu" : _chain[_index];

        public bool SelectFirstSuitableOption()
        {
            if (!OptionsAvailable)
                return false;
            SelectCount++;
            return true;
        }

        public bool TryAdvance()
        {
            if (!AdvanceSucceeds)
                return false;
            AdvanceCount++;
            if (!_endless && _index < _chain.Count - 1)
                _index++;
            return true;
        }
    }

    private static List<string> VanillaChain() => new List<string>
    {
        "narrative_parent_menu",
        "narrative_childhood_menu",
        "narrative_education_menu",
        "narrative_youth_menu",
        "narrative_adulthood_menu",
        "narrative_age_selection_menu",
        CareerMenuService.CareerMenuId,
    };

    [TestMethod]
    public void SkipToCareerMenu_NoSelection_DoesNotTouchStage()
    {
        _session.HasSelection.Returns(false);
        var stage = Substitute.For<INarrativeStageAdapter>();

        _sut.SkipToCareerMenu(stage);

        stage.DidNotReceive().SelectFirstSuitableOption();
        stage.DidNotReceive().TryAdvance();
    }

    [TestMethod]
    public void SkipToCareerMenu_AlreadyOnCareerMenu_DoesNotAdvance()
    {
        _session.HasSelection.Returns(true);
        var stage = new FakeStage(new List<string> { CareerMenuService.CareerMenuId });

        _sut.SkipToCareerMenu(stage);

        Assert.AreEqual(0, stage.AdvanceCount, "Re-entering the stage while already on career must not walk further.");
        Assert.AreEqual(0, stage.SelectCount);
    }

    [TestMethod]
    public void SkipToCareerMenu_SixHopChain_AdvancesToCareerMenu()
    {
        _session.HasSelection.Returns(true);
        var stage = new FakeStage(VanillaChain());

        _sut.SkipToCareerMenu(stage);

        Assert.AreEqual(CareerMenuService.CareerMenuId, stage.CurrentMenuId);
        Assert.AreEqual(6, stage.AdvanceCount, "parent to career is six hops.");
        Assert.AreEqual(6, stage.SelectCount, "Every hop must select before advancing, or TrySwitchToNextMenu throws.");
    }

    [TestMethod]
    public void SkipToCareerMenu_NoSuitableOptionMidChain_AbortsAndLogs()
    {
        _session.HasSelection.Returns(true);
        var stage = new FakeStage(VanillaChain()) { OptionsAvailable = false };

        _sut.SkipToCareerMenu(stage);

        Assert.AreEqual(0, stage.AdvanceCount, "Advancing an unselected menu throws KeyNotFoundException in the engine.");
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void SkipToCareerMenu_AdvanceFails_AbortsAndLogs()
    {
        _session.HasSelection.Returns(true);
        var stage = new FakeStage(VanillaChain()) { AdvanceSucceeds = false };

        _sut.SkipToCareerMenu(stage);

        Assert.AreNotEqual(CareerMenuService.CareerMenuId, stage.CurrentMenuId);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void SkipToCareerMenu_HopCountExceedsSafetyCap_AbortsAndLogs()
    {
        _session.HasSelection.Returns(true);
        var stage = new FakeStage(VanillaChain(), endless: true);

        _sut.SkipToCareerMenu(stage);

        Assert.AreEqual(NarrativeCareerFastPathService.MaxHops, stage.AdvanceCount,
            "A chain that never reaches career must stop at the cap rather than spin.");
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void SkipToCareerMenu_StageThrows_DoesNotPropagate()
    {
        _session.HasSelection.Returns(true);
        var stage = Substitute.For<INarrativeStageAdapter>();
        stage.CurrentMenuId.Returns(_ => throw new InvalidOperationException("engine drift"));

        _sut.SkipToCareerMenu(stage);

        _logger.Received().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void SkipToCareerMenu_NullStage_DoesNothing()
    {
        _session.HasSelection.Returns(true);

        _sut.SkipToCareerMenu(null!);

        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }
}
