using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

/// <summary>
/// The Eye of Sauron hunt's engine boundary (#765 M8; members verified against the v1.5.4 decompile: MobileParty.cs,
/// MobilePartyAi.cs, AiPartyThinkBehavior.cs, SetPartyAiAction.cs, NavigationHelper.cs, MobilePartyHelper.cs). Every
/// call looks its hero or party up by StringId in the live campaign, so a loaded save never meets an object from the
/// last one. It reads and acts; every decision is the service's. Main thread only: the campaign's daily,
/// hourly-party, frame and map-event callbacks.
/// </summary>
public sealed class HunterPartyAdapter : IHunterPartyAdapter
{
    private const int SpawnTries = 15;

    /// <summary>One percent of broad-phase headroom, so a hunter exactly on the band's edge is found.</summary>
    private const float LocatorSlack = 1.01f;

    public double NowDays => CampaignTime.Now.ToDays;

    public string? PlayerKingdomId => Clan.PlayerClan?.Kingdom?.StringId;

    public string? PlayerKingdomCultureId => Clan.PlayerClan?.Kingdom?.Culture?.StringId;

    public float PlayerStrength
    {
        get
        {
            var main = Campaign.Current?.MainParty;
            if (main?.Party == null) return float.NaN;
            var army = main.Army;
            return army != null && army.LeaderParty == main ? army.EstimatedStrength : main.Party.EstimatedStrength;
        }
    }

    public bool PlayerInAnotherLordsArmy => InAnotherLordsArmy(Campaign.Current?.MainParty);

    private static bool InAnotherLordsArmy(MobileParty? main) => main?.Army != null && main.Army.LeaderParty != main;

    private static Hero? FindHero(string id) => Campaign.Current?.CampaignObjectManager.Find<Hero>(id);

    // A linear walk over every party: daily, hourly and on a sight flip only, never every frame (ReadSight).
    private static MobileParty? FindParty(string id) => Campaign.Current?.CampaignObjectManager.Find<MobileParty>(id);

    public HunterHeroSnapshot ReadHero(string heroId)
    {
        var hero = FindHero(heroId);
        if (hero == null) return HunterHeroSnapshot.Missing;

        var party = hero.PartyBelongedTo;
        var faction = hero.MapFaction;
        var playerKingdom = Clan.PlayerClan?.Kingdom;
        var settlement = hero.CurrentSettlement;
        return new HunterHeroSnapshot
        {
            Exists = true,
            IsAlive = hero.IsAlive,
            IsActive = hero.IsActive,
            IsFugitive = hero.IsFugitive,
            IsPrisoner = hero.IsPrisoner,
            IsHeldByAParty = hero.PartyBelongedToAsPrisoner != null,
            IsMarkedForDeath = hero.DeathMark != KillCharacterAction.KillCharacterActionDetail.None,
            FactionId = faction?.StringId,
            // Null-safe, and handles eliminated factions, constant war and stances (FactionManager.cs:144-159).
            AtWarWithPlayerKingdom = playerKingdom != null && FactionManager.IsAtWarAgainstFaction(playerKingdom, faction),
            IsInAParty = party != null,
            LedPartyId = party != null && party.LeaderHero == hero ? party.StringId : null,
            CanLeadParty = hero.CanLeadParty(),
            IsLord = hero.CharacterObject?.Occupation == Occupation.Lord,
            SettlementBlocksSpawn = settlement?.Town != null && (settlement.Town.HasTournament || settlement.IsUnderSiege),
            GovernorOfSettlementId = hero.GovernorOf?.Settlement?.StringId,
        };
    }

    public HuntPartySnapshot ReadParty(string partyId)
    {
        var party = FindParty(partyId);
        if (party?.Party == null) return HuntPartySnapshot.Missing;

        var main = Campaign.Current?.MainParty;
        return new HuntPartySnapshot
        {
            Exists = true,
            IsActive = party.IsActive,
            LeaderHeroId = party.LeaderHero?.StringId,
            InMapEvent = party.MapEvent != null,
            // A leader has Army set; an attached member AttachedTo as well (Army.cs:845), as in ReadStepState.
            InArmy = party.Army != null || party.AttachedTo != null,
            // BesiegedSettlement dereferences BesiegerCamp.SiegeEvent unguarded (MobileParty.cs:1172).
            Besieging = party.BesiegerCamp != null,
            InSettlement = party.CurrentSettlement != null,
            IsDisbanding = party.IsDisbanding,
            AiPinned = party.Ai?.DoNotMakeNewDecisions ?? false,
            IsEngagingPlayer = IsEngaging(party, main),
            HoldsOffPlayer = HoldsOff(party),
            HeadCount = party.MemberRoster?.TotalManCount ?? 0,
            Strength = party.Party.EstimatedStrength,
        };
    }

