using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.HeroRace;
using TAOM.Features.HeroRace.Hooks;

namespace TAOM.Tests.Features.HeroRace;

[TestClass]
public class FaceGenIconReplacementHookTests
{
    private FaceGenIconReplacementHook _sut;
    private IFaceGenIconService _faceGenIconService;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _faceGenIconService = Substitute.For<IFaceGenIconService>();
        _logger = Substitute.For<IModLogger>();
        _sut = new FaceGenIconReplacementHook(_faceGenIconService, _logger);
    }

    [TestMethod]
    public void OnUpdateRaceAndGenderBasedResources_HumanRace_DoesNotCallService()
    {
        var beardItems = new List<IFaceGenItem>();
        var hairItems = new List<IFaceGenItem>();

        _sut.OnUpdateRaceAndGenderBasedResources(0, 0, beardItems, hairItems);

        _faceGenIconService.DidNotReceive().GetBeardName(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
        _faceGenIconService.DidNotReceive().GetHairName(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [TestMethod]
    public void OnUpdateRaceAndGenderBasedResources_NonHumanRace_ReplacesBeardImagePath()
    {
        var beardItem = Substitute.For<IFaceGenItem>();
        beardItem.Index.Returns(1);
        _faceGenIconService.GetBeardName(1, 1, 0).Returns("sk_dwarf_beard_a_01");

        _sut.OnUpdateRaceAndGenderBasedResources(1, 0,
            new List<IFaceGenItem> { beardItem },
            new List<IFaceGenItem>());

        beardItem.Received(1).ImagePath = @"FaceGen\Beard\sk_dwarf_beard_a_01";
    }

    [TestMethod]
    public void OnUpdateRaceAndGenderBasedResources_NonHumanMale_ReplacesHairWithMaleFolder()
    {
        var hairItem = Substitute.For<IFaceGenItem>();
        hairItem.Index.Returns(1);
        _faceGenIconService.GetHairName(1, 1, 0).Returns("dwarf_hair_a");

        _sut.OnUpdateRaceAndGenderBasedResources(1, 0,
            new List<IFaceGenItem>(),
            new List<IFaceGenItem> { hairItem });

        hairItem.Received(1).ImagePath = @"FaceGen\Hair\Male\dwarf_hair_a";
    }

    [TestMethod]
    public void OnUpdateRaceAndGenderBasedResources_NonHumanFemale_ReplacesHairWithFemaleFolder()
    {
        var hairItem = Substitute.For<IFaceGenItem>();
        hairItem.Index.Returns(1);
        _faceGenIconService.GetHairName(1, 1, 1).Returns("dwarf_female_hair_a");

        _sut.OnUpdateRaceAndGenderBasedResources(1, 1,
            new List<IFaceGenItem>(),
            new List<IFaceGenItem> { hairItem });

        hairItem.Received(1).ImagePath = @"FaceGen\Hair\Female\dwarf_female_hair_a";
    }

    [TestMethod]
    public void OnUpdateRaceAndGenderBasedResources_ServiceReturnsNull_DoesNotSetImagePath()
    {
        var beardItem = Substitute.For<IFaceGenItem>();
        beardItem.Index.Returns(0);
        _faceGenIconService.GetBeardName(0, 1, 0).Returns((string)null);

        _sut.OnUpdateRaceAndGenderBasedResources(1, 0,
            new List<IFaceGenItem> { beardItem },
            new List<IFaceGenItem>());

        beardItem.DidNotReceive().ImagePath = Arg.Any<string>();
    }

    [TestMethod]
    public void OnUpdateRaceAndGenderBasedResources_ServiceReturnsEmpty_DoesNotSetImagePath()
    {
        var beardItem = Substitute.For<IFaceGenItem>();
        beardItem.Index.Returns(0);
        _faceGenIconService.GetBeardName(0, 1, 0).Returns(string.Empty);

        _sut.OnUpdateRaceAndGenderBasedResources(1, 0,
            new List<IFaceGenItem> { beardItem },
            new List<IFaceGenItem>());

        beardItem.DidNotReceive().ImagePath = Arg.Any<string>();
    }

    [TestMethod]
    public void OnUpdateRaceAndGenderBasedResources_MultipleBeardItems_ReplacesAll()
    {
        var beard1 = Substitute.For<IFaceGenItem>();
        beard1.Index.Returns(1);
        var beard2 = Substitute.For<IFaceGenItem>();
        beard2.Index.Returns(2);
        _faceGenIconService.GetBeardName(1, 1, 0).Returns("sk_dwarf_beard_a_01");
        _faceGenIconService.GetBeardName(2, 1, 0).Returns("sk_dwarf_beard_a_02");

        _sut.OnUpdateRaceAndGenderBasedResources(1, 0,
            new List<IFaceGenItem> { beard1, beard2 },
            new List<IFaceGenItem>());

        beard1.Received(1).ImagePath = @"FaceGen\Beard\sk_dwarf_beard_a_01";
        beard2.Received(1).ImagePath = @"FaceGen\Beard\sk_dwarf_beard_a_02";
    }

    [TestMethod]
    public void OnUpdateRaceAndGenderBasedResources_EmptyLists_DoesNotThrow()
    {
        _sut.OnUpdateRaceAndGenderBasedResources(1, 0,
            new List<IFaceGenItem>(),
            new List<IFaceGenItem>());

        _faceGenIconService.DidNotReceive().GetBeardName(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
        _faceGenIconService.DidNotReceive().GetHairName(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }
}
