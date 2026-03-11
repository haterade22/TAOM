namespace TAOM.Features.FactionMap.Models;

public enum LandmarkType
{
    Capital,
    City,
    Fortress,
    Landmark,
    Ruin,
    Port,
}

public class LandmarkDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public LandmarkType Type { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int FactionId { get; set; }
}
