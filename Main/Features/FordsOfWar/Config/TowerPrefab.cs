namespace TAOM.Features.FordsOfWar;

/// <summary>The prefab a tower kind is spawned from, and its scale.</summary>
internal sealed record TowerPrefab(string Prefab, float Scale)
{
    public static TowerPrefab DefaultTower { get; } = new("siege_barricade_a", 1.0f);

    public static TowerPrefab DefaultKeep { get; } = new("siege_barricade_a", 1.6f);
}
