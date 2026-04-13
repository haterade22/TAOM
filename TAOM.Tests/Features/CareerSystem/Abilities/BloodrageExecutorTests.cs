using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Abilities.Executors;

namespace TAOM.Tests.Features.CareerSystem.Abilities;

[TestClass]
public class BloodrageExecutorTests
{
    private BloodrageExecutor _sut;
    private IAbilityExecutionContext _context;

    [TestInitialize]
    public void SetUp()
    {
        _sut = new BloodrageExecutor();
        _context = Substitute.For<IAbilityExecutionContext>();
        _context.Duration.Returns(8f);
    }

    [TestMethod]
    public void CareerId_ReturnsBlackUrukCaptain()
    {
        Assert.AreEqual("black_uruk_captain", _sut.CareerId);
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
    public void Execute_AppliesResistanceBuff()
    {
        // Act
        _sut.Execute(_context);

        // Assert
        _context.Received(1).ApplyResistanceBuff(Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_DamageBuffUsesDuration()
    {
        // Arrange
        _context.Duration.Returns(8f);

        // Act
        _sut.Execute(_context);

        // Assert
        _context.Received(1).ApplyDamageBuff(Arg.Any<float>(), 8f);
    }

    [TestMethod]
    public void Execute_ResistanceBuffUsesDuration()
    {
        // Arrange
        _context.Duration.Returns(8f);

        // Act
        _sut.Execute(_context);

        // Assert
        _context.Received(1).ApplyResistanceBuff(Arg.Any<float>(), 8f);
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
    public void Execute_ResistanceMultiplierIsGreaterThanOne()
    {
        // Arrange
        float capturedMultiplier = 0f;
        _context.When(c => c.ApplyResistanceBuff(Arg.Any<float>(), Arg.Any<float>()))
            .Do(ci => capturedMultiplier = (float)ci[0]);

        // Act
        _sut.Execute(_context);

        // Assert
        Assert.IsTrue(capturedMultiplier > 1f, $"Resistance multiplier should be > 1, was {capturedMultiplier}");
    }
}
