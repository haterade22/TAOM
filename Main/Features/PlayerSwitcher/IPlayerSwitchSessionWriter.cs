using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Write side of the selection. Split from the read side so that the only components able to
/// change the selection are the picker UI and the patch that clears it.
/// </summary>
public interface IPlayerSwitchSessionWriter
{
    void Select(HeroPickRow row);

    /// <summary>Clears the selection. Called on every BodyGeneratorView construction.</summary>
    void Clear();

    void SetPreviewActive(bool active);
}
