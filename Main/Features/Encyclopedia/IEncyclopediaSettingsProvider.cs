namespace TAOM.Features.Encyclopedia;

/// <summary>
/// Encyclopedia-feature settings boundary. Decouples <see cref="Models.TaomInformationRestrictionModel"/>
/// from the concrete <c>TaomSettings</c> singleton (csharp-architecture.md "Constructor injection only").
/// Phase 9b #145.
/// </summary>
public interface IEncyclopediaSettingsProvider
{
    /// <summary>
    /// When true, all encyclopedia characters are considered "known" by the player —
    /// `DoesPlayerKnowDetailsOf` returns true without calling vanilla.
    /// </summary>
    bool ShowAllEncyclopediaCharacters { get; }
}
