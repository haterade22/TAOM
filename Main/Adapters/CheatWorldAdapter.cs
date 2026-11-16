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
using TAOM.Features.Arachnophobia;
using TAOM.Features.CheatMenu;
using TAOM.Features.EliteEmissary;
using TAOM.Features.FieldCamp;
using TAOM.Features.TroopProgression;

namespace TAOM.Adapters;

/// <summary>
/// The cheat menu's engine calls (<see cref="ICheatWorldAdapter"/>), all vanilla actions (v1.5.7): GiveGoldAction,
/// GainKingdomInfluenceAction, GainRenownAction, ChangeRelationAction and the main party's roster. Holds no engine object.
/// </summary>
public sealed class CheatWorldAdapter : ICheatWorldAdapter
{
    private readonly ICampService _camp;
    private readonly IVolunteerRecruitmentService _recruitment;
    private readonly IEliteEmissaryConfigProvider _emissary;
    private readonly IArachnophobiaService _arachnophobia;

    public CheatWorldAdapter(ICampService camp, IVolunteerRecruitmentService recruitment, IEliteEmissaryConfigProvider emissary,
        IArachnophobiaService arachnophobia)
    {
        _camp = camp;
        _recruitment = recruitment;
        _emissary = emissary;
        _arachnophobia = arachnophobia;
    }

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

    public (CheatTroopSources Sources, IReadOnlyDictionary<string, CheatTroopFacts> Troops)? ReadFactionTroops()
    {
        var clan = Hero.MainHero?.Clan;
        var culture = clan?.Kingdom?.Culture ?? clan?.Culture;
        if (culture == null) return null;

        var own = new List<string>();
        void Own(CharacterObject? c)
        {
            if (c != null) own.Add(c.StringId);
        }
        Own(culture.BasicTroop);
        Own(culture.EliteBasicTroop);
        Own(culture.MeleeMilitiaTroop);
        Own(culture.RangedMilitiaTroop);
        Own(culture.MeleeEliteMilitiaTroop);
        Own(culture.RangedEliteMilitiaTroop);
        own.AddRange(TemplateTroops(culture.VassalRewardTroopsPartyTemplate));
        if (culture.BasicMercenaryTroops != null)
            foreach (var merc in culture.BasicMercenaryTroops) Own(merc);
        own.AddRange(_recruitment.GetCulturePoolTroopIds(culture.StringId));
        if (_emissary.GetConfig().CultureOffers.TryGetValue(culture.StringId, out var offers))
            own.AddRange(offers);

        var fielded = new HashSet<string>(VolunteerRecruitmentService.AllPooledTroopIds());
        foreach (var c in Clan.All)
            if (c != null && !c.IsBanditFaction)
                fielded.UnionWith(TemplateTroops(c.DefaultPartyTemplate));

        var troops = new Dictionary<string, CheatTroopFacts>(System.StringComparer.Ordinal);
        foreach (var c in CharacterObject.All)
        {
            if (c?.StringId == null || troops.ContainsKey(c.StringId) || _arachnophobia.IsHidden(c.StringId)) continue;
            troops[c.StringId] = new CheatTroopFacts(
                c.StringId,
                c.Name?.ToString() ?? c.StringId,
                c.IsHero ? 0 : c.Tier,
                c.Culture?.StringId,
                c.IsHero,
                c.IsTemplate,
                c.IsObsolete,
                c.Occupation == Occupation.Soldier || c.Occupation == Occupation.Mercenary,
                c.UpgradeTargets?.Where(u => u != null).Select(u => u.StringId).ToList() ?? new List<string>());
        }
        return (new CheatTroopSources(culture.StringId, own, fielded), troops);
    }

    // v1.5.7 PartyScreenHelper: private static TroopRoster GetRosterWithAllGameTroops(), the list OpenScreenAsCheat shows (every
    // valid encyclopedia troop, 10 of each, sorted by name). Null if a game update renames it; the screen then does not open.
    private static readonly System.Reflection.MethodInfo? VanillaCheatRoster =
        HarmonyLib.AccessTools.Method(typeof(Helpers.PartyScreenHelper), "GetRosterWithAllGameTroops");

