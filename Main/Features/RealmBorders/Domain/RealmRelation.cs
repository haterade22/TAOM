namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// A realm's standing towards the player, with the values of vanilla's
/// <c>SettlementNameplateVM.RelationType</c> (0 neutral, 1 same faction, 2 enemy, 3 allied), which
/// <c>NameplateRelationPalette</c> follows too, so the war map and the settlement nameplates agree.
/// </summary>
public enum RealmRelation
{
    Neutral = 0,
    Own = 1,
    Enemy = 2,
    Ally = 3,
}

/// <summary>The war map mode's grouping keys: provinces merge by their realm's relation to the player.</summary>
public static class RelationGroups
{
    public const string Own = "relation:own";
    public const string Ally = "relation:ally";
    public const string Enemy = "relation:enemy";
    public const string Neutral = "relation:neutral";

    public static string Of(RealmRelation relation) => relation switch
    {
        RealmRelation.Own => Own,
        RealmRelation.Ally => Ally,
        RealmRelation.Enemy => Enemy,
        _ => Neutral,
    };

    /// <summary>A war front: the player's side, own or allied land, against an enemy.</summary>
    public static bool IsFront(string a, string b) =>
        (a == Enemy && (b == Own || b == Ally)) || (b == Enemy && (a == Own || a == Ally));
}
