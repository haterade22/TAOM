namespace TAOM.Features.FiefManagement.Models;

public sealed class FiefSummary
{
    public string Id { get; }
    public string Name { get; }
    public bool IsTown { get; }
    public bool IsCastle { get; }

    public FiefSummary(string id, string name, bool isTown, bool isCastle)
    {
        Id = id ?? string.Empty;
        Name = name ?? string.Empty;
        IsTown = isTown;
        IsCastle = isCastle;
    }
}
