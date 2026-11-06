using System;
using TaleWorlds.Library;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>
/// The shop panel's Fortifications group (phase3-plan.md section 4), buying only: a click goes to the session and the run answers. Field care, the gate (a level row and a repair row) and the Elven Volley are in;
/// the towers join as their effect lands. The volley row only buys: the key N looses it, in a wave (D24). A row is listed while the build offers it and the shop carries it, and stays listed once used or at its top, so a player sees it is spent.
/// </summary>
public sealed class LongDefeatFortVM : LongDefeatViewModel
{
    private readonly Action _buyCare;
    private readonly Action _buyGate;
    private readonly Action _repair;
    private readonly Action _buyVolley;
    private bool _isVisible;
    private string _title = string.Empty;
    private bool _isCareVisible;
    private string _careName = string.Empty;
    private string _careDetail = string.Empty;
    private string _carePrice = string.Empty;
    private bool _canBuyCare;
    private bool _isGateVisible;
    private string _gateName = string.Empty;
    private string _gateDetail = string.Empty;
    private string _gatePrice = string.Empty;
    private bool _canBuyGate;
    private bool _isRepairVisible;
    private string _repairName = string.Empty;
    private string _repairDetail = string.Empty;
    private string _repairPrice = string.Empty;
    private bool _canRepair;
    private bool _isVolleyVisible;
    private string _volleyName = string.Empty;
    private string _volleyDetail = string.Empty;
    private string _volleyPrice = string.Empty;
    private bool _canBuyVolley;

    internal LongDefeatFortVM(Action buyCare, Action buyGate, Action repair, Action buyVolley)
    {
        _buyCare = buyCare;
        _buyGate = buyGate;
        _repair = repair;
        _buyVolley = buyVolley;
    }

    [DataSourceProperty]
    public bool IsVisible { get => _isVisible; private set => Set(ref _isVisible, value, nameof(IsVisible)); }

    [DataSourceProperty]
    public string Title { get => _title; private set => Set(ref _title, value, nameof(Title)); }

    [DataSourceProperty]
    public bool IsCareVisible { get => _isCareVisible; private set => Set(ref _isCareVisible, value, nameof(IsCareVisible)); }

    [DataSourceProperty]
    public string CareName { get => _careName; private set => Set(ref _careName, value, nameof(CareName)); }

    [DataSourceProperty]
    public string CareDetail { get => _careDetail; private set => Set(ref _careDetail, value, nameof(CareDetail)); }

    [DataSourceProperty]
    public string CarePrice { get => _carePrice; private set => Set(ref _carePrice, value, nameof(CarePrice)); }

    [DataSourceProperty]
    public bool CanBuyCare { get => _canBuyCare; private set => Set(ref _canBuyCare, value, nameof(CanBuyCare)); }

    [DataSourceProperty]
    public bool IsGateVisible { get => _isGateVisible; private set => Set(ref _isGateVisible, value, nameof(IsGateVisible)); }

    [DataSourceProperty]
    public string GateName { get => _gateName; private set => Set(ref _gateName, value, nameof(GateName)); }

    [DataSourceProperty]
    public string GateDetail { get => _gateDetail; private set => Set(ref _gateDetail, value, nameof(GateDetail)); }

    [DataSourceProperty]
    public string GatePrice { get => _gatePrice; private set => Set(ref _gatePrice, value, nameof(GatePrice)); }

    [DataSourceProperty]
    public bool CanBuyGate { get => _canBuyGate; private set => Set(ref _canBuyGate, value, nameof(CanBuyGate)); }

    [DataSourceProperty]
    public bool IsRepairVisible { get => _isRepairVisible; private set => Set(ref _isRepairVisible, value, nameof(IsRepairVisible)); }

    [DataSourceProperty]
    public string RepairName { get => _repairName; private set => Set(ref _repairName, value, nameof(RepairName)); }

    [DataSourceProperty]
    public string RepairDetail { get => _repairDetail; private set => Set(ref _repairDetail, value, nameof(RepairDetail)); }

    [DataSourceProperty]
    public string RepairPrice { get => _repairPrice; private set => Set(ref _repairPrice, value, nameof(RepairPrice)); }

    [DataSourceProperty]
    public bool CanRepair { get => _canRepair; private set => Set(ref _canRepair, value, nameof(CanRepair)); }

