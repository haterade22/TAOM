using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.UI;

public class CareerChoiceGroupObjectVM : ViewModel
{
    private readonly CareerChoiceGroupDefinition _group;
    private MBBindingList<CareerChoiceObjectVM> _choices;

    public CareerChoiceGroupObjectVM(CareerChoiceGroupDefinition group)
    {
        _group = group;
        _choices = new MBBindingList<CareerChoiceObjectVM>();
    }

    [DataSourceProperty]
    public string GroupName => !string.IsNullOrEmpty(_group.DisplayName)
        ? new TextObject(_group.DisplayName).ToString()
        : HumanizeId(_group.Id);

    // Fallback when a group has no authored display_name yet: turn "ranger_of_ithilien_t1_a"
    // into "Path A" (or title-cased words if the id doesn't carry a "_t<N>_<letter>" suffix),
    // so headers are never raw ids even before lore names are authored.
    private static string HumanizeId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        var parts = id.Split('_');
        var last = parts[parts.Length - 1];
        if (parts.Length >= 2 && last.Length == 1 && char.IsLetter(last[0]))
        {
            var tierTok = parts[parts.Length - 2];
            if (tierTok.Length >= 2 && tierTok[0] == 't' && char.IsDigit(tierTok[1]))
                return "Path " + char.ToUpperInvariant(last[0]);
        }
        return string.Join(" ", System.Array.ConvertAll(parts, p =>
            p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p.Substring(1)));
    }

    [DataSourceProperty]
    public int Tier => _group.Tier;

    [DataSourceProperty]
    public MBBindingList<CareerChoiceObjectVM> Choices
    {
        get => _choices;
        set
        {
            if (_choices != value)
            {
                _choices = value;
                OnPropertyChangedWithValue(value, nameof(Choices));
            }
        }
    }
}
