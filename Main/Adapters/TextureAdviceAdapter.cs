using System;
using System.IO;
using TaleWorlds.Engine.Options;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Validation;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Adapters;

/// <summary>
/// Engine side of the texture advice: the Texture Quality option (NativeOptions.GetConfig, v1.4.8 :274; three levels per
/// NativeSelectionOptionData.GetOptionsLimit :39), installed memory through TAOM's own GlobalMemoryStatusEx reader, a
/// marker file beside the TAOM log ("Logs", as BattleLoadStallMarker), and the popup (StallReportNotifier's shape).
/// </summary>
public sealed class TextureAdviceAdapter : ITextureAdviceAdapter
{
    private const string MarkerFileName = "taom-texture-advice.shown";

    // A sanity bound that rejects garbage from the engine store, not the length of the options list.
    private const float MaxRawOption = 16f;

    private readonly string _markerPath;

    public TextureAdviceAdapter() : this(Path.Combine("Logs", MarkerFileName)) { }

    // Test seam; internal so DryIoc sees one public ctor.
    internal TextureAdviceAdapter(string markerPath) => _markerPath = markerPath;

    public int TextureQuality
    {
        get
        {
            var raw = NativeOptions.GetConfig(NativeOptions.NativeOptionsType.TextureQuality);
            return FiniteFloatValidator.IsFiniteInRange(raw, 0f, MaxRawOption) ? (int)Math.Round(raw) : -1;
        }
    }

    public int TopTextureQuality => NativeSelectionOptionData.GetOptionsLimit(NativeOptions.NativeOptionsType.TextureQuality) - 1;

    public long TotalPhysicalMemoryMb => MemorySampleReader.TryRead(out var sample) ? sample.TotalPhysMb : 0;

    public bool WasAdviceShown() => File.Exists(_markerPath);

    public void RememberAdviceShown()
    {
        var dir = Path.GetDirectoryName(_markerPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_markerPath, DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
    }

    public void ShowAdvice(int totalGigabytes)
    {
        var body = new TextObject("{=taom_texture_advice_body}This computer has about {RAM} GB of memory and Texture Quality is at its highest setting. TAOM's campaign map and items fill a lot of that memory, and on a machine like this the game can stutter or run out of memory. Lowering Texture Quality by one step (Options, Performance, Graphics) is the largest memory saving the game offers, and Foliage Quality is the next. Both take effect at once, with no restart. This message will not show again.");
        body.SetTextVariable("RAM", totalGigabytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
        InformationManager.ShowInquiry(new InquiryData(
            titleText: new TextObject("{=taom_texture_advice_title}TAOM: memory advice").ToString(),
            text: body.ToString(),
            isAffirmativeOptionShown: true,
            isNegativeOptionShown: false,
            affirmativeText: new TextObject("{=taom_texture_advice_ok}Understood").ToString(),
            negativeText: string.Empty,
            affirmativeAction: () => { },
            negativeAction: () => { }),
            pauseGameActiveState: false);
    }
}
