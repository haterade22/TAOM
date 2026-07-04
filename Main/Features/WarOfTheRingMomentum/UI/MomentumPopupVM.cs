using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum.UI;

/// <summary>
/// Data source for MomentumView.xml (LOTRAOM parity source: MomentumVM). Property
/// names Good*/Evil* are pinned by the prefab bindings — the code sides are Free/Evil.
/// Computes once in the ctor AND live-recomputes on MomentumChanged while open
/// (LOTRAOM only ctor-computed). Leader banners: Gondor (empire_w) vs Mordor
/// (empire_s), falling back to the first enrolled kingdom of the side.
/// </summary>
public sealed class MomentumPopupVM : ViewModel
{
    private const string FreeLeaderKingdomId = "empire_w"; // Gondor
    private const string EvilLeaderKingdomId = "empire_s"; // Mordor

    private static readonly MomentumActionType[] ActionTypes =
        (MomentumActionType[])Enum.GetValues(typeof(MomentumActionType));

    private readonly IMomentumQueryService _query;
    private readonly Action _onClose;

    public MomentumPopupVM(IMomentumQueryService query, Action onClose)
    {
        _query = query;
        _onClose = onClose;

        GoodFactionLeader = new MBBindingList<FactionRelationshipVM>();
        EvilFactionLeader = new MBBindingList<FactionRelationshipVM>();
        GoodFactionParticipants = new MBBindingList<FactionRelationshipVM>();
        GoodFactionParticipants2 = new MBBindingList<FactionRelationshipVM>();
        EvilFactionParticipants = new MBBindingList<FactionRelationshipVM>();
        EvilFactionParticipants2 = new MBBindingList<FactionRelationshipVM>();
        MomentumBreakdown = new MBBindingList<BreakdownVM>();
        StatsBreakdown = new MBBindingList<BreakdownVM>();

        Rebuild();
        _query.MomentumChanged += Rebuild;
    }

    public override void OnFinalize()
    {
        _query.MomentumChanged -= Rebuild;
        base.OnFinalize();
    }

    public void ExecuteClose() => _onClose?.Invoke();

    // ---- static labels ----

    [DataSourceProperty] public string ScreenName => new TextObject("{=taom_wotr_title}War of the Ring").ToString();
    [DataSourceProperty] public string LeadersLabel => new TextObject("{=taom_wotr_leaders}Leaders").ToString();
    [DataSourceProperty] public string AlliesLabel => new TextObject("{=taom_wotr_allies}Allies").ToString();
    [DataSourceProperty] public string BreakdownLabel => new TextObject("{=taom_wotr_breakdown_header}Momentum Breakdown").ToString();
    [DataSourceProperty] public string StatsLabel => new TextObject("{=taom_wotr_total_stats_header}Total Stats").ToString();

    // ---- live-rebuilt state ----

    [DataSourceProperty] public string Momentum { get; private set; }
    [DataSourceProperty] public ImageIdentifierVM GoodAllianceVisual { get; private set; }
    [DataSourceProperty] public ImageIdentifierVM EvilAllianceVisual { get; private set; }
    [DataSourceProperty] public MBBindingList<FactionRelationshipVM> GoodFactionLeader { get; private set; }
    [DataSourceProperty] public MBBindingList<FactionRelationshipVM> EvilFactionLeader { get; private set; }
    [DataSourceProperty] public MBBindingList<FactionRelationshipVM> GoodFactionParticipants { get; private set; }
    [DataSourceProperty] public MBBindingList<FactionRelationshipVM> GoodFactionParticipants2 { get; private set; }
    [DataSourceProperty] public MBBindingList<FactionRelationshipVM> EvilFactionParticipants { get; private set; }
    [DataSourceProperty] public MBBindingList<FactionRelationshipVM> EvilFactionParticipants2 { get; private set; }
    [DataSourceProperty] public MBBindingList<BreakdownVM> MomentumBreakdown { get; private set; }
    [DataSourceProperty] public MBBindingList<BreakdownVM> StatsBreakdown { get; private set; }

    private void Rebuild()
    {
        var total = new TextObject("{=taom_wotr_total}Total: {MOMENTUM}");
        total.SetTextVariable("MOMENTUM", (int)_query.InternalMomentum);
        Momentum = total.ToString();
        OnPropertyChangedWithValue(Momentum, nameof(Momentum));

        RebuildLeadersAndBanners();
        RebuildParticipants();
        RebuildBreakdown();
        RebuildStats();
    }

