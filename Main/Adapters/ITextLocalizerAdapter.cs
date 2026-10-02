namespace TAOM.Adapters;

/// <summary>Resolves a <c>{=key}English</c> string to the player's language, as a
/// <c>TextObject</c> would.</summary>
public interface ITextLocalizerAdapter
{
    string Localize(string raw);

    /// <summary>The game's text language now; it can change while the game runs.</summary>
    string ActiveLanguage { get; }
}
