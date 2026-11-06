using System;
using TaleWorlds.Library;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>
/// The shop panel's Fortifications group (phase3-plan.md section 4), buying only: a click goes to the session and the run answers. Slice 4 holds one row, field care; the gate, towers and volley rows
/// join as their effects land. A row is listed while the build offers it and the shop carries it, and stays listed once used, so a player sees it is spent.
/// </summary>
public sealed class LongDefeatFortVM : LongDefeatViewModel
{
    private readonly Action _buyCare;
    private bool _isVisible;
    private string _title = string.Empty;
    private bool _isCareVisible;
    private string _careName = string.Empty;
    private string _careDetail = string.Empty;
    private string _carePrice = string.Empty;
    private bool _canBuyCare;

    internal LongDefeatFortVM(Action buyCare) => _buyCare = buyCare;

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

    public void ExecuteBuyCare() => _buyCare();

    internal void Update(RunView view)
    {
        var care = view.Fort.Care;
        var used = care.MaxLevel > 0 && care.Level >= care.MaxLevel;
        IsCareVisible = care.Available || used;
        IsVisible = IsCareVisible;
        if (!IsVisible)
        {
            CanBuyCare = false;
            return;
        }

        Title = LongDefeatHudText.FortTitle();
        CareName = LongDefeatHudText.CareName();
        CareDetail = LongDefeatHudText.CareDetail(view.Fort.CareShare);
        CarePrice = used ? LongDefeatHudText.CareUsed() : LongDefeatHudText.Price(care.Price);
        CanBuyCare = care.Available && view.Gold >= care.Price;
    }

    internal void Hide()
    {
        IsVisible = false;
        IsCareVisible = false;
        CanBuyCare = false;
    }
}
