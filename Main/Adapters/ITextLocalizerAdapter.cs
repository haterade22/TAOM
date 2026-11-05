namespace TAOM.Adapters;

/// <summary>The game's text language, read through an adapter so no service touches <c>MBTextManager</c> (ADR-007).</summary>
public interface ITextLocalizerAdapter
{
    /// <summary>The game's text language now; it can change while the game runs.</summary>
    string ActiveLanguage { get; }
}
