namespace TAOM.Features.RealmBorders.Domain;

/// <summary>What the borders show. The map-mode key cycles them in this order.</summary>
public enum MapMode
{
    /// <summary>Every realm against every other: the default.</summary>
    Political = 0,

    /// <summary>The Free Peoples against the Shadow: one front line, realms of a side merged.</summary>
    Alignment = 1,

    /// <summary>
    /// Every realm by its relation to the player, in the settlement nameplates' colours (own, allied,
    /// enemy, neutral), with a front burning where the player's side meets an enemy.
    /// </summary>
    War = 2,
}

public static class MapModes
{
    public static MapMode Next(MapMode mode) => mode switch
    {
        MapMode.Political => MapMode.Alignment,
        MapMode.Alignment => MapMode.War,
        _ => MapMode.Political,
    };
}
