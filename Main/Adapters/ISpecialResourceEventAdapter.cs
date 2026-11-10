using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TAOM.Features.SpecialResources;

namespace TAOM.Adapters;

/// <summary>
/// Reads the engine for the special-resource earning paths and hands the service plain data (ADR-007).
/// Every snapshot method guards the whole read: a null return means "nothing to credit" and is logged
/// when the read failed.
/// </summary>
public interface ISpecialResourceEventAdapter
{
    /// <summary>The local <c>Hero.MainHero</c>, or null when there is none.</summary>
    EarnerRef DescribeLocalHero();

    /// <summary>A map event that ended. Null when it cannot pay anyone on this process.</summary>
    VictorySnapshot SnapshotVictory(MapEvent mapEvent);

    RaidOrHideoutSnapshot SnapshotRaid(BattleSideEnum side, RaidEventComponent component);

    RaidOrHideoutSnapshot SnapshotHideout(BattleSideEnum winnerSide, HideoutEventComponent component);

    /// <summary>
    /// The tournament winner as an earner. Without a session only the local MainHero qualifies; in a
    /// session any hero does. Null when there is no such hero.
    /// </summary>
    EarnerRef DescribeTournamentWinner(CharacterObject winner);

    int CountPrisoners(FlattenedTroopRoster roster);

    /// <summary>
    /// One hero's daily inputs. Null when the hero is not one this process ticks: without a session
    /// only the local MainHero, in a session only a living registered player, and nothing on a client.
    /// </summary>
    /// <param name="perPlayer">A co-op session is live: the caller decided the mode once for this hero.</param>
    DailyTickInput SnapshotDaily(Hero hero, bool perPlayer);

    /// <summary>Whose recruit this may be: the scope Patch113 named and the local MainHero. Never throws.</summary>
    RecruitContext SnapshotRecruit();

    /// <summary>The hero with this StringId as an earner, or null when there is none. Never throws.</summary>
    EarnerRef DescribeHero(string heroId);

    /// <summary>Removes the deserting troops from the hero's party and returns how many left.</summary>
    int ApplyDesertion(Hero hero, IReadOnlyList<TroopDesertionEntry> desertions);
}
