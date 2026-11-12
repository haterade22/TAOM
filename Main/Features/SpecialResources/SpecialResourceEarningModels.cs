using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.SpecialResources;

/// <summary>
/// The data the earning service works from (#967 C1). The adapter reads the engine once and fills
/// these; nothing here holds a Hero, MapEvent or other sealed engine object (ADR-007).
/// </summary>
public sealed class EarnerRef
{
    public EarnerRef(
        string heroId, string kingdomId, string cultureId, bool isLocalMainHero,
        PlayerControl control, int? partyManCount = null)
    {
        HeroId = heroId;
        KingdomId = kingdomId;
        CultureId = cultureId;
        IsLocalMainHero = isLocalMainHero;
        Control = control;
        PartyManCount = partyManCount;
    }

    public string HeroId { get; }
    public string KingdomId { get; }
    public string CultureId { get; }

    /// <summary>
    /// The hero is <c>Hero.MainHero</c> right now. It only decides who a toast is for, never who earns:
    /// on the co-op server MainHero is temporarily a guest's inside the barter and crime scopes.
    /// </summary>
    public bool IsLocalMainHero { get; }

    public PlayerControl Control { get; }

    /// <summary>Men in the party that fought, or null when the hero has none. No production code reads it since the battle pay stopped dividing by it (2026-11-12); deleting it is a separate cleanup.</summary>
    public int? PartyManCount { get; }
}

/// <summary>A map event that ended. Candidates are filled only on the co-op authority during a live session.</summary>
public sealed class VictorySnapshot
{
    public string State { get; set; } = "";
    public bool IsPlayerMapEvent { get; set; }
    public BattleSideEnum PlayerSide { get; set; }
    public BattleSideEnum WinningSide { get; set; }
    public MapEvent.BattleTypes EventType { get; set; }
    public bool IsSiege { get; set; }
    public bool BeatAFieldParty { get; set; }
    public int EnemyCount { get; set; }
    public EarnerRef LocalHero { get; set; }
    public IReadOnlyList<EarnerRef> WinnerCandidates { get; set; } = Array.Empty<EarnerRef>();
}

/// <summary>A raid or a hideout clear. Raid reads <see cref="VillageLooted"/>, hideout reads <see cref="IsFinalized"/>.</summary>
public sealed class RaidOrHideoutSnapshot
{
    public BattleSideEnum Side { get; set; }
    public bool IsPlayerMapEvent { get; set; }
    public BattleSideEnum PlayerSide { get; set; }
    public bool VillageLooted { get; set; }
    public bool IsFinalized { get; set; }
    public EarnerRef LocalHero { get; set; }
    public IReadOnlyList<EarnerRef> AttackerLeaders { get; set; } = Array.Empty<EarnerRef>();
}

/// <summary>One hero's daily tick inputs.</summary>
public sealed class DailyTickInput
{
    public DailyTickInput(EarnerRef hero, int ownedTowns, IReadOnlyList<TroopUpkeepInfo> troopUpkeep)
    {
        Hero = hero;
        OwnedTowns = ownedTowns;
        TroopUpkeep = troopUpkeep;
    }

    public EarnerRef Hero { get; }
    public int OwnedTowns { get; }
    public IReadOnlyList<TroopUpkeepInfo> TroopUpkeep { get; }
}

/// <summary>What the behavior must show or apply after a daily tick. Texts are for the local player only.</summary>
public sealed class DailyOutcome
{
    /// <summary>A fresh instance each time, because the outcome is mutable.</summary>
    public static DailyOutcome NotProcessed => new();

    /// <summary>False when this process may not tick the hero; the desertion grace is then left unspent.</summary>
    public bool Processed { get; set; }

    /// <summary>The hero is this process's own player and may be shown messages (never true in a session).</summary>
    public bool IsLocal { get; set; }

    public string ResourceName { get; set; }
    public TextObject DailyLine { get; set; }
    public bool DailyLineYellow { get; set; }
    public TextObject Overdraft { get; set; }
    public string Warning { get; set; }
    public IReadOnlyList<TroopDesertionEntry> Desertions { get; set; } = Array.Empty<TroopDesertionEntry>();
    public bool GraceLogged { get; set; }
}

/// <summary>A positive earning, for the green toast. Shown only when <see cref="IsLocal"/>.</summary>
public sealed class EarningNotice
{
    public EarningNotice(string heroId, string resourceName, float earned, string source, bool isLocal)
    {
        HeroId = heroId;
        ResourceName = resourceName;
        Earned = earned;
        Source = source;
        IsLocal = isLocal;
    }

    public string HeroId { get; }
    public string ResourceName { get; }
    public float Earned { get; }
    public string Source { get; }
    public bool IsLocal { get; }
}

/// <summary>Who a recruit event may charge: the scope Patch113 named, and this process's own hero.</summary>
public sealed class RecruitContext
{
    public RecruitContext(string scopedPartyId, EarnerRef scopedLeader, EarnerRef local)
    {
        ScopedPartyId = scopedPartyId;
        ScopedLeader = scopedLeader;
        Local = local;
    }

    /// <summary>The party Patch113 named for this recruit, or null when no scope is open.</summary>
    public string ScopedPartyId { get; }

    /// <summary>The scoped party's leader, or null when the party or its leader is gone.</summary>
    public EarnerRef ScopedLeader { get; }

    public EarnerRef Local { get; }
}

/// <summary>One upgrade Coop's server applied: the troop the men became, and how many.</summary>
public sealed class CoopUpgradeLine
{
    public CoopUpgradeLine(string targetTroopId, int count)
    {
        TargetTroopId = targetTroopId;
        Count = count;
    }

    public string TargetTroopId { get; }
    public int Count { get; }
}

/// <summary>A recruit charge the local player is told about.</summary>
public sealed class RecruitChargeNotice
{
    public RecruitChargeNotice(string resourceName, float charged)
    {
        ResourceName = resourceName;
        Charged = charged;
    }

    public string ResourceName { get; }
    public float Charged { get; }
}
