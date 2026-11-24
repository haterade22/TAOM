namespace TAOM.Features.FactionUI.FactionScreen;

/// <summary>One named-hero card on the faction screen (#704).</summary>
public sealed class SpecialCharacter
{
    public SpecialCharacter(string fallbackName, string characterId, string portraitSprite)
    {
        FallbackName = fallbackName;
        CharacterId = characterId;
        PortraitSprite = portraitSprite;
    }

    /// <summary>Shown only if the character cannot be found; otherwise its own localized name is used.</summary>
    public string FallbackName { get; }

    public string CharacterId { get; }

    public string PortraitSprite { get; }
}
