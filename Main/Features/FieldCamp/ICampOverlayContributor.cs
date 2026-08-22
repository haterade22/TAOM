namespace TAOM.Features.FieldCamp;

/// <summary>Status line and 0..100 progress for the overlay panel.</summary>
public readonly struct CampOverlayStatus
{
    public CampOverlayStatus(string text, int progressPercent)
    {
        Text = text;
        ProgressPercent = progressPercent;
    }

    public string Text { get; }
    public int ProgressPercent { get; }
}

/// <summary>
/// Seam for features that extend the camp overlay and menus (Refuge, in Phase 3). Replaces the
/// source module's three mutable static delegates on FieldCampButtonHook with a resolvable
/// interface: contributors are ResolveMany'd at the boundary, first non-null answer wins.
/// </summary>
public interface ICampOverlayContributor
{
    /// <summary>Overrides the overlay button caption, or null for the default.</summary>
    string CaptionOverride();

    /// <summary>Blocks camp creation with a player-facing reason, or null to allow.</summary>
    string CreationBlockedReason();

    /// <summary>Extra overlay status (a refuge raising nearby), or null when idle.</summary>
    CampOverlayStatus? OverlayStatus();
}
