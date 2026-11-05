using System;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.TextureAdvice;

/// <summary>
/// Reads memory and Texture Quality once, logs the decision, shows the advice when due and only then records it, so a
/// marker that cannot be written costs one repeat at the next launch and never a popup that was promised and not shown.
/// </summary>
public sealed class TextureAdviceService : ITextureAdviceService
{
    private readonly ITextureAdviceAdapter _adapter;
    private readonly IModLogger _logger;
    private bool _ran;

    public TextureAdviceService(ITextureAdviceAdapter adapter, IModLogger logger)
    {
        _adapter = adapter;
        _logger = logger;
    }

    public void AdviseOnce()
    {
        if (_ran) return;
        _ran = true;

        var shown = false;
        try
        {
            var ram = _adapter.TotalPhysicalMemoryMb;
            var quality = _adapter.TextureQuality;
            var top = _adapter.TopTextureQuality;
            var shownBefore = _adapter.WasAdviceShown();
            var advise = TextureAdviceRule.ShouldAdvise(ram, quality, top, shownBefore);

            Info(TextureAdviceLines.BuildDecisionLine(ram, quality, top, shownBefore, advise));

            if (advise)
            {
                _adapter.ShowAdvice(TextureAdviceRule.RoundedGigabytes(ram));
                shown = true;
            }
        }
        catch (Exception ex)
        {
            Warn(TextureAdviceLines.BuildFailedLine(ex));
            return;
        }

        if (!shown) return;
        try { _adapter.RememberAdviceShown(); }
        catch (Exception ex) { Warn(TextureAdviceLines.BuildNotRememberedLine(ex)); }
    }

    private void Info(string line)
    {
        try { _logger.LogInfo(line); }
        catch { }
    }

    private void Warn(string line)
    {
        try { _logger.LogWarning(line); }
        catch { }
    }
}
