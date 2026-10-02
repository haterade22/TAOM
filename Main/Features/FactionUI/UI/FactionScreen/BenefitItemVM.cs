using TaleWorlds.Library;

namespace TAOM.Features.FactionUI.UI.FactionScreen;

/// <summary>One faction bonus or penalty (#704, Kysaro's).</summary>
public sealed class BenefitItemVM : ViewModel
{
    public BenefitItemVM(string text, bool isPositive)
    {
        Text = text;
        IsPositive = isPositive;
    }

    [DataSourceProperty]
    public string Text { get; }

    [DataSourceProperty]
    public bool IsPositive { get; }

    [DataSourceProperty]
    public bool IsNegative => !IsPositive;
}
