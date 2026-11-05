using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ButterLibDistanceMatrix;

namespace TAOM.Tests.Features.ButterLibDistanceMatrix;

/// <summary>
/// The switch at the first main menu (#740): ButterLib's Distance Matrix goes off, and one log line says what happened.
/// </summary>
[TestClass]
public class DistanceMatrixSwitchTests
{
    private IButterLibDistanceMatrixAdapter _adapter = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IButterLibDistanceMatrixAdapter>();
        _logger = Substitute.For<IModLogger>();
    }

    private void Apply() => new DistanceMatrixSwitch(_adapter, _logger).Apply();

    private void AdapterReturns(string? problem, bool wasAlreadyOff) =>
        _adapter.TryDisable(out Arg.Any<bool>()).Returns(call =>
        {
            call[0] = wasAlreadyOff;
            return problem;
        });

    [TestMethod]
    public void Apply_SwitchedOff_LogsItOnce()
    {
        AdapterReturns(null, wasAlreadyOff: false);

        Apply();

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.StartsWith("[ButterLibDistance] switched off")));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Apply_AlreadyOff_SaysSoInsteadOfClaimingTheSwitch()
    {
        AdapterReturns(null, wasAlreadyOff: true);

        Apply();

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("was already off")));
        _logger.DidNotReceive().LogInfo(Arg.Is<string>(s => s.Contains("switched off")));
    }

    [TestMethod]
    public void Apply_NotSwitchedOff_WarnsWithTheReason()
    {
        AdapterReturns("ButterLib's DistanceMatrixSubSystem is not loaded", wasAlreadyOff: false);

        Apply();

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.StartsWith("[ButterLibDistance] not switched off:") && s.Contains("not loaded")));
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void Apply_AdapterThrows_WarnsAndDoesNotThrow()
    {
        _adapter.TryDisable(out Arg.Any<bool>()).Returns(_ => throw new InvalidOperationException("boom"));

        Apply();

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.StartsWith("[ButterLibDistance]") && s.Contains("boom")));
    }
}