    public bool OpenCheatPartyScreen()
    {
        var game = Game.Current;
        var main = PartyBase.MainParty;
        if (game == null || main == null || Campaign.Current == null) return false;

        // With the engine's cheat mode on, vanilla's own call does it all.
        if (game.CheatMode)
        {
            Helpers.PartyScreenHelper.OpenScreenAsCheat();
            return true;
        }

        // Otherwise the same screen as OpenScreenAsCheat (v1.5.7), field for field, minus its cheat-mode check: vanilla's
        // list on the left, the main party on the right, dismiss mode on. Its done handler, the private
        // PartyScreenHelper.DefaultDoneHandler, releases the prisoners moved to the left, then returns true; the handler
        // below does the same. Without the release a lord moved left stays a prisoner with no captor for good. Vanilla's
        // taken-prisoner branch cannot run here: the left prisoner roster starts empty.
        if (VanillaCheatRoster?.Invoke(null, null) is not TaleWorlds.CampaignSystem.Roster.TroopRoster left) return false;
        var logic = new PartyScreenLogic();
        logic.Initialize(new PartyScreenLogicInitializationData
        {
            LeftOwnerParty = null,
            RightOwnerParty = main,
            LeftMemberRoster = left,
            LeftPrisonerRoster = TaleWorlds.CampaignSystem.Roster.TroopRoster.CreateDummyTroopRoster(),
            RightMemberRoster = main.MemberRoster,
            RightPrisonerRoster = main.PrisonRoster,
            LeftLeaderHero = null,
            RightLeaderHero = main.LeaderHero,
            LeftPartyMembersSizeLimit = 0,
            LeftPartyPrisonersSizeLimit = 0,
            RightPartyMembersSizeLimit = main.PartySizeLimit,
            RightPartyPrisonersSizeLimit = main.PrisonerSizeLimit,
            LeftPartyName = null,
            RightPartyName = main.Name,
            TroopTransferableDelegate = Helpers.PartyScreenHelper.TroopTransferableDelegate,
            PartyPresentationDoneButtonDelegate = (_, _, _, _, _, released, _, _, _) =>
            {
                if (!released.IsEmpty()) EndCaptivityAction.ApplyByReleasedByChoice(released);
                return true;
            },
            PartyPresentationDoneButtonConditionDelegate = null,
            PartyPresentationCancelButtonActivateDelegate = null,
            PartyPresentationCancelButtonDelegate = null,
            IsDismissMode = true,
            IsTroopUpgradesDisabled = false,
            Header = null,
            PartyScreenClosedDelegate = null,
            TransferHealthiesGetWoundedsFirst = false,
            ShowProgressBar = false,
            MemberTransferState = PartyScreenLogic.TransferState.Transferable,
            PrisonerTransferState = PartyScreenLogic.TransferState.Transferable,
            AccompanyingTransferState = PartyScreenLogic.TransferState.NotTransferable,
        });
        var state = game.GameStateManager.CreateState<PartyState>();
        state.PartyScreenLogic = logic;
        state.IsDonating = false;
        game.GameStateManager.PushState(state);
        return true;
    }

    public string? AddTroop(string troopId, int count)
    {
        var troop = CharacterObject.Find(troopId);
        var roster = Campaign.Current?.MainParty?.MemberRoster;
        if (troop == null || troop.IsHero || roster == null) return null;
        roster.AddToCounts(troop, count);
        return troop.Name?.ToString() ?? troop.StringId;
    }

    private static IEnumerable<string> TemplateTroops(PartyTemplateObject? template) =>
        template?.Stacks == null
            ? Enumerable.Empty<string>()
            : template.Stacks.Where(s => s.Character != null).Select(s => s.Character.StringId).ToList();

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
