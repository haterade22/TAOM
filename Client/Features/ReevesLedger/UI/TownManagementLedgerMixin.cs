using System;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.ReevesLedger.UI;

/// <summary>
/// The Reeve's Ledger on the town management window (docs/features/reeves-ledger.md): the fief's name as the title, a Profit
/// row and a Patrol row (towns, and castles with a patrol) appended to the stat lists, and tooltips on the stash and prison icons the prefab inserts.
/// Hooked on RefreshTownManagementStats, which clears and refills both lists, so the rows are appended after every refill: at
/// construction (UIExtenderEx replays the constructor's refresh once the mixin exists), on a queue change, a governor change
/// and a reserve confirm. The title is set there too, after RefreshValues has written "Manage" in the constructor.
/// The mixin captures <see cref="Settlement.CurrentSettlement"/> itself: the view model reads the same value in its
/// constructor, and the remote F6 window swaps it in before building the view model.
/// </summary>
[ViewModelMixin("RefreshTownManagementStats")]
internal class TownManagementLedgerMixin : BaseViewModelMixin<TownManagementVM>
{
    private readonly ReevesLedgerService? _service;
    private readonly IFiefLedgerAdapter? _fief;
    private readonly ScreenFaultLog? _faults;
    private readonly Settlement? _settlement;
    private readonly bool _enabled;
    private TownManagementDescriptionItemVM? _profitRow;
    private TownManagementDescriptionItemVM? _patrolRow;
    private bool _showLedgerIcons;
    private readonly BasicTooltipViewModel _profitTooltip = new();
    private readonly BasicTooltipViewModel _patrolTooltip = new();

    public TownManagementLedgerMixin(TownManagementVM viewModel) : base(viewModel)
    {
        try
        {
            // First, so a fault in anything below reaches the log.
            _faults = new ScreenFaultLog(IoC.Resolve<IModLogger>(), "ReevesLedger");
            _service = IoC.Resolve<ReevesLedgerService>();
            _fief = IoC.Resolve<IFiefLedgerAdapter>();
            _settlement = Settlement.CurrentSettlement;
            _enabled = _settlement != null && IoC.Resolve<ReevesLedgerSettingsProvider>().LedgerEnabled;
        }
        catch (Exception ex)
        {
            // The constructor runs inside the view model's own: a throw here would take the whole window down.
            _enabled = false;
            _faults?.LogOnce("ledger constructor", ex);
        }
    }

    [DataSourceProperty]
    public bool ShowLedgerIcons
    {
        get => _showLedgerIcons;
        set => SetField(ref _showLedgerIcons, value, nameof(ShowLedgerIcons));
    }

    // Created once; each refresh swaps the callback, so the widgets keep the same data source.
    [DataSourceProperty]
    public BasicTooltipViewModel StashTooltip { get; } = new();

    [DataSourceProperty]
    public BasicTooltipViewModel PrisonTooltip { get; } = new();

    public override void OnRefresh()
    {
        if (!_enabled)
            return;
        _faults!.Run("title", SetTitle);
        _faults.Run("profit row", AddProfitRow);
        _faults.Run("patrol row", AddPatrolRow);
        _faults.Run("icon tooltips", RefreshIcons);
    }

    private void SetTitle()
    {
        var vm = ViewModel;
        if (vm != null)
            vm.ManageText = _settlement!.Name.ToString();
    }

    private void AddProfitRow()
    {
        var vm = ViewModel;
        if (vm == null)
            return;
        var profit = _service!.ComposeProfit(_fief!.ReadProfit(_settlement!));
        var taxLabel = GameTexts.FindText("str_town_management_population_tax").ToString();
        var title = GameTexts.FindText("str_LEFT_colon").SetTextVariable("LEFT", GameTexts.FindText("str_profit"));
        _profitTooltip.SetToolipCallback(() => _faults!.Lines("profit tooltip", () => LedgerTooltips.Profit(profit, taxLabel)));
        Replace(vm.MiddleFirstTextList, ref _profitRow,
            new TownManagementDescriptionItemVM(title, profit.Total, 0, TownManagementDescriptionItemVM.DescriptionType.Gold, _profitTooltip));
    }

    private void AddPatrolRow()
    {
        var vm = ViewModel;
        if (vm == null || !_service!.ShowsPatrolRow(_settlement!.IsTown, _settlement.IsCastle, _fief!.ReadPatrolAlive(_settlement), _fief.PatrolModelAllows(_settlement)))
            return;
        var title = GameTexts.FindText("str_LEFT_colon").SetTextVariable("LEFT", GameTexts.FindText("str_patrol"));
        _patrolTooltip.SetHintCallback(() => _faults!.Text("patrol tooltip", () => _fief!.PatrolTooltip(_settlement!)));
        Replace(vm.MiddleSecondTextList, ref _patrolRow,
            new TownManagementDescriptionItemVM(title, _fief!.ReadPatrolMen(_settlement!), 0, TownManagementDescriptionItemVM.DescriptionType.Security, _patrolTooltip));
    }

    private void RefreshIcons()
    {
        // Snapshots now, formatted on hover: the lists cannot change while the window is open, except through a refresh.
        var stash = _service!.SummarizeStash(_fief!.ReadStash(_settlement!), LedgerTooltips.MaxLines);
        var prison = _service.SummarizePrisoners(_fief.ReadPrisoners(_settlement!), LedgerTooltips.MaxLines);
        StashTooltip.SetToolipCallback(() => _faults!.Lines("stash tooltip", () => LedgerTooltips.Stash(stash)));
        PrisonTooltip.SetToolipCallback(() => _faults!.Lines("prison tooltip", () => LedgerTooltips.Prisoners(prison)));
        ShowLedgerIcons = true;
    }

    // Drop the row this mixin added last time, so a refresh that the vanilla method did not precede cannot stack a second one.
    private static void Replace(MBBindingList<TownManagementDescriptionItemVM> list, ref TownManagementDescriptionItemVM? old, TownManagementDescriptionItemVM row)
    {
        if (old != null)
            list.Remove(old);
        list.Add(row);
        old = row;
    }
}
