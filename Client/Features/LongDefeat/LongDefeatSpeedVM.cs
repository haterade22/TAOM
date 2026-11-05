using System;
using TaleWorlds.Library;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>One speed button in the shop panel: its label, whether it is the current speed, and a click that sets it.</summary>
public sealed class LongDefeatSpeedVM : LongDefeatViewModel
{
    private readonly int _speed;
    private readonly Action<int> _onSelect;
    private bool _isCurrent;

    internal LongDefeatSpeedVM(int speed, Action<int> onSelect)
    {
        _speed = speed;
        _onSelect = onSelect;
        Text = LongDefeatHudText.SpeedButton(speed);
    }

    [DataSourceProperty]
    public string Text { get; }

    [DataSourceProperty]
    public bool IsCurrent
    {
        get => _isCurrent;
        private set => Set(ref _isCurrent, value, nameof(IsCurrent));
    }

    public void ExecuteSelect() => _onSelect(_speed);

    internal void Update(int current) => IsCurrent = current == _speed;
}
