namespace TAOM.Features.FactionMap.Models;

public class RegionData
{
    public string FactionId { get; set; } = "";
    public float BBoxX { get; set; }
    public float BBoxY { get; set; }
    public float BBoxW { get; set; }
    public float BBoxH { get; set; }
    public float CapitalX { get; set; } = -1f;
    public float CapitalY { get; set; } = -1f;
    public bool HasCapitalPos => CapitalX >= 0f && CapitalY >= 0f;
}
