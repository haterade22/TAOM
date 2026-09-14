using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.SpecialResources;
using TAOM.Features.TroopWeight;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// v1.5.0 made OnCharacterCreationIsOverEvent an MbEvent&lt;int&gt; fired ten times (index 0..9). The
/// starting resource is seeded in the last phase only; the positive case reads Hero.MainHero, so it
/// is live-game.
/// </summary>
[TestClass]
public class SpecialResourcesBehaviorPhaseGuardTests
{
    private ISpecialResourceService _service;
    private SpecialResourcesBehavior _sut;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<ISpecialResourceService>();
        _sut = new SpecialResourcesBehavior(
            _service,
            Substitute.For<ISpecialResourceStorageService>(),
            Substitute.For<ISpecialResourceConfigProvider>(),
            Substitute.For<IModLogger>(),
            Substitute.For<ITroopWeightService>(),
            Substitute.For<IDedicatedServerProvider>());
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public void OnCharacterCreationIsOver_BeforeTheLastPhase_DoesNotSeed(int index)
    {
        _sut.OnCharacterCreationIsOver(index);

        _service.DidNotReceive().InitializeHero(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }
}
