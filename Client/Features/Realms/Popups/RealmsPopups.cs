using System;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using TAOM.Features.Realms;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Client.Features.Realms.Popups;

/// <summary>
/// Opens the Realms popups from the encyclopedia title sections, one at a time, on whatever screen is on top. A second
/// click while one is open does nothing; a throw while opening closes the half-built layer and is logged.
/// </summary>
internal static class RealmsPopups
{
    private static RealmsPopupLayer? _layer;

    /// <summary>The whole title tree around a title, that title highlighted.</summary>
    public static void OpenTree(string? titleId)
    {
        if (!TryBehavior(out var b))
            return;
        var tree = TitleTree.Build(b.Service.Catalog, titleId, b.Service.LandsShownUnder);
        if (tree != null)
            Open("RealmsTitleTreePopup", close => new TitleTreePopupVM(b, tree, close));
    }

    /// <summary>Portraits of everyone claiming a title.</summary>
    public static void OpenClaimants(string? titleId)
    {
        if (!TryBehavior(out var b))
            return;
        var title = b.Service.Catalog.Get(titleId);
        if (title != null && b.Service.Ledger.ClaimsOn(title.Id).Count > 0)
            Open("RealmsClaimantsPopup", close => new ClaimantsPopupVM(b, title, close));
    }

    /// <summary>Closes the open popup, if any (a tree card opening a title's page closes the tree first).</summary>
    public static void Close() => _layer?.Close();

    /// <summary>A popup is open; a second one waits (event windows) or is not opened (the title popups).</summary>
    public static bool IsOpen => _layer != null;

    /// <summary>
    /// A story event window (<see cref="EventWindow.EventWindows"/>), which pauses the campaign while it shows, as the
    /// inquiry it replaced did.
    /// </summary>
    public static void OpenEvent(string movie, Func<Action, ViewModel> build) => Open(movie, build, pausesGame: true);

    private static void Open(string movie, Func<Action, ViewModel> build, bool pausesGame = false)
    {
        if (_layer != null)
            return;
        var host = ScreenManager.TopScreen;
        if (host == null)
            return;
        RealmsPopupLayer? layer = null;
        try
        {
            layer = new RealmsPopupLayer(host, Closed, pausesGame);
            _layer = layer;
            layer.Show(movie, build);
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] opening {movie} threw: {ex}");
            _layer = null;
            layer?.Close();
        }
    }

    private static void Closed(RealmsPopupLayer layer)
    {
        if (_layer == layer)
            _layer = null;
    }

    private static bool TryBehavior(out RealmsCampaign behavior)
    {
        behavior = RealmsCampaign.Current!;
        return behavior != null && behavior.EnsureInitialized();
    }
}
