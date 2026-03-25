using System.Collections.Generic;

namespace TAOM.Features.Diplomacy.Models;

public class WarOfTheRingConfig
{
    public bool Enabled { get; set; } = true;
    public PhaseConfig Phase1 { get; set; } = new PhaseConfig();
    public PhaseConfig Phase2 { get; set; } = new PhaseConfig();
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
    public int Phase1Day { get; set; } = 2;
    public int Phase2Day { get; set; } = 5;
}
