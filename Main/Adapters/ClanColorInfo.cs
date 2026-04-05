namespace TAOM.Adapters;

public readonly struct ClanColorInfo
{
    public string ClanStringId { get; }
    public uint Color1 { get; }
    public uint Color2 { get; }
    public bool HasCustomColors => Color1 != 0 && Color2 != 0;

    public ClanColorInfo(string clanStringId, uint color1, uint color2)
    {
        ClanStringId = clanStringId;
        Color1 = color1;
        Color2 = color2;
    }
}
