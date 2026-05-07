using System.Collections.Generic;

namespace TAOM.Features.StartupResources.Config;

public class StartupResourcesConfig
{
    public List<CultureResourceEntry> CultureEntries { get; set; } = new List<CultureResourceEntry>();
}

public class CultureResourceEntry
{
    public string CultureId { get; set; }
    public int Gold { get; set; }
    public float Influence { get; set; }
    public int PlayerGold { get; set; }
}
