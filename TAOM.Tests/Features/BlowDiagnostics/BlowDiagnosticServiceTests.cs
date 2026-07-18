using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System;
using TAOM.Core.Logging;
using TAOM.Features.BlowDiagnostics;
using TAOM.Features.BlowDiagnostics.Domain;

namespace TAOM.Tests.Features.BlowDiagnostics;

[TestClass]
public class BlowDiagnosticServiceTests
{
    private IModLogger _logger;
    private IBlowDiagnosticsSettingsProvider _settings;
    private BlowDiagnosticService _sut;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _settings = Substitute.For<IBlowDiagnosticsSettingsProvider>();
        _settings.IsEnabled.Returns(true);
        _sut = new BlowDiagnosticService(_logger, _settings);
    }

    private static BlowDiagRecord Sample() => new BlowDiagRecord
    {
        VictimName = "Thorgran",
        VictimRace = 1,
        VictimIsPlayer = true,
        VictimIsMounted = false,
        MountMonster = "",
        VictimHealth = 12.4f,
        BlowFlags = "KnockDown",
        DamageType = "Blunt",
        InflictedDamage = 47,
        BaseMagnitude = 30.5f,
        IsMissile = true,
        IsFallDamage = false,
        VictimBodyPart = "Chest",
        AttackerIndex = 88
    };

    // ---- enable gate ----

    [TestMethod]
    public void IsEnabled_MirrorsProvider()
    {
        _settings.IsEnabled.Returns(false);
        Assert.IsFalse(_sut.IsEnabled);
        _settings.IsEnabled.Returns(true);
        Assert.IsTrue(_sut.IsEnabled);
    }

    [TestMethod]
    public void LogBlow_Disabled_DoesNotLog()
    {
        _settings.IsEnabled.Returns(false);
        _sut.LogBlow(Sample());
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void LogDeath_Disabled_DoesNotLog()
    {
        _settings.IsEnabled.Returns(false);
        _sut.LogDeath(Sample());
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void LogSiegeShot_Disabled_DoesNotLog()
    {
        _settings.IsEnabled.Returns(false);
        _sut.LogSiegeShot("pot_projectile", "Defender");
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    // ---- durable level ----

    [TestMethod]
    public void LogBlow_Enabled_UsesDurableLogInfo_NotDebug()
    {
        _sut.LogBlow(Sample());
        _logger.Received().LogInfo(Arg.Any<string>());
        _logger.DidNotReceive().LogDebug(Arg.Any<string>());
    }

    // ---- blow format ----

    [TestMethod]
    public void LogBlow_Enabled_EmitsTagKindAndKeyFields()
    {
        _sut.LogBlow(Sample());
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("[BlowDiag]") && s.Contains(" blow ") &&
            s.Contains("victim='Thorgran'") && s.Contains("race=1") &&
            s.Contains("player=True") && s.Contains("flags=KnockDown") &&
            s.Contains("dmgType=Blunt") && s.Contains("dmg=47") &&
            s.Contains("missile=True") && s.Contains("part=Chest") &&
            s.Contains("attackerIdx=88")));
    }

    [TestMethod]
    public void LogBlow_NotMounted_OmitsMountField()
    {
        _sut.LogBlow(Sample());
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("mounted=False") && !s.Contains("mount='")));
    }

    [TestMethod]
    public void LogBlow_Mounted_IncludesMountMonster()
    {
        var r = Sample();
        r.VictimIsMounted = true;
        r.MountMonster = "spider_mount_a";
        _sut.LogBlow(r);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("mounted=True") && s.Contains("mount='spider_mount_a'")));
    }

    // ---- death format ----

    [TestMethod]
    public void LogDeath_Enabled_EmitsDieKind()
    {
        _sut.LogDeath(Sample());
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("[BlowDiag]") && s.Contains(" DIE ") && s.Contains("victim='Thorgran'")));
    }

    // ---- siege-shot format ----

    [TestMethod]
    public void LogSiegeShot_Enabled_EmitsItemAndSide()
    {
        _sut.LogSiegeShot("pot_projectile", "Defender");
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("[BlowDiag]") && s.Contains("siege-shot") &&
            s.Contains("item='pot_projectile'") && s.Contains("side=Defender")));
    }

    [TestMethod]
    public void LogSiegeShot_NullItem_EmitsNullMarker()
    {
        _sut.LogSiegeShot(null, "Defender");
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("item='<null>'")));
    }

    // ---- resilience ----

    [TestMethod]
    public void LogBlow_NullRecord_DoesNotLogOrThrow()
    {
        _sut.LogBlow(null);
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void LogBlow_LoggerThrows_DoesNotPropagate()
    {
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Throw(new InvalidOperationException("sink died"));
        _sut.LogBlow(Sample());
        // Reaching here without an exception is the assertion.
    }

    [TestMethod]
    public void LogSiegeShot_LoggerThrows_DoesNotPropagate()
    {
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Throw(new InvalidOperationException("sink died"));
        _sut.LogSiegeShot("pot_projectile", "Defender");
    }
}
