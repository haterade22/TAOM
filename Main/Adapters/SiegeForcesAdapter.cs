using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.SiegeForces.Domain;
using TAOM.Features.SiegeForces.Hooks;

namespace TAOM.Adapters;

/// <summary>
/// The engine side of the siege troop picker. Every method is total (null or false on any fault), and no campaign
/// static is read unguarded: Custom Battle has no campaign, and <c>MobileParty.MainParty</c> and <c>MapEvent.PlayerMapEvent</c>
/// throw there (adapters.md). Members referenced here are pinned by SiegeForcesBindingTests, because a member that
/// stops resolving fails when the method is first compiled, before any try inside it can run.
/// </summary>
public sealed class SiegeForcesAdapter : ISiegeForcesAdapter
{
    private const string Tag = "[SiegeForces]";

    private readonly IModLogger _logger;

    public SiegeForcesAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public SiegeForcesSnapshot? CaptureWallBattle()
    {
        try
        {
            var mainParty = Campaign.Current?.MainParty;
            var mapEvent = mainParty?.MapEvent;
            if (mainParty == null || mapEvent == null || !mapEvent.IsSiegeAssault)
                return null;

            // Vanilla's StartSiegeMission acts only on the walls; the lord's hall fight never reaches this hook.
            var settlement = PlayerSiege.BesiegedSettlement;
            if (settlement == null || settlement.CurrentSiegeState != Settlement.SiegeState.OnTheWalls)
                return null;

            var playerSide = mapEvent.PlayerSide;
            if (playerSide != BattleSideEnum.Attacker && playerSide != BattleSideEnum.Defender)
                return null;

            var attacker = playerSide == BattleSideEnum.Attacker;
            var army = mainParty.Army;
            var leadsArmy = army != null && army.LeaderParty == mainParty;
            var defendingOwnFief = !attacker && settlement.OwnerClan == Clan.PlayerClan;
            var mainPartyBase = mainParty.Party;

            var parties = new List<SiegeParty>();
            foreach (var eventParty in mapEvent.PartiesOnSide(playerSide))
            {
                var party = eventParty?.Party;
                if (party == null) continue;

                var mobile = party.MobileParty;
                parties.Add(new SiegeParty(
                    party.Id,
                    party == mainPartyBase,
                    army != null && mobile != null && mobile.Army == army,
                    mobile != null && mobile.IsGarrison,
                    ReadTroops(party.MemberRoster)));
            }

            return new SiegeForcesSnapshot(mapEvent, attacker, leadsArmy, defendingOwnFief, parties);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} could not read the wall battle ({ex.GetType().Name}: {ex.Message})");
            return null;
        }
    }

    public bool TryOpenPicker(PickerRequest request, Action<IReadOnlyDictionary<string, int>> onDone)
    {
        try
        {
            var context = Campaign.Current?.CurrentMenuContext;
            if (context?.Handler == null)
                return false;

            // The same two rosters and the same lambda vanilla's lord's hall builds (MenuHelper.EncounterAttackConsequence):
            // the screen's rows are one per character, every healthy troop selectable except the player.
            var full = TroopRoster.CreateDummyTroopRoster();
            var initial = TroopRoster.CreateDummyTroopRoster();
            foreach (var row in request.Rows)
            {
                if (!(row.Source is CharacterObject character))
                    return false;
                if (row.Number <= 0)
                    continue;

                // A hero's wounded state is the hero's own (TroopRosterElement.WoundedNumber reads HeroObject.IsWounded).
                full.AddToCounts(character, row.Number, woundedCount: character.IsHero ? 0 : row.Wounded);
                if (row.Initial > 0)
                    initial.AddToCounts(character, row.Initial);
            }

            context.OpenTroopSelection(full, initial, ch => !ch.IsPlayerCharacter, roster => onDone(ToSelection(roster)),
                request.Max, request.Min);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} could not open the troop selection ({ex.GetType().Name}: {ex.Message})");
            return false;
        }
    }

    public object? ReadMapEventToken()
    {
        try
        {
            return Campaign.Current?.MainParty?.MapEvent;
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"{Tag} no map event could be read ({ex.GetType().Name}: {ex.Message})");
            return null;
        }
    }

    public bool IsSpawnTotalsFitAttached()
    {
        try
        {
            var target = AccessTools.Method(typeof(DefaultBattleMissionAgentSpawnLogic), nameof(DefaultBattleMissionAgentSpawnLogic.InitWithSinglePhase));
            var info = target == null ? null : Harmony.GetPatchInfo(target);
            if (info == null)
                return false;

            foreach (var prefix in info.Prefixes)
            {
                if (prefix.PatchMethod?.DeclaringType == typeof(Patch102_SpawnTotalsFit))
                    return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} could not read the spawn logic's patches ({ex.GetType().Name}: {ex.Message})");
            return false;
        }
    }

    private static IReadOnlyList<SiegeTroop> ReadTroops(TroopRoster? roster)
    {
        var troops = new List<SiegeTroop>();
        if (roster == null)
            return troops;

        foreach (var element in roster.GetTroopRoster())
        {
            var character = element.Character;
            if (character == null) continue;

            troops.Add(new SiegeTroop(character, character.StringId, character.IsPlayerCharacter, character.Race,
                element.Number, element.WoundedNumber));
        }

        return troops;
    }

    private static IReadOnlyDictionary<string, int> ToSelection(TroopRoster? roster)
    {
        var selection = new Dictionary<string, int>(StringComparer.Ordinal);
        if (roster == null)
            return selection;

        foreach (var element in roster.GetTroopRoster())
        {
            if (element.Character != null && element.Number > 0)
                selection[element.Character.StringId] = element.Number;
        }

        return selection;
    }
}
