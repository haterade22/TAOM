using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Features.HeroRace.Hooks;
using TAOM.Core.Logging;
using System;
using TaleWorlds.Core;

namespace TAOM.Features.HeroRace;

public class EyeHeightAdjustmentHook : IOnFaceGenGetBaseMonsterFromRace
{
    private const float DwarvenEyeHeightAdjustment = -0.2f;

    private readonly IRaceManager _raceManager;
    private readonly IFaceGenAdapter _faceGenAdapter;
    private readonly IModLogger _logger;
    private Monster _defaultMonster;
    private bool _initialized;

    public EyeHeightAdjustmentHook(IRaceManager raceManager, IFaceGenAdapter faceGenAdapter, IModLogger logger)
    {
        _raceManager = raceManager;
        _faceGenAdapter = faceGenAdapter;
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
            _defaultMonster = _faceGenAdapter.GetBaseMonsterFromRace(0);
            _initialized = true;

            if (_defaultMonster == null)
            {
                _logger.LogError("EyeHeightAdjustmentHook: Failed to initialize _defaultMonster. FaceGen may not be ready.");
                return;
            }
        }

        var raceName = _raceManager.GetRaceNameFromId(race)?.ToLower();
        if (raceName == "dwarf")
        {
            try
            {
                var newStandingHeight = _defaultMonster.StandingEyeHeight + DwarvenEyeHeightAdjustment;
                var newCrouchHeight = _defaultMonster.CrouchEyeHeight + DwarvenEyeHeightAdjustment;

                ReflectionHelper.SetFieldValue(result, "<StandingEyeHeight>k__BackingField", newStandingHeight);
                ReflectionHelper.SetFieldValue(result, "<CrouchEyeHeight>k__BackingField", newCrouchHeight);
            }
            catch (Exception ex)
            {
                _logger.LogError($"EyeHeightAdjustmentHook: Failed to adjust eye height for dwarf: {ex.Message}");
            }
        }
    }
}
