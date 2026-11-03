using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>The look that tells factions apart: one accent colour per faction. Layout and state colours stay the same for all of them.</summary>
internal static class FordsFactionStyle
{
    internal const string DefaultAccent = "#FFD700FF";

    internal static string Accent(string factionId)
    {
        switch (factionId)
        {
            case "gondor": return "#A9CDF0FF";
            case "rohan": return "#E8B93FFF";
            case "mordor": return "#E5512FFF";
            case "isengard": return "#C4D0B8FF";
            case "rivendell": return "#7FE0C8FF";
            case "erebor": return "#E0925AFF";
            case "harad": return "#D8567AFF";
            case "gundabad": return "#C08CE0FF";
            default: return DefaultAccent;
        }
    }

    /// <summary>The troop-type icon of a role. Each name is a sprite of Native's ui_group1 category, which is always loaded.</summary>
    internal static string RoleIcon(UnitRole role)
    {
        switch (role)
        {
            case UnitRole.Ranged: return @"General\TroopTypeIcons\icon_troop_type_bow_big";
            case UnitRole.Cavalry: return @"General\TroopTypeIcons\icon_troop_type_cavalry_big";
            case UnitRole.HorseArcher: return @"General\TroopTypeIcons\icon_troop_type_horse_archer_big";
            default: return @"General\TroopTypeIcons\icon_troop_type_infantry_big";
        }
    }

    internal static string PriceColor(bool canAfford) => canAfford ? "#FFD700FF" : "#FF7A5AFF";
}

/// <summary>One line of the faction's introduction: a strength or a weakness.</summary>
public sealed class FordsTraitVM : FordsViewModel
{
    private string _text = string.Empty;
    private string _signText = string.Empty;
    private string _signColor = "#FFFFFFFF";

    [DataSourceProperty]
    public string Text
    {
        get => _text;
        private set => Set(ref _text, value, nameof(Text));
    }

    [DataSourceProperty]
    public string SignText
    {
        get => _signText;
        private set => Set(ref _signText, value, nameof(SignText));
    }

    [DataSourceProperty]
    public string SignColor
    {
        get => _signColor;
        private set => Set(ref _signColor, value, nameof(SignColor));
    }

    internal void Update(FactionTrait trait)
    {
        Text = new TextObject(trait.Text).ToString();
        SignText = trait.Strength ? "+" : "-";
        SignColor = trait.Strength ? "#7FD67FFF" : "#FF7A5AFF";
    }
}

/// <summary>One region a commander can call by name.</summary>
public sealed class FordsRegionChoiceVM : FordsViewModel
{
    private readonly Action<string> _onCall;
    private string _name = string.Empty;
    private bool _canCall;

    internal FordsRegionChoiceVM(Action<string> onCall)
    {
        _onCall = onCall;
    }

    internal string RegionId { get; private set; } = string.Empty;

    [DataSourceProperty]
    public string Name
    {
        get => _name;
        private set => Set(ref _name, value, nameof(Name));
    }

    [DataSourceProperty]
    public bool CanCall
    {
        get => _canCall;
        private set => Set(ref _canCall, value, nameof(CanCall));
    }

    public void ExecuteCall() => _onCall(RegionId);

    internal void Update(string regionId, string name, bool canCall)
    {
        RegionId = regionId;
        Name = name;
        CanCall = canCall;
    }
}

/// <summary>One creature of the call. Its key is its place in the row.</summary>
public sealed class FordsCreatureVM : FordsViewModel
{
    private readonly int _index;
    private readonly Action<int> _onCall;
    private string _name = string.Empty;
    private string _priceText = string.Empty;
    private string _priceColor = FordsFactionStyle.PriceColor(true);
    private bool _canCall;

    internal FordsCreatureVM(int index, Action<int> onCall)
    {
        _index = index;
        _onCall = onCall;
    }

    [DataSourceProperty]
    public string Name
    {
        get => _name;
        private set => Set(ref _name, value, nameof(Name));
    }

    [DataSourceProperty]
    public string PriceText
    {
        get => _priceText;
        private set => Set(ref _priceText, value, nameof(PriceText));
    }

    [DataSourceProperty]
    public string PriceColor
    {
        get => _priceColor;
        private set => Set(ref _priceColor, value, nameof(PriceColor));
    }

    [DataSourceProperty]
    public string KeyText => _index < FordsCommandVM.CreatureKeys.Length ? FordsCommandVM.CreatureKeys[_index] : string.Empty;

    [DataSourceProperty]
    public bool CanCall
    {
        get => _canCall;
        private set => Set(ref _canCall, value, nameof(CanCall));
    }

    public void ExecuteCall() => _onCall(_index);

    internal void Update(MatchView.UnitOfferView offer, string name, bool ready)
    {
        Name = offer.PackSize > 1 ? FordsHudText.CreaturePack(name, offer.PackSize) : name;
        PriceText = FordsHudText.Price(offer.Price);
        PriceColor = FordsFactionStyle.PriceColor(offer.CanAfford);
        CanCall = ready && offer.CanAfford;
    }
}

/// <summary>One neutral camp on the battle board.</summary>
public sealed class FordsCampVM : FordsViewModel
{
    private string _text = string.Empty;
    private string _textColor = "#FFFFFFFF";

    [DataSourceProperty]
    public string Text
    {
        get => _text;
        private set => Set(ref _text, value, nameof(Text));
    }

    [DataSourceProperty]
    public string TextColor
    {
        get => _textColor;
        private set => Set(ref _textColor, value, nameof(TextColor));
    }

    internal void Update(int number, MatchView.CampView camp)
    {
        Text = camp.Alive > 0 ? FordsHudText.CampAlive(number, camp.Alive, camp.Reward) : FordsHudText.CampCleared(number);
        TextColor = camp.Alive > 0 ? "#FFFFFFFF" : "#888888FF";
    }
}
