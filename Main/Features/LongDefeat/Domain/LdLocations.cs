using System;
using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>What a buy at the armoury or the yard answers. <c>NotOffered</c> is a place or kind this build cannot deliver yet (<see cref="LocationOffer"/>); <c>Hidden</c> a row the fortress, the shop or the lord's class does not carry.</summary>
internal enum LocationResult
{
    Ok,
    NotOpen,
    NotOffered,
    Hidden,
    NotNear,
    NotOwnBody,
    NoGold,
    Owned,
    SiteTaken,
    SiteLost,
    NoSite,
    RunOver,
}

internal enum LdPlaceKind
{
    None,
    Armoury,
    Yard,
}

/// <summary>How the buyer came to a place: a person on his own feet, or the unattended bot's errand, which walks and teleports only when the walk ran out of time (D44). The report names it, so the dev bypass is never hidden.</summary>
internal enum ErrandReach
{
    Person,
    Walk,
    Teleport,
}

/// <summary>One defender deployment point of the scene as the field read it at boot: its number, where it stands and the engine kinds its weapons give (<c>mangonel</c>, <c>trebuchet</c>).</summary>
internal sealed record LdSite(int Index, LdPoint At, IReadOnlyList<string> Kinds);

/// <summary>An engine to deploy on the tick after its buy: the kind at the site.</summary>
internal sealed record EngineOrder(int Site, string Kind);

internal enum ErrandStep
{
    Start,
    Teleport,
    End,
}

/// <summary>What the director does with the bot's lord: walk him to a place, put him there when the walk ran out of time, or release his scripted walk. <c>At</c> is null for <see cref="ErrandStep.End"/>.</summary>
internal sealed record ErrandOrder(ErrandStep Step, LdPlaceKind Place, LdPlace? At);

internal enum ErrandResult
{
    Ok,
    NotOpen,
    NoPlace,
    Busy,
    Unreached,
    RunOver,
}

internal enum GearState
{
    ForSale,
    Owned,
}

/// <summary>One armoury row: the set, what the buy takes now (after the price factor) and whether the lord owns it.</summary>
internal sealed record GearRow(GearSet Set, int Price, GearState State);

internal enum SiteState
{
    Free,
    Taken,
    Lost,
}

/// <summary>One yard row: the site, its state, the kind that stands or stood there (empty while free) and the kinds it can be sold, each with the price the buy takes now.</summary>
internal sealed record SiteRow(LdSite Site, SiteState State, string Standing, IReadOnlyList<EngineOffer> Kinds);

/// <summary>
/// The two places as the HUD and the bot read them. <c>Near</c> is the place the lord stands at now (the armoury first), <c>None</c> elsewhere and for a place the run does not sell, so the prompt and the
/// panel group show only there (D37). The rows are listed wherever the lord stands, because the bot reads them to decide a walk. <c>Errand</c> is the place the bot's open errand goes to, <c>None</c> with
/// no errand open. <c>Unreached</c> lists the places an errand failed to reach since the last wave.
/// </summary>
internal sealed record LocationsView(LdPlaceKind Near, bool OwnBody, IReadOnlyList<GearRow> Gear, IReadOnlyList<SiteRow> Sites, LdPlaceKind Errand, IReadOnlyList<LdPlaceKind> Unreached)
{
    public static LocationsView None { get; } = new(LdPlaceKind.None, false, Array.Empty<GearRow>(), Array.Empty<SiteRow>(), LdPlaceKind.None, Array.Empty<LdPlaceKind>()) { LordStands = false };

    /// <summary>Whether the player has a body on the field at all, his own or a taken one: the bot walks no errand without one.</summary>
    public bool LordStands { get; init; } = true;
}
