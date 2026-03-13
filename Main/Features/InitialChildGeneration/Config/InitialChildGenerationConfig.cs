using System.Collections.Generic;

namespace TAOM.Features.InitialChildGeneration.Config;

public class InitialChildGenerationConfig
{
    public GlobalDefaults Defaults { get; set; } = new GlobalDefaults();
    public List<string> ExcludedCultures { get; set; } = new List<string>();
    public List<string> ExcludedClans { get; set; } = new List<string>();
    public List<CultureOverride> CultureOverrides { get; set; } = new List<CultureOverride>();
    public List<ClanOverride> ClanOverrides { get; set; } = new List<ClanOverride>();
}

public class GlobalDefaults
{
    public int MinAge { get; set; } = 2;
    public int MaxAge { get; set; } = 17;
    public double FemaleRatio { get; set; } = 0.49;
    public double ChildCountMultiplier { get; set; } = 1.0;
}

public class CultureOverride
{
    public string CultureId { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public double? FemaleRatio { get; set; }
    public double? ChildCountMultiplier { get; set; }
}

public class ClanOverride
{
    public string ClanId { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public double? FemaleRatio { get; set; }
    public double? ChildCountMultiplier { get; set; }
    public int? FixedChildCount { get; set; }
}
