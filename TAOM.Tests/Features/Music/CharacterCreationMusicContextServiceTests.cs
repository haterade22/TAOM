using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Music;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class CharacterCreationMusicContextServiceTests
{
    [TestMethod]
    public void CaptureSnapshot_ReturnsInactiveBeforeCharacterCreationStarts()
    {
        var service = new CharacterCreationMusicContextService();

        var snapshot = service.CaptureSnapshot();

        Assert.IsFalse(snapshot.IsActive);
        Assert.IsFalse(snapshot.CharacterCreation);
        Assert.AreEqual("empty", snapshot.Reason);
    }

    [TestMethod]
    public void EnterCharacterCreation_RemainsInactiveUntilConfirmedCultureArrives()
    {
        var service = new CharacterCreationMusicContextService();

        service.EnterCharacterCreation();
        var snapshot = service.CaptureSnapshot();

        Assert.IsFalse(snapshot.IsActive);
        Assert.IsFalse(snapshot.CharacterCreation);
        Assert.AreEqual(MusicTrackIndex.NeutralCulture, snapshot.CultureId);
        Assert.AreEqual("character_creation_waiting_for_culture", snapshot.Reason);
    }

    [TestMethod]
    public void EnterCharacterCreation_PreservesConfirmedCultureAfterSelection()
    {
        var service = new CharacterCreationMusicContextService();

        service.ConfirmCulture("gondor");
        service.EnterCharacterCreation();
        var snapshot = service.CaptureSnapshot();

        Assert.IsTrue(snapshot.IsActive);
        Assert.IsTrue(snapshot.CharacterCreation);
        Assert.AreEqual("gondor", snapshot.CultureId);
        Assert.AreEqual("character_creation_culture_confirmed", snapshot.Reason);
    }

    [TestMethod]
    public void ConfirmCulture_StoresTrimmedCultureAndActivatesCharacterCreationSnapshot()
    {
        var service = new CharacterCreationMusicContextService();

        service.ConfirmCulture("  gondor  ");
        var snapshot = service.CaptureSnapshot();

        Assert.IsTrue(snapshot.IsActive);
        Assert.IsTrue(snapshot.CharacterCreation);
        Assert.AreEqual("gondor", snapshot.CultureId);
        Assert.AreEqual("character_creation_culture_confirmed", snapshot.Reason);
    }

    [TestMethod]
    public void SelectCulture_StoresTrimmedCultureAndActivatesCharacterCreationSnapshot()
    {
        var service = new CharacterCreationMusicContextService();

        service.SelectCulture("  harad  ");
        var snapshot = service.CaptureSnapshot();

        Assert.IsTrue(snapshot.IsActive);
        Assert.IsTrue(snapshot.CharacterCreation);
        Assert.AreEqual("harad", snapshot.CultureId);
        Assert.AreEqual("character_creation_culture_selected", snapshot.Reason);
    }

    [TestMethod]
    public void ConfirmCulture_OverridesSelectedReasonAfterRegionPreview()
    {
        var service = new CharacterCreationMusicContextService();

        service.SelectCulture("harad");
        service.ConfirmCulture("gondor");
        var snapshot = service.CaptureSnapshot();

        Assert.IsTrue(snapshot.IsActive);
        Assert.IsTrue(snapshot.CharacterCreation);
        Assert.AreEqual("gondor", snapshot.CultureId);
        Assert.AreEqual("character_creation_culture_confirmed", snapshot.Reason);
    }

    [TestMethod]
    public void ConfirmCulture_IgnoresBlankCultureWithoutClearingLastConfirmedCulture()
    {
        var service = new CharacterCreationMusicContextService();

        service.ConfirmCulture("gondor");
        service.ConfirmCulture(" ");
        var snapshot = service.CaptureSnapshot();

        Assert.IsTrue(snapshot.IsActive);
        Assert.IsTrue(snapshot.CharacterCreation);
        Assert.AreEqual("gondor", snapshot.CultureId);
    }

    [TestMethod]
    public void SelectCulture_IgnoresBlankCultureWithoutClearingLastSelectedCulture()
    {
        var service = new CharacterCreationMusicContextService();

        service.SelectCulture("harad");
        service.SelectCulture(" ");
        var snapshot = service.CaptureSnapshot();

        Assert.IsTrue(snapshot.IsActive);
        Assert.IsTrue(snapshot.CharacterCreation);
        Assert.AreEqual("harad", snapshot.CultureId);
        Assert.AreEqual("character_creation_culture_selected", snapshot.Reason);
    }

    [TestMethod]
    public void ExitCharacterCreation_ClearsSnapshot()
    {
        var service = new CharacterCreationMusicContextService();

        service.ConfirmCulture("gondor");
        service.ExitCharacterCreation("screen_finalized");
        var snapshot = service.CaptureSnapshot();

        Assert.IsFalse(snapshot.IsActive);
        Assert.IsFalse(snapshot.CharacterCreation);
        Assert.AreEqual("screen_finalized", snapshot.Reason);
    }
}
