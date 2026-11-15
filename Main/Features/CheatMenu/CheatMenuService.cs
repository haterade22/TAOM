using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.SpecialResources;

namespace TAOM.Features.CheatMenu;

/// <summary>What a cheat did, for the menu (CheatMenuTexts) and the console (CheatMenuCheats) to word.</summary>
public enum CheatOutcomeKind
{
    Done,
    CoopRefused,
    BadAmount,
    UnknownTarget,
    NothingToDo,
    TeleportRefused,

    /// <summary>A battle cheat outside a running battle (no mission, no player team, or the battle has ended or a side retreated).</summary>
    NoBattle,
}

/// <summary>Which cheat a Done result came from, so CheatMenuTexts words it for that cheat.</summary>
public enum CheatGiven
{
    Other,
    Gold,
    Influence,
    Renown,
    Heal,
    Relation,
    Teleport,
}

/// <param name="Name">What was given or reached (a troop, a resource, a kingdom, a settlement), when there is one.</param>
/// <param name="Number">The result's number: gold now, troops added, soldiers healed, lords changed, the resource's new balance.</param>
/// <param name="Given">Which cheat it was, so the player reads what it did and not only a name and a bare number.</param>
public sealed record CheatOutcome(CheatOutcomeKind Kind, string? Name = null, float Number = 0, TeleportRefusal Refusal = TeleportRefusal.None,
    CheatGiven Given = CheatGiven.Other)
{
    public bool Ok => Kind == CheatOutcomeKind.Done;
}

/// <summary>
/// The cheats behind both the menu buttons and the <c>taom.cheat_*</c> console commands (docs/features/cheat-menu.md). Every
/// action refuses in co-op (<see cref="CoopSessionPolicy.IsSinglePlayerAuthority"/>): TAOM has no message that carries a cheat
/// to the host, and a write on one peer desyncs the campaign. Amounts are checked here, before any engine call.
/// </summary>
public sealed class CheatMenuService
{
    public const int MaxGold = 1_000_000;
    public const int MaxInfluenceOrRenown = 10_000;
    public const int MaxTroops = 500;
    public const int MaxRelation = 100;
    public const float MaxResource = 10_000f;

    private readonly ICheatWorldAdapter _world;
    private readonly ISpecialResourceService _resources;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly IModLogger _logger;

    public CheatMenuService(ICheatWorldAdapter world, ISpecialResourceService resources, ICoopSessionProvider coop,
        IDedicatedServerProvider dedicated, IModLogger logger)
    {
        _world = world;
        _resources = resources;
        _coop = coop;
        _dedicated = dedicated;
        _logger = logger;
    }

