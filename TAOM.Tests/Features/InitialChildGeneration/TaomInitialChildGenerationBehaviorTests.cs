using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.InitialChildGeneration;

namespace TAOM.Tests.Features.InitialChildGeneration;

[TestClass]
public class TaomInitialChildGenerationBehaviorTests
{
    private IInitialChildGenerationService _service;
    private TaomInitialChildGenerationBehavior _sut;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<IInitialChildGenerationService>();
        _sut = new TaomInitialChildGenerationBehavior(_service);
    }

    [TestMethod]
    public void OnNewGameCreatedPartialFollowUp_Index0_CallsService()
    {
        _sut.OnNewGameCreatedPartialFollowUp(null, 0);

        _service.Received(1).GenerateInitialChildren();
    }

    [TestMethod]
    public void OnNewGameCreatedPartialFollowUp_IndexNot0_DoesNotCallService()
    {
        _sut.OnNewGameCreatedPartialFollowUp(null, 1);
        _sut.OnNewGameCreatedPartialFollowUp(null, 5);

        _service.DidNotReceive().GenerateInitialChildren();
    }
}
