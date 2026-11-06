using TaleWorlds.Localization;

namespace TAOM.Adapters;

/// <summary><see cref="ITextLocalizerAdapter"/> over <see cref="MBTextManager"/>.</summary>
public sealed class TextLocalizerAdapter : ITextLocalizerAdapter
{
    public string ActiveLanguage => MBTextManager.ActiveTextLanguage;
}
