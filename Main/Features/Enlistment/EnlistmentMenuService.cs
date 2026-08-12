using TAOM.Adapters;
using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

public class EnlistmentMenuService : IEnlistmentMenuService
{
    public const string ServiceWaitMenuId = "taom_enlistment_service_wait";

    private readonly IEnlistmentStore _store;
    private readonly IMobilePartyAttachmentAdapter _attachment;
    private readonly IModLogger _logger;

    // Built once in the ctor (singleton provider, process-stable config) — hot path.
    private readonly HashSet<string> _redirectIds;
    private readonly HashSet<string> _reportedUnknownIds = new HashSet<string>(StringComparer.Ordinal);

    public EnlistmentMenuService(
        IEnlistmentStore store,
        IEnlistmentConfigProvider config,
        IMobilePartyAttachmentAdapter attachment,
        IModLogger logger)
    {
        _store = store;
        _attachment = attachment;
        _logger = logger;
        _redirectIds = new HashSet<string>(
            config.GetConfig().RedirectMenuIds ?? new List<string>(), StringComparer.Ordinal);
    }

    /// <summary>
    /// Is this a menu enlistment may take over? Id-only — no state check — because the maintenance
    /// pump asks a different question from <see cref="TryRedirectMenu"/>: not "should I rewrite
    /// this push?" but "am I entitled to replace what is already on screen?". A settlement or
    /// encounter menu the player legitimately owns must never be closed out from under them.
    /// </summary>
    public bool IsRedirectable(string menuId) =>
        !string.IsNullOrEmpty(menuId) && _redirectIds.Contains(menuId);

    public bool TryRedirectMenu(string requestedMenuId, out string redirectedMenuId)
    {
        redirectedMenuId = null;
        if (string.IsNullOrEmpty(requestedMenuId) || requestedMenuId == ServiceWaitMenuId)
            return false;

        if (_store.Record.State != EnlistmentState.EnlistedAttached)
            return false;

        // NEVER redirect a battle menu while the player is genuinely IN a map event.
        //
        // EnlistedBattle is coerced to EnlistedAttached at save time, so a save taken
        // mid-battle reloads with the state saying "attached" while the engine still has the
        // player in the event and restores GameMenuId = "encounter". The redirect would then
        // swallow that menu — and MapEventManager.Tick deliberately SKIPS the player's own map
        // event, which advances only through PlayerEncounter.Update, driven from that menu.
        // The battle would never resolve, and the wait menu has no isLeave option to escape by.
        if (IsBattleMenu(requestedMenuId) && _attachment.GetPresenceFlags().IsInMapEvent)
        {
            _logger?.LogInfo($"[EnlistDiag] not redirecting '{requestedMenuId}' — the player is in a live map event and this menu is the only thing that advances it");
            return false;
        }

        // SHORE LEAVE (field report 1). The player is genuinely inside the settlement already —
        // EnterSettlementAction.ApplyForParty ran on the follow path — and this redirect was the only
        // thing standing between him and the market he is standing in. While the pass is held, the
        // settlement's own menu is his.
        //
        // Narrow on purpose: only the three settlement menus are released, and only while the column
        // rests there. Every other redirect, and the whole of service, still applies — this is a pass
        // to walk the town, not a suspension of enlistment.
        if (_store.Record.OnTownLeave && IsSettlementMenu(requestedMenuId))
        {
            _logger?.LogInfo($"[Enlistment] shore leave — '{requestedMenuId}' left to the player");
            return false;
        }

        if (_redirectIds.Contains(requestedMenuId))
        {
            redirectedMenuId = ServiceWaitMenuId;
            return true;
        }

        // Fail-open + diagnostic funnel: an unlisted menu id flows through untouched, and
        // one log line per id tells us whether the redirect list needs extending. Capped —
        // the distinct-menu-id universe is small, but a runaway producer must not leak.
        if (_reportedUnknownIds.Count > 100)
            _reportedUnknownIds.Clear();
        if (_reportedUnknownIds.Add(requestedMenuId))
            _logger?.LogInfo($"[Enlistment] menu '{requestedMenuId}' pushed while attached and NOT redirected — extend RedirectMenuIds if service state breaks here");
        return false;
    }

    /// <summary>Menus that drive a live PlayerEncounter rather than merely describing state.</summary>
    private static bool IsBattleMenu(string menuId) =>
        menuId == "encounter" || menuId == "join_encounter";

    /// <summary>
    /// The three settlement menus shore leave releases. Deliberately its own list rather than a
    /// subtraction from <c>RedirectMenuIds</c>: that list also carries the settlement APPROACH menus
    /// (<c>town_outside</c>, <c>village_outside</c>, the port menus), which stay redirected even on
    /// leave — a soldier on a pass is already inside, and has no business back on the map deciding
    /// whether to besiege the place.
    /// </summary>
    private static bool IsSettlementMenu(string menuId) =>
        menuId == "town" || menuId == "castle" || menuId == "village";
}
