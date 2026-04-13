using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Abilities.Executors;

namespace TAOM.Tests.Features.CareerSystem.Abilities;

[TestClass]
public class StampedeExecutorTests
{
    private StampedeExecutor _sut;
    private IAbilityExecutionContext _context;

    [TestInitialize]
    public void SetUp()
    {
        _sut = new StampedeExecutor();
        _context = Substitute.For<IAbilityExecutionContext>();
        _context.Duration.Returns(12f);
        _context.Radius.Returns(20f);
    }

    [TestMethod]
    public void CareerId_ReturnsKnightOfBelfalas()
    {
        Assert.AreEqual("knight_of_belfalas", _sut.CareerId);
    }

    [TestMethod]
    public void Execute_AppliesDamageBuff()
    {
        // Act
        _sut.Execute(_context);

        // Assert
        _context.Received(1).ApplyDamageBuff(Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_AppliesMoraleBurst()
    {
        // Act
        _sut.Execute(_context);

        // Assert
        _context.Received(1).ApplyMoraleBurst(Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_DamageBuffUsesDuration()
    {
        // Arrange
        _context.Duration.Returns(12f);

        // Act
        _sut.Execute(_context);

        // Assert
        _context.Received(1).ApplyDamageBuff(Arg.Any<float>(), 12f);
    }

    [TestMethod]
    public void Execute_MoraleBurstUsesRadius()
    {
        // Arrange
        _context.Radius.Returns(20f);

        // Act
        _sut.Execute(_context);

        // Assert
        _context.Received(1).ApplyMoraleBurst(20f, Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_DamageMultiplierIsGreaterThanOne()
    {
        // Arrange
        float capturedMultiplier = 0f;
        _context.When(c => c.ApplyDamageBuff(Arg.Any<float>(), Arg.Any<float>()))
            .Do(ci => capturedMultiplier = (float)ci[0]);

        // Act
        _sut.Execute(_context);

        // Assert
        Assert.IsTrue(capturedMultiplier > 1f, $"Damage multiplier should be > 1, was {capturedMultiplier}");
    }

    [TestMethod]
    public void Execute_MoraleMagnitudeIsPositive()
    {
        // Arrange
        float capturedMagnitude = 0f;
        _context.When(c => c.ApplyMoraleBurst(Arg.Any<float>(), Arg.Any<float>()))
            .Do(ci => capturedMagnitude = (float)ci[1]);

        // Act
        _sut.Execute(_context);

        // Assert
        Assert.IsTrue(capturedMagnitude > 0f, $"Morale magnitude should be > 0, was {capturedMagnitude}");
    }
}
