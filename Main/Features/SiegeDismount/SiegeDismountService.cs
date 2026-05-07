using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Features.SiegeDismount;

public class SiegeDismountService : ISiegeDismountService
{
    private readonly ISiegeDismountSettingsProvider _settings;
    private readonly IPlayerMountAdapter _mount;
    private readonly IPartyMountInventoryAdapter _inventory;
    private readonly IModLogger _logger;

    private IMountSnapshot? _capturedSnapshot;
    private bool _pendingRemount;

    public SiegeDismountService(
        ISiegeDismountSettingsProvider settings,
        IPlayerMountAdapter mount,
        IPartyMountInventoryAdapter inventory,
        IModLogger logger)
    {
        _settings = settings;
        _mount = mount;
        _inventory = inventory;
        _logger = logger;
    }

    public void OnMissionStart(bool isSiegeBattle, string? sceneName)
    {
        if (!_settings.IsEnabled)
        {
            _logger.LogInfo("[SiegeDismount] disabled via MCM — patches inert");
            return;
        }

        if (!IsSiegeMission(isSiegeBattle, sceneName))
            return;

        var behavior = _settings.MountBehavior;
        if (behavior == SiegeMountBehaviorType.Vanilla)
            return;

        _logger.LogInfo($"[SiegeDismount] siege detected — scene='{sceneName}' behavior={behavior}");

        if (!_mount.HasMount())
        {
            if (_settings.IsDebugMode)
                _logger.LogDebug("[SiegeDismount] player has no mount equipped — no action");
            return;
        }

        try
        {
            var snapshot = _mount.Capture();
            if (!snapshot.HasMount)
            {
                _logger.LogWarning("[SiegeDismount] HasMount returned true but capture was empty — skipping dismount");
                _capturedSnapshot = null;
                _pendingRemount = false;
                return;
            }
            _capturedSnapshot = snapshot;

            switch (behavior)
            {
                case SiegeMountBehaviorType.DismountKeepOnMap:
                    // The original developer's module advertised this mode as "horse spawns
                    // on map but player is on foot" — but the implementation never actually
                    // spawned a horse agent or cleared the equipment slot, so it was a silent
                    // no-op with the same effect as Vanilla. We're keeping the enum value for
                    // save-compat but treating it as a full no-op until somebody implements
                    // an actual map-side horse-spawn. The MCM hint reflects this honestly.
                    _logger.LogWarning("[SiegeDismount] DismountKeepOnMap — mode 1 is currently equivalent to Vanilla (Phase 1 port did not implement map-side horse spawn). No mount changes applied.");
                    _capturedSnapshot = null;
                    _pendingRemount = false;
                    break;

                case SiegeMountBehaviorType.DismountToInventory:
                    _mount.Clear();
                    _inventory.Deposit(_capturedSnapshot);
                    _pendingRemount = false;
                    if (_settings.IsDebugMode)
                        _logger.LogDebug("[SiegeDismount] DismountToInventory — moved to inventory");
                    break;

                case SiegeMountBehaviorType.AutoRemountAfter:
                    _mount.Clear();
                    _inventory.Deposit(_capturedSnapshot);
                    _pendingRemount = true;
                    if (_settings.IsDebugMode)
                        _logger.LogDebug("[SiegeDismount] AutoRemountAfter — moved to inventory, will restore on mission end");
                    break;

                default:
                    _logger.LogWarning($"[SiegeDismount] unknown SiegeMountBehavior value {(int)behavior} — treating as Vanilla (no-op). Check MCM JSON for an out-of-range setting.");
                    _capturedSnapshot = null;
                    _pendingRemount = false;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SiegeDismount] dismount failed: {ex.Message}");
            _pendingRemount = false;
            _capturedSnapshot = null;
        }
    }

    public void OnMissionEnd()
    {
        if (!_pendingRemount || _capturedSnapshot == null)
        {
            // Even when no auto-remount is pending, clear any stale snapshot so the
            // singleton doesn't carry mount-id strings between missions (state hygiene).
            _capturedSnapshot = null;
            return;
        }

        try
        {
            _mount.Restore(_capturedSnapshot);
            _inventory.Withdraw(_capturedSnapshot);
            _logger.LogInfo("[SiegeDismount] mount restored after siege");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SiegeDismount] remount failed: {ex.Message}");
        }
        finally
        {
            _pendingRemount = false;
            _capturedSnapshot = null;
        }
    }

    private static bool IsSiegeMission(bool isSiegeBattle, string? sceneName)
    {
        // Trust Mission.IsSiegeBattle exclusively. Earlier versions also matched scene-name
        // substrings ("siege", "assault", "breach") as a fallback for modded scenes that don't
        // set the engine flag — but Codex review found that 24 vanilla settlement Location
        // id="center" entries use scene names like "empire_siege_001". Those scenes can be
        // loaded as non-combat Missions (settlement-center cinematics, story events) where
        // IsSiegeBattle=false, and the keyword fallback would have falsely clobbered mounts.
        // Modded sieges that fail to set IsSiegeBattle won't trigger this feature; document
        // that requirement in the feature doc instead.
        return isSiegeBattle;
    }
}
