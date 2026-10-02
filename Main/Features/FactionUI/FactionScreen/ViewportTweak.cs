namespace TAOM.Features.FactionUI.FactionScreen;

/// <summary>How the faction screen's 3D viewport shows one faction's character (#704).</summary>
public sealed class ViewportTweak
{
    public ViewportTweak(float offset, bool hideWeapons, int? race)
    {
        Offset = offset;
        HideWeapons = hideWeapons;
        Race = race;
    }

    /// <summary>Pixels to move the viewport down.</summary>
    public float Offset { get; }

    public bool HideWeapons { get; }

    /// <summary>A display-only race index for the viewport (0 is human), for characters taller than
    /// the frame; null keeps the character's own.</summary>
    public int? Race { get; }
}
