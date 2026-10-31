namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>Gold flow: starting purse, passive and capture income, and what kills and towers pay.</summary>
internal sealed record FordsEconomyConfig(int StartingGold, int IncomePerMinute, float KillBountyFraction,
    int MinKillBounty, int TowerBounty, int HeroKillBounty, int CaptureIncomePerMinute, int MaxGold);
