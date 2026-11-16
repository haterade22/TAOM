using System.Collections.Generic;
using TaleWorlds.Localization;

namespace TAOM.Adapters;

/// <summary><see cref="ITextLocalizerAdapter"/> over <see cref="TextObject"/> and
/// <see cref="MBTextManager"/>.</summary>
public sealed class TextLocalizerAdapter : ITextLocalizerAdapter
{
    public string Localize(string raw) => string.IsNullOrEmpty(raw) ? "" : new TextObject(raw).ToString();

    public string Localize(string raw, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(raw))
            return "";
        var text = new TextObject(raw);
        foreach (var variable in variables)
            text.SetTextVariable(variable.Key, variable.Value);
        return text.ToString();
    }

    public string ActiveLanguage => MBTextManager.ActiveTextLanguage;
}
