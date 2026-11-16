using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// A named hero the shop sells (#1325): one character, <c>Price</c> gold before the boons' price factor, and the factor his health is
/// scaled by on the field. <c>Name</c> and <c>Text</c> are raw <c>{=KEY}English</c> lines for the shop row.
/// </summary>
internal sealed record HeroOffer(string Id, string Name, string Text, string TroopId, int Price, float HealthFactor);

/// <summary>One hero for the field to spawn: the shop's id, which his fall is reported by, his character and his health factor.</summary>
internal sealed record HeroSpawn(string Id, string TroopId, float HealthFactor);

/// <summary>
/// The defenders' formation the heroes share, "The Three Hunters": index 7, which the Long Defeat uses for nothing else (foot 0,
/// archers 1, the mounted lord 2, the split groups 3 and 5, the tower posts 4), so the order bar shows it as group 8.
/// </summary>
internal static class HeroGroup
{
    public const int FormationIndex = 7;

    /// <summary>A standing hero within this ground distance of the heroes' place counts as near it on the hunters line.</summary>
    public const float NearMetres = 20f;

    /// <summary>And within this height of it: a hero at the foot of the wall is not at a place on top of it.</summary>
    public const float NearHeightMetres = 4f;

    /// <summary>The hunters line judges distance only when the field's own order has stood this long, so heroes on their way are not judged.</summary>
    public const float SettleSeconds = 60f;
}

/// <summary>
/// What the field did with one hero spawn. Refused: <c>Problem</c> says why. Spawned: what the engine made of him, read back from the
/// body: whether it treats him as a hero, whether he has a mount, his formation, his health limit as a multiple of the limit he
/// spawned with, the item in his hand, whether his formation is off AI control after the spawn (<c>Held</c>), how many soldiers in
/// it are not held heroes (<c>Strangers</c>, 0 is right) and the name of its movement order (<c>Order</c>). <c>CampaignCharacter</c>: the body is a
/// campaign <c>CharacterObject</c>, whose <c>IsHero</c> is true only with a Hero behind it; the mission heroes have none (#1366), so there <c>IsHero</c> is false.
/// </summary>
internal sealed record LdHeroReport(bool Spawned, string? Problem, bool IsHero = false, bool Mounted = false, int FormationIndex = -1, float HealthFactor = 0f, string Wielded = "",
    bool Held = false, int Strangers = 0, string Order = "", bool CampaignCharacter = false);

/// <summary>
/// One standing hero as the field reads him now (#1337): his kills so far (the engine's <c>KillCount</c>, which also counts knock-outs),
/// his formation index (-1 for none), his health as a share of his health limit, and his ground distance and height difference to the
/// heroes' place (both NaN when the field has no place for them). <c>PlayerControlled</c>: the player drives this hero's body (a take-over after the
/// player fell, possible in a campaign only, #1366). <c>Weapon</c>: the weapon class of the wielded weapon's current usage as text ("none" when
/// nothing is wielded), so a check line can show which mode a two-mode weapon is in (#1373).
/// </summary>
internal sealed record LdHeroFacts(string Id, int Kills, int FormationIndex, float HealthShare, float Distance, float Height, bool PlayerControlled = false, string Weapon = "none");

/// <summary>
/// What the field reads of the heroes at a wave clear (#1337): the standing heroes (empty when none stands), and the heroes' formation
/// itself. <c>GroupHeld</c>: the formation is off AI control. <c>GroupOrder</c>: the name of its movement order. <c>OwnOrder</c>: that
/// order is still the field's own Move to the heroes' place, so nobody else ordered the group. <c>GroupUnits</c>: how many soldiers the
/// formation holds, which is more than the heroes when the siege AI moved others in. <c>SettledSeconds</c>: how long ago the field gave
/// its own order.
/// </summary>
internal sealed record LdHeroesRead(IReadOnlyList<LdHeroFacts> Heroes, bool GroupHeld, string GroupOrder, bool OwnOrder, int GroupUnits, float SettledSeconds);

/// <summary>
/// What one hold of the heroes' group did (#1337): whether it took the formation back from the AI (<c>Regained</c>), how many soldiers
/// who are not held heroes it moved out, and how many held heroes it put back.
/// </summary>
internal readonly record struct LdHuntersHold(bool Regained, int MovedOut, int MovedBack)
{
    public static LdHuntersHold Nothing { get; } = new(false, 0, 0);

    public bool Acted => Regained || MovedOut > 0 || MovedBack > 0;
}

/// <summary>The director's running totals of the holds in one mission: how many regained the group, and the soldiers moved out and the heroes moved back.</summary>
internal readonly record struct LdHuntersTotals(int Retaken, int MovedOut, int MovedBack)
{
    public static LdHuntersTotals None { get; } = new(0, 0, 0);

    public LdHuntersTotals Add(LdHuntersHold hold) => new(Retaken + (hold.Regained ? 1 : 0), MovedOut + hold.MovedOut, MovedBack + hold.MovedBack);
}

/// <summary>Where a hero of the shop is: for sale, bought and not yet on the field, or standing on it.</summary>
internal enum HeroState
{
    ForSale,
    Pending,
    Standing,
}

/// <summary>One hero row as the HUD and the bot read it: the offer, the price to pay now (after the price factor) and his state.</summary>
internal sealed record HeroRow(HeroOffer Offer, int Price, HeroState State);

internal enum HeroResult
{
    Ok,
    NotOpen,
    UnknownHero,

    /// <summary>He is bought already: waiting for his spawn, or standing on the field. He is sold again only after he has fallen.</summary>
    Alive,
    NoGold,
    GarrisonFull,
    RunOver,
}
