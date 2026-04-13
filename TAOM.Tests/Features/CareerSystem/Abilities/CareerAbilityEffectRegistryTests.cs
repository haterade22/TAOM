using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem.Abilities;

namespace TAOM.Tests.Features.CareerSystem.Abilities;

[TestClass]
public class CareerAbilityEffectRegistryTests
{
    private CareerAbilityEffectRegistry _sut;

    [TestInitialize]
    public void SetUp()
    {
        _sut = new CareerAbilityEffectRegistry();
    }

    [TestMethod]
    public void Register_ThenLookup_ReturnsRegisteredExecutor()
    {
        // Arrange
        var executor = Substitute.For<ICareerAbilityEffectExecutor>();
        executor.CareerId.Returns("ranger_of_ithilien");
        _sut.Register(executor);

        // Act
        var result = _sut.GetExecutor("ranger_of_ithilien");

        // Assert
        Assert.AreSame(executor, result);
    }

    [TestMethod]
    public void GetExecutor_UnknownCareerId_ReturnsNoOpExecutor()
    {
        // Act
        var result = _sut.GetExecutor("unknown_career");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("__noop__", result.CareerId);
    }

    [TestMethod]
    public void GetExecutor_NoOpExecutor_ExecuteDoesNotThrow()
    {
        // Arrange
        var context = Substitute.For<IAbilityExecutionContext>();
        var executor = _sut.GetExecutor("nonexistent");

        // Act + Assert (no exception)
        executor.Execute(context);
        context.DidNotReceiveWithAnyArgs().ApplySpeedBuff(default, default);
    }

    [TestMethod]
    public void Register_MultipleExecutors_EachLookupReturnsCorrect()
    {
        // Arrange
        var exec1 = Substitute.For<ICareerAbilityEffectExecutor>();
        exec1.CareerId.Returns("ranger_of_ithilien");

        var exec2 = Substitute.For<ICareerAbilityEffectExecutor>();
        exec2.CareerId.Returns("black_uruk_captain");

        _sut.Register(exec1);
        _sut.Register(exec2);

        // Act
        var result1 = _sut.GetExecutor("ranger_of_ithilien");
        var result2 = _sut.GetExecutor("black_uruk_captain");

        // Assert
        Assert.AreSame(exec1, result1);
        Assert.AreSame(exec2, result2);
    }

    [TestMethod]
    public void Register_OverwritesSameCareerIdExecutor()
    {
        // Arrange
        var exec1 = Substitute.For<ICareerAbilityEffectExecutor>();
        exec1.CareerId.Returns("ranger_of_ithilien");

        var exec2 = Substitute.For<ICareerAbilityEffectExecutor>();
        exec2.CareerId.Returns("ranger_of_ithilien");

        _sut.Register(exec1);
        _sut.Register(exec2);

        // Act
        var result = _sut.GetExecutor("ranger_of_ithilien");

        // Assert
        Assert.AreSame(exec2, result);
    }
}