    private void RebuildLeadersAndBanners()
    {
        var freeLeader = ResolveLeaderKingdom(MomentumSide.Free, FreeLeaderKingdomId);
        var evilLeader = ResolveLeaderKingdom(MomentumSide.Evil, EvilLeaderKingdomId);

        GoodFactionLeader.Clear();
        EvilFactionLeader.Clear();
        if (freeLeader != null)
            GoodFactionLeader.Add(new FactionRelationshipVM(freeLeader));
        if (evilLeader != null)
            EvilFactionLeader.Add(new FactionRelationshipVM(evilLeader));

        GoodAllianceVisual = new BannerImageIdentifierVM(freeLeader?.Banner, nineGrid: true);
        EvilAllianceVisual = new BannerImageIdentifierVM(evilLeader?.Banner, nineGrid: true);
        OnPropertyChangedWithValue(GoodAllianceVisual, nameof(GoodAllianceVisual));
        OnPropertyChangedWithValue(EvilAllianceVisual, nameof(EvilAllianceVisual));
    }

    private void RebuildParticipants()
    {
        FillSplit(GoodFactionParticipants, GoodFactionParticipants2, MomentumSide.Free);
        FillSplit(EvilFactionParticipants, EvilFactionParticipants2, MomentumSide.Evil);
    }

    private void FillSplit(MBBindingList<FactionRelationshipVM> row1,
        MBBindingList<FactionRelationshipVM> row2, MomentumSide side)
    {
        row1.Clear();
        row2.Clear();

        var kingdoms = _query.GetKingdomIds(side)
            .Select(ResolveKingdom)
            .Where(k => k != null)
            .ToList();

        // LOTRAOM parity: even indices → row 1, odd → row 2.
        for (int i = 0; i < kingdoms.Count; i++)
        {
            (i % 2 == 0 ? row1 : row2).Add(new FactionRelationshipVM(kingdoms[i]));
        }
    }

    private void RebuildBreakdown()
    {
        MomentumBreakdown.Clear();
        foreach (MomentumActionType type in ActionTypes)
        {
            MomentumBreakdown.Add(new BreakdownVM(
                ActionTypeLabel(type),
                _query.GetEvents(MomentumSide.Free, type),
                _query.GetEvents(MomentumSide.Evil, type)));
        }
    }

    private void RebuildStats()
    {
        var free = _query.GetTotalStats(MomentumSide.Free);
        var evil = _query.GetTotalStats(MomentumSide.Evil);

        StatsBreakdown.Clear();
        StatsBreakdown.Add(new BreakdownVM(
            new TextObject("{=taom_wotr_enemies_killed}Enemies Killed").ToString(),
            free.TotalKills.ToString(), evil.TotalKills.ToString()));
        StatsBreakdown.Add(new BreakdownVM(
            new TextObject("{=taom_wotr_settlements_captured}Settlements Captured").ToString(),
            free.TotalSettlementsCaptured.ToString(), evil.TotalSettlementsCaptured.ToString()));
        StatsBreakdown.Add(new BreakdownVM(
            new TextObject("{=taom_wotr_villages_raided}Villages Raided").ToString(),
            free.TotalVillagesRaided.ToString(), evil.TotalVillagesRaided.ToString()));
    }

    private static string ActionTypeLabel(MomentumActionType type)
    {
        switch (type)
        {
            case MomentumActionType.BattleWon:
                return new TextObject("{=taom_wotr_battles_won}Battles Won").ToString();
            case MomentumActionType.VillageRaided:
                return new TextObject("{=taom_wotr_villages_raided}Villages Raided").ToString();
            case MomentumActionType.Sieges:
                return new TextObject("{=taom_wotr_fiefs_captured}Fiefs Captured").ToString();
            case MomentumActionType.ArmyGathered:
                return new TextObject("{=taom_wotr_army_gathered}Armies Gathered").ToString();
            case MomentumActionType.RelativeStrength:
                return new TextObject("{=taom_wotr_relative_strength}Relative Strength").ToString();
            default:
                return "";
        }
    }

    private Kingdom ResolveLeaderKingdom(MomentumSide side, string preferredId)
    {
        var preferred = ResolveKingdom(preferredId);
        if (preferred != null)
            return preferred;

        return _query.GetKingdomIds(side).Select(ResolveKingdom).FirstOrDefault(k => k != null);
    }

    private static Kingdom ResolveKingdom(string kingdomId)
    {
        if (string.IsNullOrEmpty(kingdomId))
            return null;
        // Kingdom.All (== Campaign.Current.Kingdoms), the vanilla idiom. MBObjectManager
        // .GetObject<Kingdom>(id) does NOT resolve campaign kingdoms → returned null → the
        // Leaders/Allies banner lists came back empty (blank rows). ~30-kingdom scan, run
        // only on popup rebuild — not a hot path.
        return Kingdom.All?.FirstOrDefault(k => k?.StringId == kingdomId);
    }
}
