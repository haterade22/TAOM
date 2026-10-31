using System;
using TaleWorlds.Library;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>One hero of the commander's catalog: ready to field, in the field, or fallen with a buyback. The button does whichever applies.</summary>
public sealed class FordsHeroOfferVM : FordsViewModel
{
    private readonly Action<string> _onAct;
    private string _name = string.Empty;
    private string _statusText = string.Empty;
    private string _actionText = string.Empty;
    private bool _canAct;

    internal FordsHeroOfferVM(Action<string> onAct)
    {
        _onAct = onAct;
    }

    /// <summary>The hero this row acts on; each refresh rebinds it, because a dropped offer shifts the rows after it.</summary>
    internal string HeroId { get; private set; } = string.Empty;

    [DataSourceProperty]
    public string Name
    {
        get => _name;
        private set => Set(ref _name, value, nameof(Name));
    }

    [DataSourceProperty]
    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value, nameof(StatusText));
    }

    [DataSourceProperty]
    public string ActionText
    {
        get => _actionText;
        private set => Set(ref _actionText, value, nameof(ActionText));
    }

    [DataSourceProperty]
    public bool CanAct
    {
        get => _canAct;
        private set => Set(ref _canAct, value, nameof(CanAct));
    }

    public void ExecuteAct() => _onAct(HeroId);

    internal void Update(string heroId, string name, string status, string action, bool canAct)
    {
        HeroId = heroId;
        Name = name;
        StatusText = status;
        ActionText = action;
        CanAct = canAct;
    }
}
