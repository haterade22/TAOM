using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

public class EnlistmentMenuService : IEnlistmentMenuService
{
    public const string ServiceWaitMenuId = "taom_enlistment_service_wait";

    private readonly IEnlistmentStore _store;
    private readonly IModLogger _logger;

    // Built once in the ctor (singleton provider, process-stable config) — hot path.
    private readonly HashSet<string> _redirectIds;
    private readonly HashSet<string> _reportedUnknownIds = new HashSet<string>(StringComparer.Ordinal);

    public EnlistmentMenuService(IEnlistmentStore store, IEnlistmentConfigProvider config, IModLogger logger)
    {
        _store = store;
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
}