    private static bool IsEngaging(MobileParty party, MobileParty? main) =>
        main != null && party.DefaultBehavior == AiBehavior.EngageParty && party.TargetParty == main;

    private static bool HoldsOff(MobileParty party) =>
        (party.Ai?.DoNotAttackMainPartyUntil.IsFuture ?? false)
        || (party.MapFaction?.NotAttackableByPlayerUntilTime.IsFuture ?? false);

    public bool PinAndEngagePlayer(string partyId)
    {
        var party = FindParty(partyId);
        var main = Campaign.Current?.MainParty;
        if (party?.Ai == null || !party.IsActive || main == null || !main.IsActive) return false;

        // TargetSettlement stays null: a pinned party with a target settlement keeps vanilla's initiative exception
        // (MobilePartyAi.cs:486). The engage call resets all movement state, so the caller re-issues it only when lost.
        SetPartyAiAction.GetActionForEngagingParty(party, main, MobileParty.NavigationType.Default, false);
        party.Ai.CheckPartyNeedsUpdate();
        party.Ai.SetDoNotMakeNewDecisions(true);
        return true;
    }

    public void ReleasePin(string partyId)
    {
        var party = FindParty(partyId);
        if (party?.Ai == null) return;

        party.Ai.SetDoNotMakeNewDecisions(false);
        // A pinned party returns before HourCounter++ (AiPartyThinkBehavior.cs:49-52, 213), and vanilla thinks only at
        // HourCounter % 6 == 0, so the hunt's engage order would stand for up to six hours. Rethink next hour (num 1).
        party.Ai.RethinkAtNextHourlyTick = true;
        if (!party.IsActive || party.MapEvent != null) return;

        // Drop the engage order on the player, as vanilla does at peace (FactionHelper.cs:346-350); the AI then
        // chooses afresh, and re-engages on its own if it still wants to.
        if (IsEngaging(party, Campaign.Current?.MainParty))
            party.SetMoveModeHold();
        party.Ai.CheckPartyNeedsUpdate();
    }

    public void InvalidateSpeed(string partyId) => FindParty(partyId)?.UpdateVersionNo();

    // MobileParty.UpdateVersionNo leaves MemberRoster.VersionNo, the size cache's key, alone; TroopRoster.UpdateVersion
    // bumps both (TroopRoster.cs:731-735).
    public void InvalidatePartySize(string partyId) => FindParty(partyId)?.MemberRoster?.UpdateVersion();

    public string? SpawnHunterParty(string heroId, float ringInner, float ringOuter, float minSights)
    {
        var hero = FindHero(heroId);
        var main = Campaign.Current?.MainParty;
        if (hero == null || main == null || !main.IsActive || hero.PartyBelongedTo != null) return null;

        if (!TryFindPointOutOfSight(main, ringInner, ringOuter, minSights, out var at)) return null;
        if (hero.GovernorOf != null)
            ChangeGovernorAction.RemoveGovernorOf(hero);

        // Re-checked in the same tick, right before the call: SpawnLordParty does no hero check of its own.
        if (hero.PartyBelongedTo != null) return null;
        return MobilePartyHelper.SpawnLordParty(hero, at, 1f)?.StringId;
    }

    /// <summary>A reachable point in the ring around the player, straight-line beyond <paramref name="minSights"/>.</summary>
    private static bool TryFindPointOutOfSight(MobileParty main, float ringInner, float ringOuter, float minSights,
        out CampaignVec2 point)
    {
        var center = main.Position;
        point = center;
        float sight = main.SeeingRange;
        if (!(sight > 0f) || float.IsInfinity(sight)) return false;

        for (int i = 0; i < SpawnTries; i++)
        {
            var candidate = NavigationHelper.FindReachablePointAroundPosition(center, MobileParty.NavigationType.Default,
                ringOuter * sight, ringInner * sight);
            // On failure the engine returns the centre after its own 250 samples (NavigationHelper.cs:193-213); a retry
            // would repeat them, so tomorrow's daily tick is the retry.
            if (candidate == center) return false;
            // Near a map border the engine shrinks its clamped box (198-201), so a point can land inside the margin.
            if (!candidate.IsValid() || candidate.Distance(center) < minSights * sight) continue;
            point = candidate;
            return true;
        }
        return false;
    }

