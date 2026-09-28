using System;
using TAOM.Adapters;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// A town's armoury level: its Barracks level (Mike's choice for KEYforce's "armoury or some building
/// in that settlement is tier X-Y-Z"; engine state, so no save data and AI construction raises it too)
/// plus a visiting master armourer's bonus, clamped to 0 to 3. The one place every reader (the market
/// gate, the daily sweep, the armoury and its ladder claim) asks.
/// </summary>
public sealed class ArmouryLevelService
{
    private readonly VisitingArmourerService _visits;
    private readonly IArmourAcquisitionSettingsProvider _settings;
    private readonly IArmouryTownAdapter _towns;

    public ArmouryLevelService(VisitingArmourerService visits, IArmourAcquisitionSettingsProvider settings, IArmouryTownAdapter towns)
    {
        _visits = visits;
        _settings = settings;
        _towns = towns;
    }

    public int GetTownLevel(string townId)
    {
        var level = Math.Max(0, Math.Min(_towns.GetBarracksLevel(townId), ArmourAcquisitionConfig.MaxArmouryLevel));
        if (_settings.VisitingArmourerEnabled)
            level += _visits.GetBonus(townId, _towns.Today);
        return Math.Min(level, ArmourAcquisitionConfig.MaxArmouryLevel);
    }
}
