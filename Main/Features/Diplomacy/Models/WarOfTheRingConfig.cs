using System.Collections.Generic;

namespace TAOM.Features.Diplomacy.Models;

public class WarOfTheRingConfig
{
    public bool Enabled { get; set; } = true;
    public PhaseConfig Phase1 { get; set; } = new PhaseConfig();
    // Phase 2 must default LATER than Phase 1 — PhaseConfig's own default (30) is Phase 1's day, and
    // WarOfTheRingConfigProvider.ValidateConfig only reverts Phase2 when it is strictly < Phase1, so
    // leaving both at 30 would fire IsengardWar and FullWar on the same tick if the JSON is missing.
    public PhaseConfig Phase2 { get; set; } = new PhaseConfig { TriggerDay = 44 };
    public TestModeConfig TestMode { get; set; } = new TestModeConfig();
}

public class PhaseConfig
{
    public int TriggerDay { get; set; } = 30;
    public bool AutoWarBetweenHostileTiers { get; set; }
    public bool BlockPeaceBetweenHostileTiers { get; set; }
    public List<WarDeclaration> Wars { get; set; } = new List<WarDeclaration>();
}

public class WarDeclaration
{
    public string Attacker { get; set; } = "";
    public string Defender { get; set; } = "";
}

public class TestModeConfig
{
    public bool Enabled { get; set; }
    public int Phase1Day { get; set; } = 1;
    public int Phase2Day { get; set; } = 3;
}