    public float GetTroopPower(string troopId, string leaderHeroId)
    {
        var troop = MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
        var hero = FindHero(leaderHeroId);
        var model = Campaign.Current?.Models?.MilitaryPowerModel;
        if (troop == null || hero == null || model == null) return float.NaN;
        return model.GetTroopPower(troop, BattleSideEnum.Defender, MapEvent.PowerCalculationContext.Estimated, hero.PowerModifier);
    }

    public int AddTroops(string partyId, string troopId, int count)
    {
        var party = FindParty(partyId);
        var troop = MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
        if (party?.MemberRoster == null || troop == null || count <= 0) return 0;
        party.MemberRoster.AddToCounts(troop, count);
        return count;
    }

    public void TopUpFood(string partyId, int targetFood)
    {
        var party = FindParty(partyId);
        var grain = DefaultItems.Grain;
        if (party?.ItemRoster == null || grain == null) return;
        int missing = targetFood - party.ItemRoster.TotalFood;
        if (missing > 0)
            party.ItemRoster.AddToCounts(grain, missing);
    }

    public HuntSightReading ReadSight(string partyId, float searchSights)
    {
        var main = Campaign.Current?.MainParty;
        if (main?.Party == null) return HuntSightReading.Missing;

        // Read once: it sets the search radius and is the reading's S. An infinite radius would walk every locator cell.
        float sight = main.SeeingRange;
        float radius = LocatorRadius(searchSights, sight);
        if (!(sight > 0f) || !(radius > 0f) || float.IsInfinity(radius)) return HuntSightReading.Missing;

        // The locator grid around the player, the search vanilla's own visibility pass runs; no allocation. A hunter
        // beyond the radius, or gone, reads as Missing: both are outside the floor's band.
        MobileParty? party = null;
        var search = MobileParty.StartFindingLocatablesAroundPosition(main.Position.ToVec2(), radius);
        for (var p = MobileParty.FindNextLocatable(ref search); p != null; p = MobileParty.FindNextLocatable(ref search))
        {
            if (p.StringId == partyId)
            {
                party = p;
                break;
            }
        }
        if (party?.Party == null) return HuntSightReading.Missing;

        return new HuntSightReading
        {
            Valid = true,
            HunterActive = party.IsActive,
            PlayerActive = main.IsActive,
            HunterInMapEvent = party.MapEvent != null,
            PlayerInMapEvent = main.MapEvent != null,
            PlayerAtSea = main.IsCurrentlyAtSea,
            SeeingRange = sight,
            Distance = party.Position.Distance(main.Position),
            // Speed asserts on an inactive party (MobileParty.cs:419-431).
            PlayerSpeed = main.IsActive ? main.Speed : 0f,
            HunterHoldsOffPlayer = HoldsOff(party),
        };
    }

    /// <summary>
    /// The locator search's radius: a little beyond <paramref name="searchSights"/> sights, because the locator skips a
    /// party at <c>d^2 &gt;= r^2</c> (LocatorGrid.cs:131, 139) and the service's band includes its edge. The service
    /// keeps the exact filter on the reading's distance.
    /// </summary>
    internal static float LocatorRadius(float searchSights, float sight) => searchSights * sight * LocatorSlack;

    public HuntStepState ReadStepState(string partyId)
    {
        var party = FindParty(partyId);
        var main = Campaign.Current?.MainParty;
        if (party?.Party == null || main?.Party == null) return HuntStepState.Missing;

        var hunterAt = party.Position.ToVec2();
        var playerAt = main.Position.ToVec2();
        return new HuntStepState
        {
            Valid = true,
            HunterActive = party.IsActive,
            HunterInMapEvent = party.MapEvent != null,
            HunterBesieging = party.BesiegerCamp != null,
            HunterInArmy = party.Army != null || party.AttachedTo != null,
            HunterInSettlement = party.CurrentSettlement != null,
            HunterAtSea = party.IsCurrentlyAtSea || party.IsTransitionInProgress,
            HunterVisible = party.IsVisible,
            HunterEngagingPlayer = IsEngaging(party, main),
            // IsActive is the enlistment guard: MobilePartyAttachmentAdapter hides an enlisted player that way.
            PlayerActive = main.IsActive,
            PlayerInMapEventOrSiege = main.MapEvent != null || main.BesiegerCamp != null,
            PlayerInSettlement = main.CurrentSettlement != null,
            PlayerAtSea = main.IsCurrentlyAtSea,
            PlayerInEncounter = PlayerEncounter.Current != null,
            TrueSight = Campaign.Current?.TrueSight == true,
            // CurrentNavigationFace is Position.Face (MobileParty.cs:1018), a plain read.
            SameIsland = party.CurrentNavigationFace.FaceIslandIndex == main.CurrentNavigationFace.FaceIslandIndex,
            SeeingRange = main.SeeingRange,
            HunterX = hunterAt.x,
            HunterY = hunterAt.y,
            PlayerX = playerAt.x,
            PlayerY = playerAt.y,
        };
    }