    [DataSourceProperty]
    public bool IsVolleyVisible { get => _isVolleyVisible; private set => Set(ref _isVolleyVisible, value, nameof(IsVolleyVisible)); }

    [DataSourceProperty]
    public string VolleyName { get => _volleyName; private set => Set(ref _volleyName, value, nameof(VolleyName)); }

    [DataSourceProperty]
    public string VolleyDetail { get => _volleyDetail; private set => Set(ref _volleyDetail, value, nameof(VolleyDetail)); }

    [DataSourceProperty]
    public string VolleyPrice { get => _volleyPrice; private set => Set(ref _volleyPrice, value, nameof(VolleyPrice)); }

    [DataSourceProperty]
    public bool CanBuyVolley { get => _canBuyVolley; private set => Set(ref _canBuyVolley, value, nameof(CanBuyVolley)); }

    public void ExecuteBuyCare() => _buyCare();

    public void ExecuteBuyGate() => _buyGate();

    public void ExecuteRepair() => _repair();

    public void ExecuteBuyVolley() => _buyVolley();

    internal void Update(RunView view)
    {
        var care = view.Fort.Care;
        var used = care.MaxLevel > 0 && care.Level >= care.MaxLevel;
        IsCareVisible = care.Available || used;
        var gate = view.Fort.Gate;
        IsGateVisible = gate.Available || gate.Level > 0;
        IsRepairVisible = IsGateVisible;
        var volley = view.Fort.Volley;
        IsVolleyVisible = volley.Available || volley.Level > 0;
        IsVisible = IsCareVisible || IsGateVisible || IsVolleyVisible;
        if (!IsVisible)
        {
            CanBuyCare = false;
            CanBuyGate = false;
            CanRepair = false;
            CanBuyVolley = false;
            return;
        }

        Title = LongDefeatHudText.FortTitle();
        CareName = LongDefeatHudText.CareName();
        CareDetail = LongDefeatHudText.CareDetail(view.Fort.CareShare);
        CarePrice = used ? LongDefeatHudText.CareUsed() : LongDefeatHudText.Price(care.Price);
        CanBuyCare = care.Available && view.Gold >= care.Price;
        UpdateGate(view, gate);
        UpdateVolley(view, volley);
    }

    private void UpdateVolley(RunView view, FortRow volley)
    {
        var atTop = volley.MaxLevel > 0 && volley.Level >= volley.MaxLevel;
        VolleyName = LongDefeatHudText.VolleyName();
        VolleyDetail = LongDefeatHudText.VolleyDetail(Math.Min(volley.Level + 1, Math.Max(1, volley.MaxLevel)), volley.MaxLevel, view.Fort.VolleyShown);
        VolleyPrice = atTop ? LongDefeatHudText.VolleyFull() : LongDefeatHudText.Price(volley.Price);
        CanBuyVolley = volley.Available && view.Gold >= volley.Price;
    }

    private void UpdateGate(RunView view, FortRow gate)
    {
        var repair = view.Fort.Repair;
        var atTop = gate.MaxLevel > 0 && gate.Level >= gate.MaxLevel;
        GateName = LongDefeatHudText.GateName();
        GateDetail = LongDefeatHudText.GateDetail(Math.Min(gate.Level + 1, Math.Max(1, gate.MaxLevel)), gate.MaxLevel, view.Fort.GatePercent);
        GatePrice = atTop ? LongDefeatHudText.GateFull() : LongDefeatHudText.Price(gate.Price);
        CanBuyGate = gate.Available && view.Gold >= gate.Price;
        RepairName = LongDefeatHudText.RepairName();
        RepairDetail = LongDefeatHudText.RepairDetail();
        RepairPrice = repair.Available ? LongDefeatHudText.Price(repair.Price) : LongDefeatHudText.RepairNone();
        CanRepair = repair.Available && view.Gold >= repair.Price;
    }

    internal void Hide()
    {
        IsVisible = false;
        IsCareVisible = false;
        IsGateVisible = false;
        IsRepairVisible = false;
        IsVolleyVisible = false;
        CanBuyCare = false;
        CanBuyGate = false;
        CanRepair = false;
        CanBuyVolley = false;
    }
}
