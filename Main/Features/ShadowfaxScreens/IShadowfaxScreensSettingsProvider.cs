namespace TAOM.Features.ShadowfaxScreens;

/// <summary>
/// The "Screen Performance" group of the Battle Load Diagnostics MCM page. Every getter is read per screen open,
/// so a switch applies to the next open without a restart.
/// Contract: no getter ever throws. An unreadable switch (no MCM instance, a faulting read) returns its shipped default,
/// fast opens on and the timer off, so a caller needs no try/catch around a read.
/// </summary>
public interface IShadowfaxScreensSettingsProvider
{
    /// <summary>Whether each screen open writes a [ScreenOpen] line. Default off.</summary>
    bool LogScreenOpenTimes { get; }

    /// <summary>Default on. Off restores vanilla's build of the Clan screen.</summary>
    bool ClanFastOpen { get; }

    /// <summary>Default on. Off restores vanilla's build of the Inventory screen.</summary>
    bool InventoryFastOpen { get; }

    /// <summary>Default on. Off restores vanilla's build of the Party screen.</summary>
    bool PartyFastOpen { get; }

    /// <summary>Default on. Off restores vanilla's build of the Quest log.</summary>
    bool QuestLogFastOpen { get; }
}
