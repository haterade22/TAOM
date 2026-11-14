using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CheatMenu;
using TAOM.Features.FieldCamp;

namespace TAOM.Adapters;

/// <summary>
/// The cheat menu's engine calls (<see cref="ICheatWorldAdapter"/>), all vanilla actions (v1.5.7): GiveGoldAction,
/// GainKingdomInfluenceAction, GainRenownAction, ChangeRelationAction and the main party's roster. Holds no engine object.
/// </summary>
public sealed class CheatWorldAdapter : ICheatWorldAdapter
{
    private readonly ICampService _camp;

    public CheatWorldAdapter(ICampService camp) => _camp = camp;

    public TeleportState ReadTeleportState()
    {
        var main = Campaign.Current?.MainParty;
        var hero = Hero.MainHero;
        if (main == null || hero == null)
            return new TeleportState(false, false, false, false, false, false, false, false, false, false);
        var army = main.Army;
        return new TeleportState(
            CampaignRunning: true,
            InMission: Mission.Current != null,
            InSettlement: main.CurrentSettlement != null,
            InMapEventOrEncounter: main.MapEvent != null || PlayerEncounter.Current != null,
            InSiege: main.SiegeEvent != null || main.BesiegerCamp != null,
            InArmyNotLeader: army != null && army.LeaderParty != main,
            IsPrisoner: hero.IsPrisoner,
            PartyActive: main.IsActive,
            AtSea: main.IsCurrentlyAtSea,
            InFieldCamp: _camp.PlayerCamp != null);
    }

    public (string? HeroId, string? KingdomId, string? CultureId) MainHeroIds()
    {
        var hero = Hero.MainHero;
        return hero == null ? (null, null, null) : (hero.StringId, hero.Clan?.Kingdom?.StringId, hero.Culture?.StringId);
    }

    public int? GiveGold(int amount)
    {
        var hero = Hero.MainHero;
        if (hero == null) return null;
        GiveGoldAction.ApplyBetweenCharacters(null, hero, amount);
        return hero.Gold;
    }

    public bool AddInfluence(int amount)
    {
        var hero = Hero.MainHero;
        if (hero?.Clan?.Kingdom == null) return false;
        GainKingdomInfluenceAction.ApplyForDefault(hero, amount);
        return true;
    }

    public bool AddRenown(int amount)
    {
        var hero = Hero.MainHero;
        if (hero?.Clan == null) return false;
        GainRenownAction.Apply(hero, amount);
        return true;
    }

    public string? AddCultureTroops(bool elite, int count)
    {
        var culture = Hero.MainHero?.Culture;
        var troop = elite ? culture?.EliteBasicTroop : culture?.BasicTroop;
        var roster = Campaign.Current?.MainParty?.MemberRoster;
        if (troop == null || roster == null) return null;
        roster.AddToCounts(troop, count);
        return troop.Name?.ToString();
    }

    public int HealMainParty()
    {
        var roster = Campaign.Current?.MainParty?.MemberRoster;
        if (roster == null) return 0;
        var healed = 0;
        foreach (var element in roster.GetTroopRoster().ToList())
        {
            var character = element.Character;
            if (character == null) continue;
            if (character.IsHero)
            {
                var hero = character.HeroObject;
                if (hero != null && hero.HitPoints < hero.MaxHitPoints) hero.HitPoints = hero.MaxHitPoints;
                continue;
            }
            if (element.WoundedNumber <= 0) continue;
            healed += element.WoundedNumber;
            roster.AddToCounts(character, 0, woundedCount: -element.WoundedNumber);
        }
        return healed;
    }

    public int? AddRelationWithKingdom(string kingdomId, int amount)
    {
        var kingdom = Kingdom.All.FirstOrDefault(k => k.StringId == kingdomId);
        if (kingdom == null) return null;
        var player = Hero.MainHero;
        var lords = kingdom.Clans
            .SelectMany(c => c.Heroes)
            .Where(h => h != null && h != player && h.IsLord && h.IsAlive)
            .ToList();
        foreach (var lord in lords)
            ChangeRelationAction.ApplyPlayerRelation(lord, amount, affectRelatives: false, showQuickNotification: false);
        return lords.Count;
    }

    public IReadOnlyList<(string Id, string Name)> Kingdoms() =>
        Kingdom.All.Where(k => !k.IsEliminated)
            .Select(k => (k.StringId, k.Name?.ToString() ?? k.StringId))
            .OrderBy(k => k.Item2)
            .ToList();

    public IReadOnlyList<(string Id, string Name)> Settlements(CheatSettlementKind kind) =>
        Settlement.All.Where(s => Is(s, kind))
            .Select(s => (s.StringId, s.Name?.ToString() ?? s.StringId))
            .OrderBy(s => s.Item2)
            .ToList();

    public string? SettlementName(string settlementId)
    {
        var s = Settlement.Find(settlementId);
        return s != null && (s.IsTown || s.IsCastle || s.IsVillage) ? s.Name?.ToString() ?? s.StringId : null;
    }

    public void TeleportToGate(string settlementId)
    {
        var target = Settlement.Find(settlementId);
        var main = Campaign.Current?.MainParty;
        if (target == null || main == null) return;
        var gate = target.GatePosition;

        main.Position = gate;
        if (main.Army != null && main.Army.LeaderParty == main)
            foreach (var attached in main.AttachedParties.ToList())
                attached.Position = gate;
        main.SetMoveModeHold();
        main.Party.SetVisualAsDirty();

        Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
        if (Game.Current?.GameStateManager?.ActiveState is MapState map && map.Handler != null)
        {
            map.Handler.ResetCamera(true, true);
            map.Handler.TeleportCameraToMainParty();
        }
    }

    private static bool Is(Settlement s, CheatSettlementKind kind) => kind switch
    {
        CheatSettlementKind.Town => s.IsTown,
        CheatSettlementKind.Castle => s.IsCastle,
        _ => s.IsVillage,
    };
}