    public HuntPath TryRebuildPath(string partyId)
    {
        var party = FindParty(partyId);
        if (party == null || !party.RecalculateLongTermPath()) return HuntPath.Empty;

        var path = party.Path;
        int n = path?.Size ?? 0;
        if (n <= 0) return HuntPath.Empty;
        var xs = new float[n];
        var ys = new float[n];
        for (int i = 0; i < n; i++)
        {
            xs[i] = path!.PathPoints[i].x;
            ys[i] = path.PathPoints[i].y;
        }
        return new HuntPath(xs, ys);
    }

    public HuntLanding ResolveLanding(string partyId, float x, float y, float enemyClearance)
    {
        var party = FindParty(partyId);
        var main = Campaign.Current?.MainParty;
        if (party == null || main == null) return default;

        int island = main.CurrentNavigationFace.FaceIslandIndex;
        var point = new CampaignVec2(new Vec2(x, y), true);
        if (!OnIsland(point, island))
        {
            // On failure this returns the centre unchanged (NavigationHelper.cs:193-219).
            var nudged = NavigationHelper.FindReachablePointAroundPosition(point, MobileParty.NavigationType.Default, 2f);
            if (nudged == point || !OnIsland(nudged, island)) return default;
            point = nudged;
        }

        var hunterFaction = party.MapFaction;
        var search = MobileParty.StartFindingLocatablesAroundPosition(point.ToVec2(), enemyClearance);
        for (var other = MobileParty.FindNextLocatable(ref search); other != null; other = MobileParty.FindNextLocatable(ref search))
        {
            if (other != party && other.IsActive && hunterFaction != null && other.MapFaction != null
                && other.MapFaction.IsAtWarWith(hunterFaction))
                return default;
        }

        var at = point.ToVec2();
        return new HuntLanding(true, at.x, at.y);
    }

    private static bool OnIsland(CampaignVec2 point, int island) => point.IsValid() && point.Face.FaceIslandIndex == island;

    public void Relocate(string partyId, float x, float y)
    {
        var party = FindParty(partyId);
        if (party == null) return;

        // The setter only moves the locator (MobileParty.cs:1064-1078); the navigation face follows the position. The
        // service never steps a hunter in an army or with parties attached (HunterInArmy).
        party.Position = new CampaignVec2(new Vec2(x, y), true);
        // Without the rebuild a hunter chasing a still player walks back to its old waypoint (DoAiPathMode,
        // MobileParty.cs:3758-3796).
        party.RecalculateLongTermPath();
        party.Ai.CheckPartyNeedsUpdate();
    }

    public void Announce(HuntAnnouncement announcement, string heroId)
    {
        var text = announcement switch
        {
            HuntAnnouncement.StartFree => new TextObject("{=taom_eye_hunt_free}The Eye of Sauron has turned upon you. {HUNTER} rides out with a host to destroy you."),
            HuntAnnouncement.StartEvil => new TextObject("{=taom_eye_hunt_evil}Elrond has sent {HUNTER} with a host of the Eldar to bring you down."),
            HuntAnnouncement.Won => new TextObject("{=taom_eye_hunt_won}{HUNTER} is beaten. The hunt is over, for now."),
            _ => new TextObject("{=taom_eye_hunt_over}The hunt is over, for now."),
        };
        var name = FindHero(heroId)?.Name ?? new TextObject(heroId);
        text.SetTextVariable("HUNTER", name);
        string message = text.ToString();

        // Always a chat line; a one-button popup when the map is free (MapPopupGate), otherwise the banner.
        // Never a scene notification: it NREs for custom races.
        InformationManager.DisplayMessage(new InformationMessage(message));
        if (MapPopupGate.IsMapFree())
        {
            InformationManager.ShowInquiry(new InquiryData(name.ToString(), message, true, false,
                GameTexts.FindText("str_ok").ToString(), string.Empty, null, null), pauseGameActiveState: true);
        }
        else
        {
            MBInformationManager.AddQuickInformation(text, extraTimeInMs: 3000);
        }
    }

    public string HeroName(string heroId) => FindHero(heroId)?.Name?.ToString() ?? heroId;
}
