using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Abilities.Executors;

namespace TAOM.Tests.Features.CareerSystem.Abilities;

[TestClass]
public class StealthExecutorTests
{
    private StealthExecutor _sut;
    private IAbilityExecutionContext _context;

    [TestInitialize]
    public void SetUp()
    {
        _sut = new StealthExecutor();
        _context = Substitute.For<IAbilityExecutionContext>();
        _context.Duration.Returns(10f);
    }

    [TestMethod]
    public void CareerId_ReturnsRangerOfIthilien()
    {
        Assert.AreEqual("ranger_of_ithilien", _sut.CareerId);
    }

    [TestMethod]
    public void Execute_AppliesSpeedBuff()
    {
        // Act
        _sut.Execute(_context);

        // Assert
        _context.Received(1).ApplySpeedBuff(Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_AppliesStealthMode()
    {
        // Act
        _sut.Execute(_context);

        // Assert
        _context.Received(1).ApplyStealthMode(Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_SpeedBuffUsesDuration()
    {
        // Arrange
        _context.Duration.Returns(15f);

        // Act
        _sut.Execute(_context);

        // Assert
        _context.Received(1).ApplySpeedBuff(Arg.Any<float>(), 15f);
    }

    [TestMethod]
    public void Execute_StealthModeUsesDuration()
    {
        // Arrange
        _context.Duration.Returns(12f);

        // Act
        _sut.Execute(_context);

        // Assert
        _context.Received(1).ApplyStealthMode(12f);
    }

    [TestMethod]
    public void Execute_SpeedMultiplierIsGreaterThanOne()
    {
        // Arrange
        float capturedMultiplier = 0f;
        _context.When(c => c.ApplySpeedBuff(Arg.Any<float>(), Arg.Any<float>()))
            .Do(ci => capturedMultiplier = (float)ci[0]);

        // Act
        _sut.Execute(_context);

        // Assert
        Assert.IsTrue(capturedMultiplier > 1f, $"Speed multiplier should be > 1, was {capturedMultiplier}");
    }
}
