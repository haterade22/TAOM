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
        var tree = TitleTree.Build(b.Service.Catalog, titleId);
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

    /// <summary>The coronation's event window (<see cref="CoronationPopupVM"/>).</summary>
    public static void OpenCoronation(Func<Action, CoronationPopupVM> build) =>
        Open("RealmsCoronationPopup", close => build(close));

    private static void Open(string movie, Func<Action, ViewModel> build)
    {
        if (_layer != null)
            return;
        var host = ScreenManager.TopScreen;
        if (host == null)
            return;
        RealmsPopupLayer? layer = null;
        try
        {
            layer = new RealmsPopupLayer(host, Closed);
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
