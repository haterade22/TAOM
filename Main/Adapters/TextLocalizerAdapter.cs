using TaleWorlds.Localization;

namespace TAOM.Adapters;

/// <summary><see cref="ITextLocalizerAdapter"/> over <see cref="TextObject"/> and
/// <see cref="MBTextManager"/>.</summary>
public sealed class TextLocalizerAdapter : ITextLocalizerAdapter
{
    public string Localize(string raw) => string.IsNullOrEmpty(raw) ? "" : new TextObject(raw).ToString();

    public string ActiveLanguage => MBTextManager.ActiveTextLanguage;
}
