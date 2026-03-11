using TaleWorlds.Library;
using TAOM.Features.FactionMap.Models;

namespace TAOM.Features.FactionMap.ViewModels;

public class LandmarkItemVM : ViewModel
{
    private readonly LandmarkDef _def;

    public LandmarkItemVM(LandmarkDef def)
    {
        _def = def;
    }

    [DataSourceProperty] public string Id => _def.Id;
    [DataSourceProperty] public string Name => _def.Name;
    [DataSourceProperty] public string Description => _def.Description;
    [DataSourceProperty] public string TypeName => _def.Type.ToString();
    [DataSourceProperty] public bool IsCapital => _def.Type == LandmarkType.Capital;
    [DataSourceProperty] public bool IsCity => _def.Type == LandmarkType.City;
    [DataSourceProperty] public bool IsFortress => _def.Type == LandmarkType.Fortress;
    [DataSourceProperty] public bool IsRuin => _def.Type == LandmarkType.Ruin;
    [DataSourceProperty] public bool IsPort => _def.Type == LandmarkType.Port;
    [DataSourceProperty] public int PosX => _def.X;
    [DataSourceProperty] public int PosY => _def.Y;
    [DataSourceProperty] public float NormalizedX => _def.X / 2048f;
    [DataSourceProperty] public float NormalizedY => _def.Y / 1423f;
    [DataSourceProperty] public int FactionId => _def.FactionId;

    [DataSourceProperty]
    public string TypeIcon => _def.Type switch
    {
        LandmarkType.Capital => "marker_capital",
        LandmarkType.City => "marker_city",
        LandmarkType.Fortress => "marker_fortress",
        LandmarkType.Ruin => "marker_ruin",
        LandmarkType.Port => "marker_port",
        LandmarkType.Landmark => "marker_landmark",
        _ => "marker_default",
    };
}
