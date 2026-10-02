using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.FactionUI.Presets;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. Kysaro's hero presets with Mike's Option A (2026-10-01): when the face generator opens
/// the pick's look and gear are copied onto the player; its name and skills only when character
/// creation finishes, so un-picking (going back and choosing Custom Character) leaves nothing hidden
/// behind, and the look the character had before the pick comes back. A pick never outlives the
/// character creation it was made in.
/// </summary>
[TestClass]
public class FactionPresetServiceTests
{
    private IPresetAppearanceAdapter _appearance = null!;
    private FactionPresetService _sut = null!;
    private readonly object _hero = new();
    private readonly object _template = new();
    private readonly object _resolvedHero = new();
    private readonly object _resolvedTemplate = new();
    private readonly object _lookBefore = new();

    [TestInitialize]
    public void Setup()
    {
        _appearance = Substitute.For<IPresetAppearanceAdapter>();
        _appearance.Resolve(_hero, true).Returns(_resolvedHero);
        _appearance.Resolve(_template, false).Returns(_resolvedTemplate);
        _appearance.CaptureLook().Returns(_lookBefore);
        _sut = new FactionPresetService(_appearance);
    }

    [TestMethod]
    public void SelectHero_ResolvesThePickOnce_SoThePreviewAndBothCopiesShareOneGearDraw()
    {
        _sut.SelectHero(_hero);
        _sut.OnFaceGeneratorOpening();
        _sut.OnFaceGeneratorOpening();
        _sut.OnCharacterCreationFinalize();

        _appearance.Received(1).Resolve(_hero, true);
    }

    [TestMethod]
    public void OnFaceGeneratorOpening_AfterAPick_CopiesTheLookButNeitherSkillsNorName()
    {
        _sut.SelectHero(_hero);

        _sut.OnFaceGeneratorOpening();

        _appearance.Received(1).ApplyLook(_resolvedHero);
        _appearance.DidNotReceiveWithAnyArgs().ApplyIdentity(default!);
    }

    [TestMethod]
    public void OnFaceGeneratorOpening_OpenedAgain_DoesNotCopyTheLookTwice()
    {
        _sut.SelectHero(_hero);

        _sut.OnFaceGeneratorOpening();
        _sut.OnFaceGeneratorOpening();

        _appearance.Received(1).ApplyLook(_resolvedHero);
    }

    [TestMethod]
    public void OnFaceGeneratorOpening_ReturnsThePicksGearEveryTimeTheGeneratorOpens()
    {
        var gear = new object();
        _appearance.DisplayEquipment(_resolvedHero).Returns(gear);
        _sut.SelectHero(_hero);

        Assert.AreSame(gear, _sut.OnFaceGeneratorOpening());
        Assert.AreSame(gear, _sut.OnFaceGeneratorOpening(), "going back to the face generator keeps the pick's gear");
    }

    [TestMethod]
    public void OnFaceGeneratorOpening_WithNoPick_KeepsTheGeneratorsOwnGearAndLook()
    {
        Assert.IsNull(_sut.OnFaceGeneratorOpening());
        _appearance.DidNotReceiveWithAnyArgs().DisplayEquipment(default!);
        _appearance.DidNotReceiveWithAnyArgs().ApplyLook(default!);
        _appearance.DidNotReceive().CaptureLook();
    }

    [TestMethod]
    public void OnFaceGeneratorOpening_CapturesTheLookBeforeTheFirstPickIsCopied()
    {
        _sut.SelectHero(_hero);

        _sut.OnFaceGeneratorOpening();

        Received.InOrder(() =>
        {
            _appearance.CaptureLook();
            _appearance.ApplyLook(_resolvedHero);
        });
    }

    [TestMethod]
    public void Clear_AfterThePicksLookWasCopied_RestoresTheLookFromBeforeThePick()
    {
        _sut.SelectHero(_hero);
        _sut.OnFaceGeneratorOpening();

        _sut.Clear();

        _appearance.Received(1).RestoreLook(_lookBefore);
        Assert.IsFalse(_sut.HasPick);
    }

    [TestMethod]
    public void Clear_ThenFinalize_CopiesNoSkillsOrName()
    {
        _sut.SelectHero(_hero);
        _sut.OnFaceGeneratorOpening();

        _sut.Clear();
        _sut.OnCharacterCreationFinalize();

        _appearance.DidNotReceiveWithAnyArgs().ApplyIdentity(default!);
    }

    [TestMethod]
    public void Clear_BeforeAnyLookWasCopied_RestoresNothing()
    {
        _sut.SelectHero(_hero);

        _sut.Clear();

        _appearance.DidNotReceiveWithAnyArgs().RestoreLook(default!);
    }