    /// <summary>Read live on every call (ICoopSessionProvider's contract).</summary>
    public bool IsSinglePlayer => CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _dedicated);

    public CheatOutcome GiveGold(int amount)
    {
        if (!IsSinglePlayer) return Refused();
        if (amount < 1 || amount > MaxGold) return new CheatOutcome(CheatOutcomeKind.BadAmount, Number: MaxGold);
        var now = _world.GiveGold(amount);
        return now == null ? new CheatOutcome(CheatOutcomeKind.NothingToDo) : Log("gold", new CheatOutcome(CheatOutcomeKind.Done, Number: now.Value, Given: CheatGiven.Gold));
    }

    public CheatOutcome AddInfluence(int amount)
    {
        if (!IsSinglePlayer) return Refused();
        if (amount < 1 || amount > MaxInfluenceOrRenown) return new CheatOutcome(CheatOutcomeKind.BadAmount, Number: MaxInfluenceOrRenown);
        return _world.AddInfluence(amount)
            ? Log("influence", new CheatOutcome(CheatOutcomeKind.Done, Number: amount, Given: CheatGiven.Influence))
            : new CheatOutcome(CheatOutcomeKind.NothingToDo);
    }

    public CheatOutcome AddRenown(int amount)
    {
        if (!IsSinglePlayer) return Refused();
        if (amount < 1 || amount > MaxInfluenceOrRenown) return new CheatOutcome(CheatOutcomeKind.BadAmount, Number: MaxInfluenceOrRenown);
        return _world.AddRenown(amount)
            ? Log("renown", new CheatOutcome(CheatOutcomeKind.Done, Number: amount, Given: CheatGiven.Renown))
            : new CheatOutcome(CheatOutcomeKind.NothingToDo);
    }

    public CheatOutcome AddTroops(bool elite, int count)
    {
        if (!IsSinglePlayer) return Refused();
        if (count < 1 || count > MaxTroops) return new CheatOutcome(CheatOutcomeKind.BadAmount, Number: MaxTroops);
        var troop = _world.AddCultureTroops(elite, count);
        return troop == null
            ? new CheatOutcome(CheatOutcomeKind.NothingToDo)
            : Log("troops", new CheatOutcome(CheatOutcomeKind.Done, troop, count));
    }

    /// <summary>Every regular troop of the player's faction (CheatTroopList), sorted by tier then name; empty in co-op.</summary>
    public IReadOnlyList<CheatTroopFacts> FactionTroops()
    {
        if (!IsSinglePlayer) return Array.Empty<CheatTroopFacts>();
        var read = _world.ReadFactionTroops();
        return read == null ? Array.Empty<CheatTroopFacts>() : CheatTroopList.Select(read.Value.Sources, read.Value.Troops);
    }

    /// <summary>Adds <paramref name="count"/> of one troop of the faction's list; any other id is refused.</summary>
    public CheatOutcome AddFactionTroop(string? troopId, int count)
    {
        if (!IsSinglePlayer) return Refused();
        if (count < 1 || count > MaxTroops) return new CheatOutcome(CheatOutcomeKind.BadAmount, Number: MaxTroops);
        if (string.IsNullOrWhiteSpace(troopId) || !FactionTroops().Any(t => t.Id == troopId))
            return new CheatOutcome(CheatOutcomeKind.UnknownTarget, troopId);
        var name = _world.AddTroop(troopId!, count);
        return name == null
            ? new CheatOutcome(CheatOutcomeKind.UnknownTarget, troopId)
            : Log("troop", new CheatOutcome(CheatOutcomeKind.Done, name, count));
    }

    /// <summary>
    /// The game's own cheat party screen (every troop, 10 of each, move the ones you want). NothingToDo when it could not be
    /// opened.
    /// </summary>
    public CheatOutcome OpenCheatPartyScreen()
    {
        if (!IsSinglePlayer) return Refused();
        return _world.OpenCheatPartyScreen()
            ? Log("cheat party screen", new CheatOutcome(CheatOutcomeKind.Done))
            : new CheatOutcome(CheatOutcomeKind.NothingToDo);
    }

    public CheatOutcome Heal()
    {
        if (!IsSinglePlayer) return Refused();
        return Log("heal", new CheatOutcome(CheatOutcomeKind.Done, Number: _world.HealMainParty(), Given: CheatGiven.Heal));
    }

    public CheatOutcome AddRelation(string? kingdomId, int amount)
    {
        if (!IsSinglePlayer) return Refused();
        if (amount == 0 || amount < -MaxRelation || amount > MaxRelation) return new CheatOutcome(CheatOutcomeKind.BadAmount, Number: MaxRelation);
        if (string.IsNullOrWhiteSpace(kingdomId)) return new CheatOutcome(CheatOutcomeKind.UnknownTarget);
        var lords = _world.AddRelationWithKingdom(kingdomId!, amount);
        return lords == null
            ? new CheatOutcome(CheatOutcomeKind.UnknownTarget, kingdomId)
            : Log($"relation {kingdomId}", new CheatOutcome(CheatOutcomeKind.Done, KingdomName(kingdomId!), lords.Value, Given: CheatGiven.Relation));
    }

    /// <summary>
    /// The main hero's own resource (special resources are per hero and resolved by kingdom, then culture), through the
    /// service's capped grant: never above the resource's cap, never below 0.
    /// </summary>
    public CheatOutcome AddSpecialResource(float amount)
    {
        if (!IsSinglePlayer) return Refused();
        if (float.IsNaN(amount) || amount < 1f || amount > MaxResource) return new CheatOutcome(CheatOutcomeKind.BadAmount, Number: MaxResource);
        var (heroId, kingdomId, cultureId) = _world.MainHeroIds();
        if (heroId == null) return new CheatOutcome(CheatOutcomeKind.NothingToDo);
        var result = _resources.GrantAmount(heroId, kingdomId, cultureId, amount);
        return result.Resolved
            ? Log("special resource", new CheatOutcome(CheatOutcomeKind.Done, result.DisplayName, result.After))
            : new CheatOutcome(CheatOutcomeKind.NothingToDo);
    }

    public CheatOutcome Teleport(string? settlementId)
    {
        if (!IsSinglePlayer) return Refused();
        var refusal = CheatMenuPolicy.CheckTeleport(_world.ReadTeleportState());
        if (refusal != TeleportRefusal.None) return new CheatOutcome(CheatOutcomeKind.TeleportRefused, Refusal: refusal);
        var name = string.IsNullOrWhiteSpace(settlementId) ? null : _world.SettlementName(settlementId!);
        if (name == null) return new CheatOutcome(CheatOutcomeKind.UnknownTarget, settlementId);
        _world.TeleportToGate(settlementId!);
        return Log("teleport", new CheatOutcome(CheatOutcomeKind.Done, name, Given: CheatGiven.Teleport));
    }

    // The picker's display name; its id only for a kingdom the picker leaves out (an eliminated one). spkingdoms.xslt
    // keeps vanilla's ids (Gondor is empire_w), so the id alone means nothing to a player (#1317).
    private string KingdomName(string kingdomId) =>
        _world.Kingdoms().FirstOrDefault(k => k.Id == kingdomId).Name ?? kingdomId;

    private static CheatOutcome Refused() => new(CheatOutcomeKind.CoopRefused);

    private CheatOutcome Log(string what, CheatOutcome outcome)
    {
        _logger.LogInfo($"[CheatMenu] {what}: {outcome.Name ?? "-"} {outcome.Number:0.##}");
        return outcome;
    }
}
