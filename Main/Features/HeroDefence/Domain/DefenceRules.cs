namespace TAOM.Features.HeroDefence.Domain;

/// <summary>The numbers of a run. These defaults are first guesses, and the config file replaces them.</summary>
internal sealed record DefenceRules
{
    public int StartingGold { get; init; } = 1000;
    public int StartingLives { get; init; } = 100;
    public float FirstPauseSeconds { get; init; } = 90f;
    public float PauseSeconds { get; init; } = 45f;
    public int MaxPlaced { get; init; } = 6;

    public float EarlyGoldPerSecond { get; init; } = 2f;
    public int EarlyGoldCap { get; init; } = 90;
    public int WaveBonusBase { get; init; } = 100;
    public int WaveBonusPerWave { get; init; } = 25;

    public float BountyBase { get; init; } = 2f;
    public float BountyPerLevel { get; init; } = 0.25f;
    public float BountyMountedPercent { get; init; } = 50f;
    public int CaptainBounty { get; init; } = 100;
    public int LieutenantBounty { get; init; } = 200;
    public int GeneralBounty { get; init; } = 350;

    public int SoldierLifeCost { get; init; } = 1;
    public int CaptainLifeCost { get; init; } = 5;
    public int LieutenantLifeCost { get; init; } = 10;
    public int GeneralLifeCost { get; init; } = 20;

    public float PackGapSeconds { get; init; } = 6f;
    public float ExitRadiusMetres { get; init; } = 8f;
    public float WaveLimitBaseSeconds { get; init; } = 180f;
    public float WaveLimitPerCreepSeconds { get; init; } = 3f;

    public float MinPostGapMetres { get; init; } = 3f;
    public float RoadEndClearMetres { get; init; } = 30f;
}
