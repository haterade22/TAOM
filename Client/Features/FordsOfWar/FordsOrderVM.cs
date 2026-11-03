using System;
using TaleWorlds.Library;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>One of the three lane orders: its name, key, whether it is the standing order and the cooldown that blocks it.</summary>
public sealed class FordsOrderVM : FordsViewModel
{
    private readonly Action<LaneOrder> _onSet;
    private string _text = string.Empty;
    private string _keyText = string.Empty;
    private string _cooldownText = string.Empty;
    private bool _isActive;
    private bool _canSet;
    private float _cooldownHeight;

    /// <summary>The height in pixels of the prefab's order button.</summary>
    public const float ButtonHeight = 59f;

    internal FordsOrderVM(LaneOrder order, string keyText, Action<LaneOrder> onSet)
    {
        Order = order;
        _onSet = onSet;
        KeyText = keyText;
    }

    internal LaneOrder Order { get; }

    [DataSourceProperty]
    public string Text
    {
        get => _text;
        private set => Set(ref _text, value, nameof(Text));
    }

    [DataSourceProperty]
    public string KeyText
    {
        get => _keyText;
        private set => Set(ref _keyText, value, nameof(KeyText));
    }

    [DataSourceProperty]
    public string CooldownText
    {
        get => _cooldownText;
        private set => Set(ref _cooldownText, value, nameof(CooldownText));
    }

    [DataSourceProperty]
    public bool IsActive
    {
        get => _isActive;
        private set => Set(ref _isActive, value, nameof(IsActive));
    }

    [DataSourceProperty]
    public bool CanSet
    {
        get => _canSet;
        private set => Set(ref _canSet, value, nameof(CanSet));
    }

    /// <summary>The height of the shade that drains as the cooldown runs; the prefab's button is ButtonHeight tall.</summary>
    [DataSourceProperty]
    public float CooldownHeight
    {
        get => _cooldownHeight;
        private set => Set(ref _cooldownHeight, value, nameof(CooldownHeight));
    }

    public void ExecuteSet() => _onSet(Order);

    /// <summary>The standing order can be confirmed again at no cost; any other waits out the lane's cooldown. None can be set outside the live phase.</summary>
    internal void Update(LaneOrder standing, float cooldownLeft, bool live, float cooldownTotal = 0f)
    {
        var active = standing == Order;
        var blocked = !active && cooldownLeft > 0f;
        Text = FordsHudText.Order(Order);
        IsActive = active;
        CanSet = live && !blocked;
        CooldownText = blocked ? FordsHudText.Cooldown((int)Math.Ceiling(cooldownLeft)) : string.Empty;
        CooldownHeight = !blocked || cooldownTotal <= 0f ? 0f : (float)Math.Round(ButtonHeight * Math.Min(1f, cooldownLeft / cooldownTotal));
    }
}