    [TestMethod]
    public void Clear_AfterTwoPicks_RestoresTheLookFromBeforeTheFirst()
    {
        _sut.SelectHero(_hero);
        _sut.OnFaceGeneratorOpening();
        _sut.SelectTemplate(_template);
        _sut.OnFaceGeneratorOpening();

        _sut.Clear();

        _appearance.Received(1).CaptureLook();
        _appearance.Received(1).RestoreLook(_lookBefore);
    }

    [TestMethod]
    public void OnCharacterCreationFinalize_CopiesTheLookThenTheNameAndSkills()
    {
        _sut.SelectHero(_hero);
        _sut.OnFaceGeneratorOpening();

        _sut.OnCharacterCreationFinalize();

        _appearance.Received(2).ApplyLook(_resolvedHero);
        _appearance.Received(1).ApplyIdentity(_resolvedHero);
    }

    [TestMethod]
    public void OnCharacterCreationFinalize_APickTheGeneratorNeverOpenedFor_IsCopiedOnce()
    {
        _sut.SelectTemplate(_template);

        _sut.OnCharacterCreationFinalize();

        _appearance.Received(1).ApplyLook(_resolvedTemplate);
        _appearance.Received(1).ApplyIdentity(_resolvedTemplate);
    }

    [TestMethod]
    public void OnCharacterCreationFinalize_ForgetsThePick_SoASecondCampaignStartsClean()
    {
        _sut.SelectHero(_hero);
        _sut.OnCharacterCreationFinalize();

        _sut.OnCharacterCreationFinalize();
        _sut.OnFaceGeneratorOpening();

        _appearance.Received(1).ApplyIdentity(_resolvedHero);
        Assert.IsFalse(_sut.HasPick);
    }

    [TestMethod]
    public void ResetForNewCharacterCreation_DropsAPickFromTheLastOneWithoutRestoringItsLook()
    {
        _sut.SelectHero(_hero);
        _sut.OnFaceGeneratorOpening();

        _sut.ResetForNewCharacterCreation();
        _sut.OnCharacterCreationFinalize();

        _appearance.DidNotReceiveWithAnyArgs().ApplyIdentity(default!);
        _appearance.DidNotReceiveWithAnyArgs().RestoreLook(default!);
        Assert.IsFalse(_sut.HasPick);
    }

    [TestMethod]
    public void SelectHero_AfterAnAppliedTemplate_ReplacesIt()
    {
        _sut.SelectTemplate(_template);
        _sut.OnFaceGeneratorOpening();

        _sut.SelectHero(_hero);
        _sut.OnFaceGeneratorOpening();
        _sut.OnCharacterCreationFinalize();

        _appearance.Received(1).ApplyLook(_resolvedTemplate);
        _appearance.DidNotReceive().ApplyIdentity(_resolvedTemplate);
        _appearance.Received(1).ApplyIdentity(_resolvedHero);
    }

    [TestMethod]
    public void HasPick_FollowsTheSelection()
    {
        Assert.IsFalse(_sut.HasPick);

        _sut.SelectHero(_hero);
        Assert.IsTrue(_sut.HasPick, "a pending pick");

        _sut.OnFaceGeneratorOpening();
        Assert.IsTrue(_sut.HasPick, "an applied pick: TAOM's race filter leaves its race alone");

        _sut.Clear();
        Assert.IsFalse(_sut.HasPick);
    }

    [TestMethod]
    public void SelectHero_APickTheAdapterCannotResolve_IsNoPick()
    {
        var unknown = new object();

        _sut.SelectHero(unknown);

        Assert.IsFalse(_sut.HasPick);
        Assert.IsNull(_sut.OnFaceGeneratorOpening());
    }

    // A hero taken over (#704) skips the face generator: FactionPickService shows his look when the
    // culture stage completes, so the career menu shows him.

    [TestMethod]
    public void ApplyPendingLook_ShowsThePicksLookWithoutTheGenerator()
    {
        _sut.SelectHero(_hero);

        _sut.ApplyPendingLook();

        Received.InOrder(() =>
        {
            _appearance.CaptureLook();
            _appearance.ApplyLook(_resolvedHero);
        });
        _appearance.DidNotReceiveWithAnyArgs().ApplyIdentity(default!);
    }

    [TestMethod]
    public void Clear_AfterALookShownWithoutTheGenerator_RestoresTheLookFromBefore()
    {
        _sut.SelectHero(_hero);
        _sut.ApplyPendingLook();

        _sut.Clear();

        _appearance.Received(1).RestoreLook(_lookBefore);
    }

    [TestMethod]
    public void ApplyPendingLook_WithNoPick_TouchesNothing()
    {
        _sut.ApplyPendingLook();

        _appearance.DidNotReceive().CaptureLook();
        _appearance.DidNotReceiveWithAnyArgs().ApplyLook(default!);
    }
}
