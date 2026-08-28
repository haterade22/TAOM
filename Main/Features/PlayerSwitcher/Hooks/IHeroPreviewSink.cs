using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher.Hooks;

/// <summary>
/// Drives the live 3D character in the face generator so the player sees the lord they are about
/// to become. Kept behind an interface so the picker ViewModel holds no engine view reference.
/// </summary>
public interface IHeroPreviewSink
{
    /// <summary>Shows the chosen lord: their face, race, gender and battle gear.</summary>
    void ApplyPreview(HeroPickRow row);

    /// <summary>Puts the player's own character back, exactly as it was before the first preview.</summary>
    void RestoreDefault();
}
