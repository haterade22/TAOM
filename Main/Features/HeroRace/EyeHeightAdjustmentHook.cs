using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Features.HeroRace.Hooks;
using TAOM.Core.Logging;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace TAOM.Features.HeroRace;

/// <summary>
/// Lowers a dwarf eye height so the third-person camera sits at the head rather than floating above
/// it. Applied to the SHARED base monster for the race, so it affects every dwarf agent, not only
/// the player.
///
/// <para>The offset is measured from the HUMAN baseline, not from the dwarf own value — a dwarf
/// ends up at human height minus the adjuster. That is the behaviour this feature shipped with and
/// is deliberately preserved.</para>
///
/// <para>Because the monster instance is shared and mutated in place, the pre-mutation values are
/// captured on first sight and written back when the feature is switched off. Without that, turning
/// the MCM toggle off would leave the last applied value baked in until a process restart, which is
/// the "toggle that does not toggle" trap.</para>
///
/// <para><b>The write-back is deferred, not immediate.</b> This hook only runs as a postfix on
/// <c>FaceGen.GetBaseMonsterFromRace</c>, so flipping the MCM toggle changes nothing by itself: the
/// shared monster is put back the next time something asks the engine for the dwarf base monster,
/// which is the next dwarf visual or tableau build. In practice that is the next time a dwarf is
/// drawn, but on a map with no dwarf on screen it can be a while. The MCM hint says so rather than
/// promising an instant revert, because an honest hint beats machinery MCM does not cleanly
/// provide.</para>
/// </summary>
public class EyeHeightAdjustmentHook : IOnFaceGenGetBaseMonsterFromRace
{
    private const string StandingBackingField = "<StandingEyeHeight>k__BackingField";
    private const string CrouchBackingField = "<CrouchEyeHeight>k__BackingField";

    private readonly IRaceManager _raceManager;
    private readonly IFaceGenAdapter _faceGenAdapter;
    private readonly IHeroRaceSettingsProvider _settings;
    private readonly IReflectionService _reflection;
    private readonly IModLogger _logger;

    // Pre-mutation eye heights, keyed by monster StringId, so the override can be undone.
    private readonly Dictionary<string, VanillaEyeHeights> _vanillaHeights =
        new Dictionary<string, VanillaEyeHeights>(StringComparer.Ordinal);

    private Monster _defaultMonster;
    private bool _initialized;

    private readonly struct VanillaEyeHeights
    {
        public VanillaEyeHeights(float standing, float crouch)
        {
            Standing = standing;
            Crouch = crouch;
        }

        public float Standing { get; }
        public float Crouch { get; }
    }

    public EyeHeightAdjustmentHook(
        IRaceManager raceManager,
        IFaceGenAdapter faceGenAdapter,
        IHeroRaceSettingsProvider settings,
        IReflectionService reflection,
        IModLogger logger)
    {
        _raceManager = raceManager;
        _faceGenAdapter = faceGenAdapter;
        _settings = settings;
        _reflection = reflection;
        _logger = logger;
    }

    public void OnGetBaseMonsterFromRace(ref Monster result, int race)
    {
        if (race <= 0)
        {
            return;
        }

        if (!_initialized)
        {
            // Re-enters the patched method with race 0, which returns above before doing any work.
            _defaultMonster = _faceGenAdapter.GetBaseMonsterFromRace(0);

            if (_defaultMonster == null)
            {
                _logger.LogError("EyeHeightAdjustmentHook: Failed to initialize _defaultMonster. FaceGen may not be ready. Will retry.");
                return;
            }

            _initialized = true;
        }

        var raceName = _raceManager.GetRaceNameFromId(race)?.ToLower();
        if (raceName != "dwarf" || result == null)
        {
            return;
        }

        try
        {
            var key = result.StringId ?? raceName;

            // Capture BEFORE any write, and only once, so the stored pair is always the engine's
            // own value and never something this hook already modified.
            if (!_vanillaHeights.TryGetValue(key, out var vanilla))
            {
                vanilla = new VanillaEyeHeights(result.StandingEyeHeight, result.CrouchEyeHeight);
                _vanillaHeights[key] = vanilla;
            }

            if (!_settings.EyeHeightEnabled)
            {
                Write(result, vanilla.Standing, vanilla.Crouch);
                return;
            }

            var adjuster = EyeHeightAdjustment.ClampAdjuster(_settings.DwarfEyeHeightAdjuster);

            if (!EyeHeightAdjustment.Resolve(_defaultMonster.StandingEyeHeight, adjuster, out var standing) ||
                !EyeHeightAdjustment.Resolve(_defaultMonster.CrouchEyeHeight, adjuster, out var crouch))
            {
                // Non-finite baseline or adjuster: leave whatever is there rather than writing a
                // value computed from garbage into a shared engine object.
                return;
            }

            Write(result, standing, crouch);
        }
        catch (Exception ex)
        {
            _logger.LogError($"EyeHeightAdjustmentHook: Failed to adjust eye height for dwarf: {ex.Message}");
        }
    }

    // Injected rather than reached through the static ReflectionHelper, whose type initialiser
    // resolves IReflectionService from the container. That static made this hook unreachable from
    // a test host (TypeInitializationException on first touch), which is the real reason its
    // capture/restore path went untested, and it is service location in a service besides.
    private void Write(Monster monster, float standing, float crouch)
    {
        _reflection.SetFieldValue(monster, StandingBackingField, standing);
        _reflection.SetFieldValue(monster, CrouchBackingField, crouch);
    }
}
