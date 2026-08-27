using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.PlayerSwitcher;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// Issue #514. The selection is character-creation-scoped state. The old LOTRAOM feature held it
/// in five static mutable fields spread across its patches; this store replaces all of them and
/// splits the read side from the write side so only the picker can change the selection.
/// </summary>
[TestClass]
public class PlayerSwitchSessionStoreTests
{
    private PlayerSwitchSessionStore _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new PlayerSwitchSessionStore();

    private static HeroPickRow Row(string id, int race = 3, bool hasClan = true)
        => new HeroPickRow(id, "Dain", HeroPickerGroup.ClanLeaders, race, false, true, hasClan);

    [TestMethod]
    public void AFreshStore_HasNoSelection()
    {
        Assert.IsFalse(_sut.HasSelection);
        Assert.AreEqual(string.Empty, _sut.SelectedHeroId);
        Assert.IsFalse(_sut.IsPreviewActive);
    }

    [TestMethod]
    public void SelectingAHero_RecordsTheWholeRow()
    {
        _sut.Select(Row("dain", race: 3));

        Assert.IsTrue(_sut.HasSelection);
        Assert.AreEqual("dain", _sut.SelectedHeroId);
        Assert.AreEqual(3, _sut.SelectedRace);
        Assert.IsTrue(_sut.SelectedRow.HasClan, "the planner reads HasClan off the stored row");
    }

    [TestMethod]
    public void SelectingAgain_ReplacesThePreviousSelection()
    {
        _sut.Select(Row("dain"));
        _sut.Select(Row("thorin"));

        Assert.AreEqual("thorin", _sut.SelectedHeroId);
    }

    [TestMethod]
    public void Clearing_RemovesTheSelection()
    {
        _sut.Select(Row("dain"));

        _sut.Clear();

        Assert.IsFalse(_sut.HasSelection);
        Assert.AreEqual(string.Empty, _sut.SelectedHeroId);
    }

    [TestMethod]
    public void Clearing_AlsoEndsThePreview()
    {
        _sut.Select(Row("dain"));
        _sut.SetPreviewActive(true);

        _sut.Clear();

        Assert.IsFalse(_sut.IsPreviewActive,
            "a stale preview flag would keep Patch9_RaceFilter suppressed for the rest of creation");
    }

    [TestMethod]
    public void ThePreviewFlagTogglesIndependentlyOfTheSelection()
    {
        _sut.Select(Row("dain"));

        _sut.SetPreviewActive(true);
        Assert.IsTrue(_sut.IsPreviewActive);

        _sut.SetPreviewActive(false);
        Assert.IsFalse(_sut.IsPreviewActive);
        Assert.IsTrue(_sut.HasSelection, "ending the preview must not clear the choice");
    }

    [TestMethod]
    public void SelectingAnEmptyRow_CountsAsNoSelection()
    {
        _sut.Select(default);

        Assert.IsFalse(_sut.HasSelection);
        Assert.AreEqual(string.Empty, _sut.SelectedHeroId);
    }

    [TestMethod]
    public void TheReadAndWriteFacesObserveTheSameState()
    {
        IPlayerSwitchSessionWriter writer = _sut;
        IPlayerSwitchSession reader = _sut;

        writer.Select(Row("dain"));

        Assert.AreEqual("dain", reader.SelectedHeroId);
    }
}
