using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.TextureAdvice;

namespace TAOM.Tests.Features.TextureAdvice;

[TestClass]
public class TextureAdviceServiceTests
{
    private ITextureAdviceAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private TextureAdviceService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<ITextureAdviceAdapter>();
        _adapter.TotalPhysicalMemoryMb.Returns(16321L);
        _adapter.TextureQuality.Returns(2);
        _adapter.TopTextureQuality.Returns(2);
        _adapter.WasAdviceShown().Returns(false);
        _logger = Substitute.For<IModLogger>();
        _sut = new TextureAdviceService(_adapter, _logger);
    }

    [TestMethod]
    public void AdviseOnce_AdviceDue_ShowsThenRemembers()
    {
        _sut.AdviseOnce();

        Received.InOrder(() =>
        {
            _adapter.ShowAdvice(16);
            _adapter.RememberAdviceShown();
        });
        _adapter.Received(1).ShowAdvice(Arg.Any<int>());
        _adapter.Received(1).RememberAdviceShown();
    }

    [TestMethod]
    public void AdviseOnce_NoAdvice_ShowsAndRemembersNothing_AndWritesOneDecisionLine()
    {
        _adapter.TotalPhysicalMemoryMb.Returns(32768L);

        _sut.AdviseOnce();

        _adapter.DidNotReceive().ShowAdvice(Arg.Any<int>());
        _adapter.DidNotReceive().RememberAdviceShown();
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("no advice")));
    }

    [TestMethod]
    public void AdviseOnce_SecondCallInTheSameLaunch_ReadsNothing()
    {
        _sut.AdviseOnce();
        _sut.AdviseOnce();

        _ = _adapter.Received(1).TotalPhysicalMemoryMb;
        _ = _adapter.Received(1).TextureQuality;
        _ = _adapter.Received(1).TopTextureQuality;
        _adapter.Received(1).WasAdviceShown();
        _adapter.Received(1).ShowAdvice(Arg.Any<int>());
        _adapter.Received(1).RememberAdviceShown();
    }

    [TestMethod]
    public void AdviseOnce_TextureQualityThrows_WritesOneWarningAndDoesNotThrow()
    {
        _adapter.TextureQuality.Returns(_ => throw new InvalidOperationException("boom"));

        _sut.AdviseOnce();

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.StartsWith("[TextureAdvice] no advice: ")));
        _adapter.DidNotReceive().ShowAdvice(Arg.Any<int>());
    }

    [TestMethod]
    public void AdviseOnce_RememberThrows_WarnsAboutTheNextLaunch_AndThePopupWasShown()
    {
        _adapter.When(a => a.RememberAdviceShown()).Do(_ => throw new InvalidOperationException("disk"));

        _sut.AdviseOnce();

        _adapter.Received(1).ShowAdvice(16);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("next launch")));
    }

    [TestMethod]
    public void AdviseOnce_LoggerThrows_EscapesNothing()
    {
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log"));
        _logger.When(l => l.LogWarning(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log"));
        _adapter.TextureQuality.Returns(_ => throw new InvalidOperationException("boom"));

        _sut.AdviseOnce();

        var second = new TextureAdviceService(Substitute.For<ITextureAdviceAdapter>(), _logger);
        second.AdviseOnce();
    }
}
