using TAOM.Features.CoopInterop;

namespace TAOM.Features.FactionUI.Menus;

/// <summary>
/// Whether the movie hook keeps vanilla's map bar (and the vanilla Camp overlay and army panel with it) under co-op. The themed bar's
/// time buttons bind TimeAccelerationMixin, which is registered under co-op too and only forwards vanilla's time change on
/// a peer that defers to a co-op server, so a BannerlordCoop client, its server process and solo play all get the themed
/// bar. A co-op mod whose roles TAOM cannot probe (BannerlordTogether) defers on every peer with no session TAOM can see:
/// it keeps the native bar, because the themed bar would drop any extension that mod makes to vanilla's MapBar prefab.
/// </summary>
public static class ThemedMapBarCoopPolicy
{
    /// <summary>The map bar, the Camp icon that sits in its clock's right wing, and the army panel cut at its stats plate
    /// (#1532); they swap together.</summary>
    public static bool IsMapBarMovie(string? movieName) =>
        movieName == "MapBar" || movieName == "TaomFieldCampOverlay" || movieName == "ArmyOverlay";

    /// <summary>Read live at each movie load: a session can start and end in one process.</summary>
    public static bool KeepsNativeMapBar(ICoopSessionProvider coop) => coop.ShouldDeferToHost && !coop.IsSessionActive;
}
