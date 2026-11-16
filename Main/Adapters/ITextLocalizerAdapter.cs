using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>Resolves a <c>{=key}English</c> string to the player's language, as a
/// <c>TextObject</c> would.</summary>
public interface ITextLocalizerAdapter
{
    string Localize(string raw);

    /// <summary>As <see cref="Localize(string)"/>, with each pair set as a text variable first, so a
    /// translation can put <c>{name}</c> where its grammar needs it. A name is case-sensitive.</summary>
    string Localize(string raw, IReadOnlyDictionary<string, string> variables);

    /// <summary>The game's text language now; it can change while the game runs.</summary>
    string ActiveLanguage { get; }
}
